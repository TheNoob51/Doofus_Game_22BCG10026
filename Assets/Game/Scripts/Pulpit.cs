using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class Pulpit : MonoBehaviour
{
    public float lifetime;
    public TextMesh countdownText; // optional; assign in prefab if you want visible timer

    bool hasRequestedSpawn = false;

    public void Init(float minTime, float maxTime)
    {
        lifetime = Random.Range(minTime, maxTime);
        StartCoroutine(LifeRoutine());
    }

    IEnumerator LifeRoutine()
    {
        float t = lifetime;

        while (t > 0f)
        {
            t -= Time.deltaTime;

            // Update countdown text if present
            if (countdownText != null)
                countdownText.text = t.ToString("0.00");

            // Request next platform spawn earlier (at 60% remaining)
            if (!hasRequestedSpawn && t <= lifetime * 0.6f)
            {
                hasRequestedSpawn = true;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RequestSpawnFrom(this.transform);
                    Debug.Log($"[Pulpit] Requested spawn from {gameObject.name} at {transform.position}");
                }
            }

            yield return null;
        }

        // End of life: notify GM and destroy
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyDestroyed(this.gameObject);
            Debug.Log($"[Pulpit] Lifetime ended for '{gameObject.name}' notifying GM.");
        }
        else
        {
            Debug.LogWarning("[Pulpit] No GameManager instance found at destroy time.");
        }

        Destroy(this.gameObject);
    }
}
