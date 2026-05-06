using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LuggageSpawner : MonoBehaviour
{
    public static LuggageSpawner Instance { get; private set; }

    [Header("Level Config")]
    [SerializeField] private LevelConfig levelConfig;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
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
                SpawnOne(prefabs, levelConfig.possibleBehaviors, forceBomb: i == bombIndex);
                if (i < wavePerWave - 1)
                    yield return new WaitForSeconds(levelConfig.intraWaveInterval);
            }
        }
    }

    private void SpawnOne(List<GameObject> prefabs, List<LuggageBehaviorType> behaviors, bool forceBomb)
    {
        GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
        if (prefab == null) return;

        LuggageBehaviorType behavior;
        if (forceBomb)
            behavior = LuggageBehaviorType.Bomb;
        else if (behaviors != null && behaviors.Count > 0)
            behavior = behaviors[Random.Range(0, behaviors.Count)];
        else
            behavior = LuggageBehaviorType.Normal;

        spawnPosition = transform.position;
        spawnRotation = Quaternion.Euler(0, 0, 90);

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

        luggage.ActiveConveyor = null;
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
