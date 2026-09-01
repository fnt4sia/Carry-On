using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runs the wave loop. Reads LevelConfig and spawns luggage at intervals using a
// per-prefab object pool. Exposes ReturnLuggage(Luggage) so destroyed, expired, or sunk
// luggage can be recycled back into the pool instead of being destroyed.
//
// A scene may hold as many spawners as the layout needs; drop in another Spawner prefab
// and it feeds its own belt. Each one runs the level config's wave loop independently,
// so the config's numbers are per spawner: two spawners with luggagePerWave = 4 put 8
// bags on the floor per wave. That makes a second spawner a real difficulty change, so
// re-check the star thresholds by hand after adding one.
public class LuggageSpawner : MonoBehaviour
{
    // Registered in enable order. The first one owns the shared pool, so a bag returned
    // at any sink can be re-rented by any spawner instead of each keeping its own pile.
    private static readonly List<LuggageSpawner> activeSpawners = new();

    public static LuggageSpawner Instance
    {
        get
        {
            for (int i = 0; i < activeSpawners.Count; i++)
                if (activeSpawners[i] != null)
                    return activeSpawners[i];
            return null;
        }
    }

    private LevelConfig levelConfig;

    [Header("Fallback (no LevelConfig, e.g. menus)")]
    [Tooltip("Used when the scene has no LevelContext. Spawns these prefabs on a fixed interval " +
        "as tutorial luggage (no timer UI, never expires).")]
    [SerializeField] private List<GameObject> fallbackLuggagePrefabs = new();
    [SerializeField, Min(0.1f)] private float fallbackSpawnInterval = 4f;
    [Tooltip("Menu belts are a closed loop, so a bag that snags would otherwise let the " +
        "count creep up forever. Stop spawning once this many are riding.")]
    [SerializeField, Min(1)] private int fallbackMaxActive = 8;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private readonly Dictionary<GameObject, Queue<GameObject>> poolDictionary = new();
    private readonly List<Gate> activeDeliveryGates = new List<Gate>();
    private Transform poolRoot;

    // Created on first use so only the pool-owning spawner builds one.
    private Transform PoolRoot
    {
        get
        {
            if (poolRoot == null)
            {
                GameObject poolObject = new("LuggagePool");
                poolObject.transform.SetParent(transform, false);
                poolRoot = poolObject.transform;
            }
            return poolRoot;
        }
    }

    private void OnEnable()
    {
        if (!activeSpawners.Contains(this))
            activeSpawners.Add(this);
    }

    private void OnDisable()
    {
        activeSpawners.Remove(this);
    }

    private void Start()
    {
        ResolveLevelConfig();

        if (levelConfig == null)
        {
            if (fallbackLuggagePrefabs.Count > 0)
            {
                StartCoroutine(FallbackLoop());
                return;
            }

            Debug.LogError($"{nameof(LuggageSpawner)} on {name} has no {nameof(LevelConfig)} assigned.");
            enabled = false;
            return;
        }

        RefreshActiveDeliveryGates();
        StartCoroutine(WaveLoop());
    }

    private IEnumerator WaveLoop()
    {
        var prefabs = levelConfig.luggagePrefabs;
        if (prefabs == null || prefabs.Count == 0) yield break;

        while (true)
        {
            yield return new WaitForSeconds(levelConfig.waveDelay);

            int wavePerWave = Mathf.Max(1, levelConfig.luggagePerWave);

            for (int i = 0; i < wavePerWave; i++)
            {
                SpawnOne(prefabs);
                if (i < wavePerWave - 1)
                    yield return new WaitForSeconds(levelConfig.intraWaveInterval);
            }
        }
    }

    // Menu/tutorial mode: no LevelConfig, so spawn forever on a plain interval.
    private IEnumerator FallbackLoop()
    {
        WaitForSeconds wait = new(fallbackSpawnInterval);
        List<Luggage> live = new();

        while (true)
        {
            yield return wait;

            live.RemoveAll(bag => bag == null || !bag.gameObject.activeInHierarchy);
            if (live.Count >= fallbackMaxActive) continue;

            Luggage spawned = SpawnOne(fallbackLuggagePrefabs);
            if (spawned != null)
                live.Add(spawned);
        }
    }

    private Luggage SpawnOne(List<GameObject> prefabs)
    {
        GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
        if (prefab == null) return null;

        Luggage prefabLuggage = prefab.GetComponent<Luggage>();
        if (prefabLuggage == null) return null;

        spawnPosition = transform.position;
        spawnRotation = GetRandomSpawnRotation();

        // Rent through the pool owner, not through this spawner, so bags recycle across
        // every spawner in the scene instead of each one hoarding its own.
        LuggageSpawner pool = Instance != null ? Instance : this;
        Luggage luggage = pool.RentLuggage(prefab, spawnPosition, spawnRotation);
        if (luggage == null) return null;

        // Set before Initialize: Initialize refreshes the timer readout, which reads the flag.
        luggage.isTutorialLuggage = levelConfig == null;
        float lifetime = levelConfig != null ? levelConfig.luggageLifetime : 0f;
        luggage.Initialize(prefabLuggage.behaviorType, lifetime, prefab);
        AssignDestinationGateIfNeeded(luggage);
        return luggage;
    }

    private static Quaternion GetRandomSpawnRotation()
    {
        Quaternion uprightPrefabRotation = Quaternion.Euler(90f, 0f, 90f);
        float randomYaw = Random.Range(0, 4) * 90f;
        return Quaternion.AngleAxis(randomYaw, Vector3.up) * uprightPrefabRotation;
    }

    private void RefreshActiveDeliveryGates()
    {
        activeDeliveryGates.Clear();
        activeDeliveryGates.AddRange(FindObjectsByType<Gate>(FindObjectsSortMode.None));
        activeDeliveryGates.Sort((a, b) => a.GateNumber.CompareTo(b.GateNumber));

        if (activeDeliveryGates.Count <= 1)
            return;

        for (int i = 1; i < activeDeliveryGates.Count; i++)
        {
            if (activeDeliveryGates[i - 1].GateNumber == activeDeliveryGates[i].GateNumber)
                Debug.LogWarning($"Multiple delivery gates use gate number {activeDeliveryGates[i].GateNumber}. Give each delivery gate a unique number.");
        }
    }

    private void AssignDestinationGateIfNeeded(Luggage luggage)
    {
        if (activeDeliveryGates.Count <= 1)
            return;

        Gate destinationGate = activeDeliveryGates[Random.Range(0, activeDeliveryGates.Count)];
        luggage.AssignDestinationGate(destinationGate.GateNumber);
    }

    public Luggage RentLuggage(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        GameObject luggageObject = null;
        if (poolDictionary.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            while (queue.Count > 0 && luggageObject == null)
                luggageObject = queue.Dequeue();
        }

        if (luggageObject == null)
            luggageObject = Instantiate(prefab);

        luggageObject.transform.SetParent(null, worldPositionStays: false);
        luggageObject.transform.SetPositionAndRotation(position, rotation);
        luggageObject.transform.localScale = prefab.transform.localScale;
        luggageObject.SetActive(true);

        Luggage luggage = luggageObject.GetComponent<Luggage>();
        if (luggage != null)
            return luggage;

        Debug.LogError($"Pooled prefab '{prefab.name}' is missing {nameof(Luggage)}.", prefab);
        Destroy(luggageObject);
        return null;
    }

    public bool ReturnLuggage(Luggage luggage)
    {
        if (luggage == null || luggage.sourcePrefab == null)
            return false;

        luggage.DropAllGrabbers();

        Rigidbody rb = luggage.Body != null ? luggage.Body : luggage.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        GameObject key = luggage.sourcePrefab;
        if (!poolDictionary.TryGetValue(key, out Queue<GameObject> queue))
            poolDictionary[key] = queue = new Queue<GameObject>();

        luggage.gameObject.SetActive(false);
        luggage.transform.SetParent(PoolRoot, worldPositionStays: false);
        queue.Enqueue(luggage.gameObject);
        return true;
    }

    public bool TryGetConfiguredLifetime(out float lifetime)
    {
        ResolveLevelConfig();
        lifetime = levelConfig != null ? levelConfig.luggageLifetime : 0f;
        return lifetime > 0f;
    }

    private void ResolveLevelConfig()
    {
        if (LevelContext.TryGetConfig(out LevelConfig sceneConfig))
            levelConfig = sceneConfig;
    }
}
