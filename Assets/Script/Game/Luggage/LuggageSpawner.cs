using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Feeds a belt. Reads LevelConfig and drops one bag every spawnInterval seconds using a
// per-prefab object pool. Exposes ReturnLuggage(Luggage) so delivered, rejected or sunk
// luggage can be recycled back into the pool instead of being destroyed.
//
// The belt runs flat — no waves — and each bag tops up whichever colour is scarcest among the
// bags still in play, so the palette a gate can ask for is exactly the set of prefabs listed in
// the pool and every colour stays on the floor. No dice: a flight is never unfillable because a
// colour did not spawn.
//
// Bags never expire. A gameplay belt drops them into a pile, so maxActiveLuggage is the pile's
// size: once it is reached the belt pauses and only restarts when a bag is delivered. The spawner
// never destroys a live bag to make room.
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

    // Cursor into the luggage pool that breaks ties between equally scarce colours.
    private int nextPrefabIndex;
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

        // Spawn first, wait after. The belt should already be carrying a bag when the countdown
        // clears instead of opening on an empty run — the wait is scaled time, so a leading
        // yield spends the whole countdown frozen and then costs another spawnInterval on "GO".
        while (true)
        {
            // The belt drops into a pile and nothing drains it but deliveries, so this cap is the
            // pile's size: at the cap the belt pauses instead of burying the arena. Nothing is
            // ever destroyed to make room: a bag on the floor stays there until a player deals
            // with it.
            PruneLive();
            if (live.Count < levelConfig.maxActiveLuggage)
            {
                Luggage spawned = SpawnOne(prefabs);
                if (spawned != null)
                    live.Add(spawned);
            }

            yield return wait;
        }
    }

    private void PruneLive()
    {
        live.RemoveAll(bag => bag == null || !bag.gameObject.activeInHierarchy);
    }

    // Menu/tutorial mode: no LevelConfig, so spawn forever on a plain interval.
    private IEnumerator FallbackLoop()
    {
        WaitForSeconds wait = new(fallbackSpawnInterval);

        while (true)
        {
            PruneLive();
            if (live.Count < fallbackMaxActive)
            {
                Luggage spawned = SpawnOne(fallbackLuggagePrefabs);
                if (spawned != null)
                    live.Add(spawned);
            }

            yield return wait;
        }
    }

    private Luggage SpawnOne(List<GameObject> prefabs)
    {
        GameObject prefab = PickScarcestPrefab(prefabs);
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

        luggage.isTutorialLuggage = levelConfig == null;
        luggage.Initialize(prefab);
        return luggage;
    }

    // Not a random draw, and not a plain round robin either. The next bag is whichever pool entry
    // has the fewest bags still in play; ties go in list order from the cursor, so a filling belt
    // still runs red, green, yellow, red, ...
    //
    // A plain round robin soft-locks a pile. At the cap a bag only spawns after a delivery, so the
    // pile drifts toward the colours nobody wants until it holds none of what the flight needs —
    // then nothing can be delivered and nothing spawns. Simulated over 40 deliveries, a strict
    // cycle locked 3% of rounds at a cap of 28 and 27% at 18; topping up the scarcest never did.
    private GameObject PickScarcestPrefab(List<GameObject> prefabs)
    {
        int bestIndex = nextPrefabIndex % prefabs.Count;
        int bestCount = int.MaxValue;
        for (int step = 0; step < prefabs.Count; step++)
        {
            int index = (nextPrefabIndex + step) % prefabs.Count;
            int count = CountLive(prefabs[index]);
            if (count < bestCount)
            {
                bestCount = count;
                bestIndex = index;
            }
        }

        nextPrefabIndex = (bestIndex + 1) % prefabs.Count;
        return prefabs[bestIndex];
    }

    private int CountLive(GameObject prefab)
    {
        int count = 0;
        for (int i = 0; i < live.Count; i++)
        {
            if (live[i] != null && live[i].sourcePrefab == prefab)
                count++;
        }

        return count;
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

    private void ResolveLevelConfig()
    {
        if (LevelContext.TryGetConfig(out LevelConfig sceneConfig))
            levelConfig = sceneConfig;
    }
}
