using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Singleton that runs the wave loop. Reads LevelConfig and spawns luggage at intervals
// using a per-prefab object pool. Exposes ReturnLuggage(Luggage) so destroyed, expired,
// or sunk luggage can be recycled back into the pool instead of being destroyed.
public class LuggageSpawner : MonoBehaviour
{
    public static LuggageSpawner Instance { get; private set; }

    [Header("Level Config")]
    [SerializeField] private LevelConfig levelConfig;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly List<Gate> activeDeliveryGates = new List<Gate>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (levelConfig == null && GameManager.Instance != null)
            levelConfig = GameManager.Instance.GetLevelConfig();

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
            bool bombWave = Random.value < levelConfig.bombWaveChance;
            int bombIndex = bombWave ? Random.Range(0, wavePerWave) : -1;

            for (int i = 0; i < wavePerWave; i++)
            {
                SpawnOne(prefabs, forceBomb: i == bombIndex);
                if (i < wavePerWave - 1)
                    yield return new WaitForSeconds(levelConfig.intraWaveInterval);
            }
        }
    }

    private void SpawnOne(List<GameObject> prefabs, bool forceBomb)
    {
        GameObject prefab = forceBomb ? FindBombVisualPrefab(prefabs) : prefabs[Random.Range(0, prefabs.Count)];
        if (prefab == null) return;

        Luggage prefabLuggage = prefab.GetComponent<Luggage>();
        if (prefabLuggage == null) return;

        LuggageBehaviorType behavior = forceBomb
            ? LuggageBehaviorType.Bomb
            : prefabLuggage.behaviorType;

        spawnPosition = transform.position;
        spawnRotation = GetRandomSpawnRotation();

        GameObject spawnedLuggage;

        if (poolDictionary.TryGetValue(prefab, out Queue<GameObject> queue) && queue.Count > 0)
        {
            spawnedLuggage = queue.Dequeue();
            spawnedLuggage.transform.position = spawnPosition;
            spawnedLuggage.transform.rotation = spawnRotation;
            spawnedLuggage.SetActive(true);
        }
        else
        {
            spawnedLuggage = Instantiate(prefab, spawnPosition, spawnRotation);
        }

        Luggage luggage = spawnedLuggage.GetComponent<Luggage>();
        if (luggage == null) return;

        luggage.Initialize(behavior, levelConfig.luggageLifetime, prefab);
        AssignDestinationGateIfNeeded(luggage);
    }

    private static GameObject FindBombVisualPrefab(List<GameObject> prefabs)
    {
        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;

            Luggage luggage = prefab.GetComponent<Luggage>();
            if (luggage != null && luggage.behaviorType == LuggageBehaviorType.Normal)
                return prefab;
        }

        return prefabs.Count > 0 ? prefabs[0] : null;
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

    public static void ReturnLuggage(Luggage luggage)
    {
        if (Instance == null)
        {
            Destroy(luggage.gameObject);
            return;
        }

        luggage.DropAllGrabbers();

        Rigidbody rb = luggage.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        luggage.gameObject.SetActive(false);

        GameObject key = luggage.sourcePrefab;
        if (key == null)
        {
            Destroy(luggage.gameObject);
            return;
        }

        if (!Instance.poolDictionary.ContainsKey(key))
        {
            Instance.poolDictionary[key] = new Queue<GameObject>();
        }

        Instance.poolDictionary[key].Enqueue(luggage.gameObject);
    }
}
