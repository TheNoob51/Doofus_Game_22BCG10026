using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;

/// <summary>
/// Grid-aligned GameManager that:
/// - Loads JSON config (player speed, pulpit lifetimes, spawnInterval)
/// - Spawns initial area
/// - Spawns new pulpit exactly spawnInterval seconds after each pulpit is created (scheduled)
/// - Ensures only maxPlatforms active at any time
/// - Prevents duplicate scheduled spawns for the same grid cell
/// - Provides ResetSceneClean() for UI retry
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("References")]
    public GameObject pulpitPrefab;   // assign prefab
    public GameObject player;         // assign Player gameobject
    public Transform spawnPoint;      // initial spawn origin (grid origin)

    [Header("Lifetime Settings")]
    public float minLifetime = 4f;
    public float maxLifetime = 5f;

    [Header("Platform Counts & Grid")]
    public int maxPlatforms = 2;      // exactly how many active at once
    public int initialFill = 1;       // how many to place at start (<= maxPlatforms)
    private float gridSpacing = 3f;   // will be derived from pulpitPrefab scale.x
    private float spawnY = 0f;

    [Header("Spawn timing (from JSON)")]
    public float spawnInterval = 2.5f; // will be overwritten by JSON if present

    // internal lists
    private List<GameObject> active = new List<GameObject>();
    // scheduled grid keys prevents double scheduling for same origin cell
    private HashSet<string> scheduled = new HashSet<string>();

    // JSON config class (expects GameConfig in project)
    private GameConfig config;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // load json config if present
        LoadJsonConfig();

        if (pulpitPrefab == null)
        {
            Debug.LogError("[GM] pulpitPrefab not assigned!");
            return;
        }
        if (player == null)
        {
            Debug.LogError("[GM] player not assigned!");
            return;
        }
        if (spawnPoint == null)
        {
            Debug.LogWarning("[GM] spawnPoint not assigned — using origin (0,0,0)");
            spawnPoint = new GameObject("SpawnPoint_TEMP").transform;
            spawnPoint.position = Vector3.zero;
        }

        // derive grid spacing from prefab scale.x (center-to-center)
        gridSpacing = Mathf.Abs(pulpitPrefab.transform.localScale.x);
        if (gridSpacing <= 0.01f) gridSpacing = 3f;
        spawnY = spawnPoint.position.y;

        Debug.Log($"[GM] Start() gridSpacing={gridSpacing} spawnY={spawnY} maxPlatforms={maxPlatforms} spawnInterval={spawnInterval}");

        // Spawn initial platforms
        SpawnStartArea();
    }

    void LoadJsonConfig()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "game_config.json");
        if (!File.Exists(path))
        {
            Debug.Log("[GM] No JSON config found at " + path + " — using inspector defaults.");
            return;
        }
        try
        {
            string json = File.ReadAllText(path);
            config = JsonUtility.FromJson<GameConfig>(json);
            if (config != null)
            {
                if (config.player_data != null)
                {
                    // apply player speed if PlayerController present
                    var pc = player != null ? player.GetComponent<PlayerController>() : null;
                    if (pc != null)
                    {
                        pc.speed = config.player_data.speed;
                        Debug.Log("[GM] Applied player speed: " + pc.speed);
                    }
                }
                if (config.pulpit_data != null)
                {
                    minLifetime = config.pulpit_data.min_pulpit_destroy_time;
                    maxLifetime = config.pulpit_data.max_pulpit_destroy_time;
                    spawnInterval = config.pulpit_data.pulpit_spawn_time;
                    Debug.Log($"[GM] Applied pulpit lifetimes: min={minLifetime} max={maxLifetime} spawnInterval={spawnInterval}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[GM] Failed to read JSON config: " + ex.Message);
        }
    }

    // -------------------- Startup spawning --------------------
    void SpawnStartArea()
    {
        Vector3 origin = GridAlign(spawnPoint.position);

        int toSpawn = Mathf.Clamp(initialFill, 1, maxPlatforms);

        // spawn base
        SpawnPulpitAtGrid(origin);

        // place player on top of base
        PlacePlayerOn(origin);

        // fill adjacent to reach 'toSpawn' count
        for (int i = 1; i < toSpawn; i++)
            ForceSpawnAdjacent(origin);
    }

    // Align arbitrary world position to grid center positions
    Vector3 GridAlign(Vector3 worldPos)
    {
        float gx = Mathf.Round(worldPos.x / gridSpacing) * gridSpacing;
        float gz = Mathf.Round(worldPos.z / gridSpacing) * gridSpacing;
        return new Vector3(gx, spawnY, gz);
    }

    // spawn a pulpit at a grid center; schedule its delayed neighbor spawn
    GameObject SpawnPulpitAtGrid(Vector3 gridCenter)
    {
        active.RemoveAll(x => x == null);

        if (active.Count >= maxPlatforms)
        {
            Debug.Log("[GM] SpawnPulpit canceled: maxPlatforms reached");
            return null;
        }

        Vector3 pos = new Vector3(gridCenter.x, spawnY, gridCenter.z);
        GameObject p = Instantiate(pulpitPrefab, pos, Quaternion.identity);
        active.Add(p);

        Pulpit pulp = p.GetComponent<Pulpit>();
        if (pulp == null) pulp = p.AddComponent<Pulpit>();
        pulp.Init(minLifetime, maxLifetime);

        Debug.Log($"[GM] Spawned pulpit at {pos} activeCount={active.Count}");

        // Schedule a spawn relative to this pulpit's grid center after spawnInterval
        ScheduleSpawnAtGrid(gridCenter);

        return p;
    }

    // Try spawn adjacent to baseGrid; returns true if a pulpit was spawned
    bool TrySpawnAdjacent(Vector3 baseGrid)
    {
        active.RemoveAll(x => x == null);

        if (active.Count >= maxPlatforms)
            return false;

        Vector3[] offsets = new Vector3[]
        {
            new Vector3(gridSpacing, 0, 0),
            new Vector3(-gridSpacing, 0, 0),
            new Vector3(0, 0, gridSpacing),
            new Vector3(0, 0, -gridSpacing)
        };

        // shuffle order
        int[] idx = { 0, 1, 2, 3 };
        for (int i = 0; i < idx.Length; i++) { int r = Random.Range(i, idx.Length); int tmp = idx[r]; idx[r] = idx[i]; idx[i] = tmp; }

        foreach (int i in idx)
        {
            Vector3 candidateGrid = GridAlign(baseGrid + offsets[i]);
            if (!SpotOccupied(candidateGrid))
            {
                SpawnPulpitAtGrid(candidateGrid);
                return true;
            }
        }

        return false;
    }

    // Force spawn adjacent (used at start) - tries all directions deterministically
    void ForceSpawnAdjacent(Vector3 baseWorld)
    {
        Vector3[] offsets = new Vector3[]
        {
            new Vector3(gridSpacing, 0, 0),
            new Vector3(-gridSpacing, 0, 0),
            new Vector3(0, 0, gridSpacing),
            new Vector3(0, 0, -gridSpacing)
        };

        foreach (var d in offsets)
        {
            Vector3 grid = GridAlign(baseWorld + d);
            if (!SpotOccupied(grid))
            {
                SpawnPulpitAtGrid(grid);
                return;
            }
        }
    }

    bool SpotOccupied(Vector3 gridCenter)
    {
        float threshold = gridSpacing * 0.6f;
        foreach (var p in active)
        {
            if (p == null) continue;
            if (Vector3.Distance(p.transform.position, gridCenter) < threshold)
                return true;
        }
        return false;
    }

    // -------------------- Scheduling logic --------------------
    // Prevent duplicate scheduled spawn for a given origin grid cell.
    void ScheduleSpawnAtGrid(Vector3 originGrid)
    {
        string key = GridKey(originGrid);
        if (scheduled.Contains(key))
        {
            Debug.Log($"[GM] Spawn already scheduled for {originGrid} (key={key})");
            return;
        }

        scheduled.Add(key);
        StartCoroutine(SpawnDelayedCoroutine(originGrid, key));
        Debug.Log($"[GM] Scheduled spawn for {originGrid} after {spawnInterval}s (key={key})");
    }

    IEnumerator SpawnDelayedCoroutine(Vector3 originGrid, string key)
    {
        // wait exactly spawnInterval
        yield return new WaitForSeconds(spawnInterval);

        // if we already have maxPlatforms, abort and clear scheduled flag
        active.RemoveAll(x => x == null);
        if (active.Count >= maxPlatforms)
        {
            Debug.Log($"[GM] Delayed spawn aborted for {originGrid} because activeCount={active.Count} >= maxPlatforms");
            scheduled.Remove(key);
            yield break;
        }

        // try to spawn adjacent to originGrid
        bool spawned = TrySpawnAdjacent(originGrid);
        if (spawned)
            Debug.Log($"[GM] Delayed spawn executed for {originGrid}");
        else
            Debug.Log($"[GM] Delayed spawn could not find adjacent spot for {originGrid}");

        // clear scheduled marker
        scheduled.Remove(key);
    }

    string GridKey(Vector3 grid)
    {
        int gx = Mathf.RoundToInt(grid.x / gridSpacing);
        int gz = Mathf.RoundToInt(grid.z / gridSpacing);
        return gx + "_" + gz;
    }

    // -------------------- External API used by Pulpit --------------------
    // Pulpit can still request spawn (backwards compatible) — we will schedule a spawn using
    // the pulpit's grid cell if none is scheduled. This keeps compatibility.
    public void RequestSpawnFrom(Transform source)
    {
        if (source == null) return;

        Vector3 gridSource = GridAlign(source.position);

        // if already at max, ignore
        active.RemoveAll(x => x == null);
        if (active.Count >= maxPlatforms)
        {
            Debug.Log("[GM] RequestSpawnFrom ignored: maxPlatforms reached");
            return;
        }

        // schedule a spawn after spawnInterval (if not already scheduled)
        string key = GridKey(gridSource);
        if (scheduled.Contains(key))
        {
            Debug.Log("[GM] RequestSpawnFrom: spawn already scheduled for " + gridSource);
            return;
        }

        // schedule at that grid cell (same behavior as SpawnPulpitAtGrid scheduling)
        scheduled.Add(key);
        StartCoroutine(SpawnDelayedCoroutine(gridSource, key));
        Debug.Log($"[GM] RequestSpawnFrom received for {source.name} at {gridSource} -> scheduling spawn after {spawnInterval}s");
    }

    public void NotifyDestroyed(GameObject p)
    {
        if (p == null)
        {
            active.RemoveAll(x => x == null);
            return;
        }
        if (active.Contains(p))
            active.Remove(p);

        Debug.Log($"[GM] NotifyDestroyed for '{p.name}' -> active now={active.Count}");

        // If active fell below initialFill, attempt a refill adjacent to player first, else origin
        if (active.Count < initialFill)
        {
            if (!TrySpawnAdjacent(player.transform.position))
            {
                ForceSpawnAdjacent(spawnPoint.position);
            }
        }
    }

    // -------------------- Player placement --------------------
    void PlacePlayerOn(Vector3 gridCenter)
    {
        Vector3 playerPos = new Vector3(gridCenter.x, spawnY + 1.0f, gridCenter.z);
        player.transform.position = playerPos;

        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("[GM] Player placed at " + playerPos);
    }

    // -------------------- Retry helper --------------------
    public void ResetSceneClean()
    {
        Debug.Log("[GM] ResetSceneClean called: destroying active pulpits and reloading scene.");
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var obj = active[i];
            if (obj != null) Destroy(obj);
        }
        active.Clear();
        scheduled.Clear();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    [ContextMenu("Debug_LogActive")]
    void DebugLogActive()
    {
        Debug.Log("[GM] Active platforms: " + active.Count);
    }
}
