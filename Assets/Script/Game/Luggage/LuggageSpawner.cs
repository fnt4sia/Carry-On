using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Singleton that runs the wave loop. Reads LevelConfig and spawns luggage at intervals
// using a per-prefab object pool. Exposes ReturnLuggage(Luggage) so destroyed, expired,
// or sunk luggage can be recycled back into the pool instead of being destroyed.
public class LuggageSpawner : SingletonBehaviour<LuggageSpawner>
{
    private LevelConfig levelConfig;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private readonly Dictionary<GameObject, Queue<GameObject>> poolDictionary = new();
    private readonly List<Gate> activeDeliveryGates = new List<Gate>();
    private Transform poolRoot;

    protected override void OnSingletonAwake()
    {
        GameObject poolObject = new("LuggagePool");
        poolObject.transform.SetParent(transform, false);
        poolRoot = poolObject.transform;
    }

    private void Start()
    {
        ResolveLevelConfig();

        if (levelConfig == null)
        {
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

    private void SpawnOne(List<GameObject> prefabs)
    {
        GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
        if (prefab == null) return;

        Luggage prefabLuggage = prefab.GetComponent<Luggage>();
        if (prefabLuggage == null) return;

        spawnPosition = transform.position;
        spawnRotation = GetRandomSpawnRotation();

        Luggage luggage = RentLuggage(prefab, spawnPosition, spawnRotation);
        if (luggage == null) return;

        luggage.Initialize(prefabLuggage.behaviorType, levelConfig.luggageLifetime, prefab);
        AssignDestinationGateIfNeeded(luggage);
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
        if (poolRoot != null)
            luggage.transform.SetParent(poolRoot, worldPositionStays: false);
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
