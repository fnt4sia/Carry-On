using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Feeds a belt. Reads LevelConfig and drops one bag every spawnInterval seconds using a
// per-prefab object pool. Exposes ReturnLuggage(Luggage) so delivered, rejected or sunk
// luggage can be recycled back into the pool instead of being destroyed.
//
// The belt runs flat — no waves. Which colour comes out is a straight random pick from the
// level's luggage pool, so the palette a gate can ask for is simply the set of prefabs listed
// there. Bags no longer expire, so maxActiveLuggage is what stops an ignored belt from filling
// the arena; the spawner idles while the cap is reached rather than queueing a backlog.
//
// A scene may hold as many spawners as the layout needs; drop in another Spawner prefab and it
// feeds its own belt. Each one runs the config's numbers independently, so two spawners double
// the real spawn rate and the cap. That makes a second spawner a difficulty change worth
// re-checking the star thresholds by hand for.
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

    // Bags this spawner has put out that are still in play, used only for the active cap.
    private readonly List<Luggage> live = new();
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

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        var prefabs = levelConfig.luggagePrefabs;
        if (prefabs == null || prefabs.Count == 0) yield break;

        WaitForSeconds wait = new(levelConfig.spawnInterval);

        while (true)
        {
            yield return wait;

            // Nothing expires any more, so an unattended belt would otherwise pile bags up
            // until the physics gives out.
            PruneLive();
            if (live.Count >= levelConfig.maxActiveLuggage && !RecycleOldest())
                continue;

            Luggage spawned = SpawnOne(prefabs);
            if (spawned != null)
                live.Add(spawned);
        }
    }

    private void PruneLive()
    {
        live.RemoveAll(bag => bag == null || !bag.gameObject.activeInHierarchy);
    }

    // The belt is a closed loop, so a bag only leaves it by being delivered. Left alone that
    // deadlocks: once the cap is reached with, say, no yellow bag riding, a flight that wants
    // yellow can never be filled because nothing new can spawn. Retiring the oldest bag keeps
    // the supply turning over so every colour comes round again.
    //
    // Anything a player is holding or a station is working on is skipped — it is in use.
    private bool RecycleOldest()
    {
        for (int i = 0; i < live.Count; i++)
        {
            Luggage bag = live[i];
            if (bag == null || bag.GetIsGrabbed() || bag.IsInStation)
                continue;

            live.RemoveAt(i);
            bag.DestroyLuggage();
            return true;
        }

        return false;
    }

    // Menu/tutorial mode: no LevelConfig, so spawn forever on a plain interval.
    private IEnumerator FallbackLoop()
    {
        WaitForSeconds wait = new(fallbackSpawnInterval);

        while (true)
        {
            yield return wait;

            PruneLive();
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
        return luggage;
    }

    private static Quaternion GetRandomSpawnRotation()
    {
        Quaternion uprightPrefabRotation = Quaternion.Euler(90f, 0f, 90f);
        float randomYaw = Random.Range(0, 4) * 90f;
        return Quaternion.AngleAxis(randomYaw, Vector3.up) * uprightPrefabRotation;
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
