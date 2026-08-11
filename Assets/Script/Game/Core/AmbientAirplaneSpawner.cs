using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decorative airplane traffic. Waits a random cooldown, picks a random prefab from the
/// fleet, and flies it from the spawn anchor to the destination anchor at a random speed.
///
/// Pure set dressing — it owns no round rules, so the lobby and gameplay scenes both use
/// it. Gameplay scenes leave <c>requireActiveRound</c> on so planes only fly during a
/// round; menus turn it off because there is no GameManager there.
/// </summary>
public class AmbientAirplaneSpawner : MonoBehaviour
{
    [Header("Fleet")]
    [SerializeField, Tooltip("One is picked at random per flight.")]
    private GameObject[] airplanePrefabs;

    [Header("Path")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform destinationPoint;
    [SerializeField, Tooltip("Turn the plane to face the way it is travelling. Off keeps the spawn anchor's rotation.")]
    private bool faceTravelDirection = true;

    [Header("Timing")]
    [SerializeField, Min(0f), Tooltip("Quiet period before the first flight.")]
    private float initialDelay = 2f;
    [SerializeField, Tooltip("Random cooldown between flights, in seconds.")]
    private Vector2 spawnDelayRange = new(8f, 20f);
    [SerializeField, Tooltip("Random world units per second for each flight.")]
    private Vector2 speedRange = new(10f, 40f);
    [SerializeField, Min(1), Tooltip("How many planes may be in the air at once.")]
    private int maxConcurrent = 1;

    [Header("Gating")]
    [SerializeField, Tooltip("Gameplay: fly only while a round is running. Menus: leave this off.")]
    private bool requireActiveRound = true;

    // One idle stack per prefab, so a Pesawat1 is never reused as a Pesawat2.
    private readonly Dictionary<GameObject, Stack<GameObject>> idle = new();
    private int inFlight;

    private bool RoundRunning =>
        GameManager.Instance != null
        && GameManager.Instance.IsRoundStarted
        && !GameManager.Instance.IsRoundEnded;

    private void Awake()
    {
        if (spawnPoint != null && destinationPoint != null && HasFleet())
            return;

        Debug.LogError($"{nameof(AmbientAirplaneSpawner)} '{name}' needs both path anchors and at least one prefab.", this);
        enabled = false;
    }

    private bool HasFleet()
    {
        if (airplanePrefabs == null) return false;
        foreach (var p in airplanePrefabs)
            if (p != null) return true;
        return false;
    }

    private void OnDisable() => inFlight = 0;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(spawnDelayRange.x, spawnDelayRange.y));

            if (requireActiveRound && !RoundRunning) continue;
            if (inFlight >= maxConcurrent) continue;

            StartCoroutine(Fly());
        }
    }

    private IEnumerator Fly()
    {
        GameObject prefab = PickPrefab();
        if (prefab == null) yield break;

        GameObject plane = Take(prefab);
        inFlight++;

        Vector3 from = spawnPoint.position;
        Vector3 to = destinationPoint.position;
        Vector3 heading = to - from;

        plane.transform.SetPositionAndRotation(
            from,
            faceTravelDirection && heading.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(heading.normalized, Vector3.up)
                : spawnPoint.rotation);
        plane.SetActive(true);

        float speed = Random.Range(speedRange.x, speedRange.y);
        while (plane.transform.position != to)
        {
            plane.transform.position = Vector3.MoveTowards(
                plane.transform.position, to, speed * Time.deltaTime);
            yield return null;
        }

        Release(prefab, plane);
        inFlight--;
    }

    private GameObject PickPrefab()
    {
        // Skip empty slots so a half-filled array in the inspector still works.
        var candidates = new List<GameObject>(airplanePrefabs.Length);
        foreach (var p in airplanePrefabs)
            if (p != null) candidates.Add(p);

        return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
    }

    private GameObject Take(GameObject prefab)
    {
        if (idle.TryGetValue(prefab, out var stack) && stack.Count > 0)
            return stack.Pop();

        return Instantiate(prefab, transform);
    }

    private void Release(GameObject prefab, GameObject plane)
    {
        plane.SetActive(false);

        if (!idle.TryGetValue(prefab, out var stack))
            idle[prefab] = stack = new Stack<GameObject>();
        stack.Push(plane);
    }
}
