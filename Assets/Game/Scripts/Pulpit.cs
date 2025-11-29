using UnityEngine;
using System.Collections;

/// <summary>
/// Pulpit lifecycle:
/// - Init(min,max) sets lifetime and starts coroutine
/// - Updates optional countdownText (UI Text)
/// - Requests spawn early via GameManager.RequestSpawnFrom(transform) if desired (kept for compatibility)
/// - On lifetime end notifies GameManager and destroys itself
/// </summary>
[RequireComponent(typeof(Collider))]
public class Pulpit : MonoBehaviour
{
    public float lifetime = 4f;
    public TextMesh countdownText; // assign in prefab (optional world UI text to display remaining lifetime)

    bool hasRequested = false;

    public void Init(float minTime, float maxTime)
    {
        lifetime = Random.Range(minTime, maxTime);
        StartCoroutine(LifeRoutine());
        Debug.Log($"[Pulpit] Init on '{gameObject.name}' lifetime={lifetime:F2} (min={minTime}, max={maxTime})");
    }

    IEnumerator LifeRoutine()
    {
        float t = lifetime;

        while (t > 0f)
        {
            // update UI at start of frame
            if (countdownText != null)
            {
                countdownText.text = t.ToString("0.00");
            }

            // decrease
            t -= Time.deltaTime;

            // legacy compatibility: request spawn somewhat early (optional)
            if (!hasRequested && t <= lifetime * 0.6f)
            {
                hasRequested = true;
                if (GameManager.Instance != null)
                {
                    Debug.Log($"[Pulpit] Requested spawn from {gameObject.name} at {transform.position}");
                    GameManager.Instance.RequestSpawnFrom(this.transform);
                }
            }

            yield return null;
        }

        // finalize display to 0.00
        if (countdownText != null) countdownText.text = "0.00";

        // lifetime ended: notify manager and destroy
        if (GameManager.Instance != null)
        {
            Debug.Log($"[Pulpit] Lifetime ended for '{gameObject.name}' notifying GM.");
            GameManager.Instance.NotifyDestroyed(this.gameObject);
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Safe notify in case GM still needs to clean up (GM handles double removals)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyDestroyed(this.gameObject);
        }
    }
}
