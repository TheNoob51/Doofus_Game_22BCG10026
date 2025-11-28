using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Robust GameManager:
/// - grid-aligned platform spawning (no Y jitter)
/// - enforces maxPlatforms (only N active at any time)
/// - RequestSpawnFrom/ForceSpawnAdjacent operate on grid
/// - ResetSceneClean() for UI Retry
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("References")]
    public GameObject pulpitPrefab;   // assign prefab
    public GameObject player;         // assign Player gameobject
    public Transform spawnPoint;      // initial spawn origin (use this as grid origin)

    [Header("Lifetime Settings")]
    public float minLifetime = 4f;
    public float maxLifetime = 5f;

    [Header("Platform Counts")]
    public int maxPlatforms = 2;      // IMPORTANT: user requested exactly 2 active at once
    public int initialFill = 1;       // initial number to create (should be <= maxPlatforms)

    // internal
    private List<GameObject> active = new List<GameObject>();
    private float gridSpacing = 3f;   // will be set from pulpitPrefab scale.x at Start
    private float spawnY = 0f;        // fixed Y for all platforms

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
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
            Debug.LogWarning("[GM] spawnPoint not assigned � using origin (0,0,0)");
            spawnPoint = new GameObject("SpawnPoint_TEMP").transform;
            spawnPoint.position = Vector3.zero;
        }

        // set grid spacing from prefab width (center-to-center)
        gridSpacing = Mathf.Abs(pulpitPrefab.transform.localScale.x);
        if (gridSpacing <= 0.01f) gridSpacing = 3f;
        spawnY = spawnPoint.position.y;

        Debug.Log($"[GM] Start() gridSpacing={gridSpacing} spawnY={spawnY} maxPlatforms={maxPlatforms}");

        // spawn initial platforms (respect maxPlatforms)
        SpawnStartArea();
    }

    // ------------------------------------------------------------------
    // Spawning helpers
    // ------------------------------------------------------------------
    void SpawnStartArea()
    {
        Vector3 origin = GridAlign(spawnPoint.position);

        // Ensure we do not exceed maxPlatforms
        int toSpawn = Mathf.Clamp(initialFill, 1, maxPlatforms);

        // Spawn base at origin
        SpawnPulpitAtGrid(origin);

        // place player above the origin platform
        PlacePlayerOn(origin);

        // Fill others adjacent (tries available directions)
        for (int i = 1; i < toSpawn; i++)
            ForceSpawnAdjacent(origin);
    }

    // Align position to grid (center positions)
    Vector3 GridAlign(Vector3 worldPos)
    {
        float gx = Mathf.Round(worldPos.x / gridSpacing) * gridSpacing;
        float gz = Mathf.Round(worldPos.z / gridSpacing) * gridSpacing;
        return new Vector3(gx, spawnY, gz);
    }

    GameObject SpawnPulpitAtGrid(Vector3 gridCenter)
    {
        // safety: remove null references
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
        return p;
    }

    // Try to spawn adjacent to basePos (grid-aligned). returns true if spawned.
    bool TrySpawnAdjacent(Vector3 basePos)
    {
        // remove dead refs
        active.RemoveAll(x => x == null);

        if (active.Count >= maxPlatforms) return false;

        Vector3[] offsets = new Vector3[]
        {
            new Vector3(gridSpacing, 0, 0),
            new Vector3(-gridSpacing, 0, 0),
            new Vector3(0, 0, gridSpacing),
            new Vector3(0, 0, -gridSpacing)
        };

        // try in random order for variety
        int[] idx = { 0, 1, 2, 3 };
        for (int i = 0; i < idx.Length; i++) { int r = Random.Range(i, idx.Length); int tmp = idx[r]; idx[r] = idx[i]; idx[i] = tmp; }

        foreach (int i in idx)
        {
            Vector3 candidate = basePos + offsets[i];
            Vector3 grid = GridAlign(candidate);
            if (!SpotOccupied(grid))
            {
                SpawnPulpitAtGrid(grid);
                return true;
            }
        }
        return false;
    }

    // Force spawn adjacent (used at start) - tries all directions
    void ForceSpawnAdjacent(Vector3 baseWorld)
    {
        // try all directions
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

    // returns true if a platform center is within threshold of pos
    bool SpotOccupied(Vector3 gridCenter)
    {
        float threshold = gridSpacing * 0.9f; // slightly less than spacing to avoid false overlap
        foreach (var p in active)
        {
            if (p == null) continue;
            if (Vector3.Distance(p.transform.position, gridCenter) < threshold)
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // External API used by Pulpit
    // ------------------------------------------------------------------
    // Pulpit calls this to request spawning a neighbor from its position
    public void RequestSpawnFrom(Transform source)
    {
        if (source == null) return;
        // use source position aligned to grid
        Vector3 gridSource = GridAlign(source.position);

        // if active already at max, we do nothing (requirement: only maxPlatforms active)
        if (active.Count >= maxPlatforms)
        {
            Debug.Log("[GM] RequestSpawnFrom ignored: maxPlatforms reached");
            return;
        }

        bool spawned = TrySpawnAdjacent(gridSource);
        if (!spawned)
        {
            Debug.Log("[GM] RequestSpawnFrom: no free adjacent spot found for " + source.name);
        }
    }

    // Called by Pulpit when it is destroyed
    public void NotifyDestroyed(GameObject p)
    {
        if (p == null)
        {
            active.RemoveAll(x => x == null);
            return;
        }
        if (active.Contains(p))
            active.Remove(p);

        Debug.Log($"[GM] NotifyDestroyed for '{(p ? p.name : "null")}' before removal -> active now={active.Count}");

        // Optional immediate refill if below desired initialFill count:
        if (active.Count < initialFill)
        {
            // try spawn adjacent to player first
            if (!TrySpawnAdjacent(player.transform.position))
            {
                // else try around origin
                ForceSpawnAdjacent(spawnPoint.position);
            }
        }
    }

    // ------------------------------------------------------------------
    // Player placement
    // ------------------------------------------------------------------
    void PlacePlayerOn(Vector3 gridCenter)
    {
        Vector3 playerPos = new Vector3(gridCenter.x, spawnY + 1.0f, gridCenter.z); // assume player half height ~0.5
        player.transform.position = playerPos;

        // zero velocities if rigidbody
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("[GM] Player placed at " + playerPos);
    }

    // ------------------------------------------------------------------
    // Retry helper called by UIManager
    // ------------------------------------------------------------------
    public void ResetSceneClean()
    {
        Debug.Log("[GM] ResetSceneClean called: destroying active pulpits and reloading scene.");
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var obj = active[i];
            if (obj != null) Destroy(obj);
        }
        active.Clear();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Editor/Test helper
    [ContextMenu("Debug_LogActive")]
    void DebugLogActive()
    {
        Debug.Log("[GM] Active platforms: " + active.Count);
    }
}
