using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Decorative traffic crossing the view: a vehicle appears at one side, travels to the other,
// then is pooled for reuse. Pure set dressing — it owns no round rules and needs no
// GameManager, so any scene can drop one in.
//
// Each group is one kind of traffic (planes, cars, whatever comes next) with its own fleet,
// endpoints and timing. Endpoints are split into two sides and a trip always crosses: a vehicle
// starting on side one finishes on side two and vice versa, never within a side. Which point on
// each side is picked is random, so the same two lists give many routes.
public class AmbientTrafficSpawner : MonoBehaviour
{
    // A prefab plus which way its nose is authored. Facing is per-model, not per-group: two
    // planes from the same pack can disagree, so it cannot live on the group.
    [System.Serializable]
    public class Vehicle
    {
        public GameObject prefab;

        [Tooltip("Degrees of yaw needed to bring this model's nose onto +Z, which is the axis " +
            "travel direction is applied to. Nose already +Z: 0. Nose +X: 270. Nose -X: 90. " +
            "Nose -Z: 180.")]
        public float yawOffset;
    }

    [System.Serializable]
    public class TrafficGroup
    {
        [Tooltip("Inspector label only — has no effect on behaviour.")]
        public string name = "Traffic";

        [Header("Fleet")]
        [Tooltip("One is picked at random per trip. Leave empty and this group never spawns.")]
        public Vehicle[] fleet;

        [Header("Route")]
        [Tooltip("Endpoints on one side of the view.")]
        public Transform[] sideOne;
        [Tooltip("Endpoints on the other side. A trip always runs from one side to the other.")]
        public Transform[] sideTwo;
        [Tooltip("Point the vehicle down its path. Off keeps the start anchor's rotation and " +
            "ignores yawOffset entirely.")]
        public bool faceTravelDirection = true;

        [Header("Timing")]
        [Min(0f), Tooltip("Quiet period before this group's first trip.")]
        public float initialDelay = 2f;
        [Tooltip("Random cooldown between trips, in seconds.")]
        public Vector2 spawnDelayRange = new(8f, 20f);
        [Tooltip("Random world units per second for each trip.")]
        public Vector2 speedRange = new(10f, 40f);
        [Min(1), Tooltip("How many of this group may be travelling at once.")]
        public int maxConcurrent = 2;

        [Header("Pass Sound")]
        [Tooltip("AudioManager row heard as one of this group passes the camera. Empty = silent.")]
        public string passSound;
        [Min(0f), Tooltip("Seconds into the clip where it is loudest. The clip is started so this " +
            "moment lands as the vehicle passes closest to the camera.")]
        public float passSoundPeak = 5.9f;
        [Min(1f), Tooltip("A pass that never comes closer to the camera than this is not heard; " +
            "the closer a pass comes, the louder it is.")]
        public float passSoundRange = 600f;

        [System.NonSerialized] public int travelling;
    }

    // A pass sound eases in when it has to start partway through its clip, and swings this far
    // across the stereo field as the vehicle crosses the view.
    private const float PassFadeIn = 0.3f;
    private const float PassPan = 0.7f;
    // Silent this long before the clip's own end, which is cut off, not faded.
    private const float PassTailCut = 0.2f;

    [SerializeField] private TrafficGroup[] groups;

    // One idle stack per prefab, so a bus is never reused as a plane.
    private readonly Dictionary<GameObject, Stack<GameObject>> idle = new();
    private readonly List<AudioSource> passSounds = new();

    private void Start()
    {
        if (groups == null) return;

        foreach (TrafficGroup group in groups)
            StartCoroutine(Run(group));
    }

    private void OnDisable()
    {
        if (groups == null) return;

        foreach (TrafficGroup group in groups)
            group.travelling = 0;

        // The timing coroutines die with the scene, but the sound would play on to its cut-off
        // end at whatever volume it had, so fade it instead.
        if (AudioManager.Instance != null)
            foreach (AudioSource source in passSounds)
                AudioManager.Instance.StopSFX(source, 0.5f);
        passSounds.Clear();
    }

    private IEnumerator Run(TrafficGroup group)
    {
        // An empty fleet is a valid way to switch a group off, so it stays silent.
        if (PickVehicle(group.fleet) == null)
            yield break;

        // Missing endpoints with a filled fleet is a wiring mistake, so it is not silent.
        if (!HasAny(group.sideOne) || !HasAny(group.sideTwo))
        {
            Debug.LogWarning($"{nameof(AmbientTrafficSpawner)} '{name}': group '{group.name}' has a fleet " +
                "but needs at least one endpoint on each side, so nothing will spawn.", this);
            yield break;
        }

        yield return new WaitForSeconds(group.initialDelay);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(group.spawnDelayRange.x, group.spawnDelayRange.y));

            if (group.travelling < group.maxConcurrent)
                StartCoroutine(Travel(group));
        }
    }

    private IEnumerator Travel(TrafficGroup group)
    {
        // Pick the starting side at random so traffic runs both ways, then take the far side
        // for the destination — that is the whole cross-side rule.
        bool startOnSideOne = Random.value < 0.5f;
        Transform from = PickRandom(startOnSideOne ? group.sideOne : group.sideTwo);
        Transform to = PickRandom(startOnSideOne ? group.sideTwo : group.sideOne);
        Vehicle vehicle = PickVehicle(group.fleet);
        if (from == null || to == null || vehicle == null) yield break;

        GameObject instance = Take(vehicle.prefab);
        group.travelling++;

        // LookRotation puts the object's +Z down the path, so a model whose nose is authored
        // along any other axis needs its yaw folded in afterwards or it travels backwards.
        Vector3 heading = to.position - from.position;
        instance.transform.SetPositionAndRotation(
            from.position,
            group.faceTravelDirection && heading.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(heading.normalized, Vector3.up)
                  * Quaternion.Euler(0f, vehicle.yawOffset, 0f)
                : from.rotation);
        instance.SetActive(true);

        float speed = Random.Range(group.speedRange.x, group.speedRange.y);
        if (!string.IsNullOrEmpty(group.passSound))
            StartCoroutine(PassSound(group, instance.transform, from.position, to.position, speed));

        while (instance.transform.position != to.position)
        {
            instance.transform.position = Vector3.MoveTowards(
                instance.transform.position, to.position, speed * Time.deltaTime);
            yield return null;
        }

        Release(vehicle.prefab, instance);
        group.travelling--;
    }

    // Times one trip's sound to the trip. The clip's loudest moment is lined up with the moment
    // the vehicle passes closest to the camera, and from there it fades to nothing just before
    // the clip ends, so its cut-off last frame is never heard. The closer the pass, the louder;
    // a pass that stays beyond passSoundRange makes no sound at all.
    private IEnumerator PassSound(TrafficGroup group, Transform vehicle, Vector3 from, Vector3 to, float speed)
    {
        Camera cam = Camera.main;
        AudioManager audio = AudioManager.Instance;
        if (cam == null || audio == null || speed <= 0f)
            yield break;

        Vector3 ear = cam.transform.position;
        Vector3 path = to - from;
        float along = Mathf.Clamp(Vector3.Dot(ear - from, path.normalized), 0f, path.magnitude);
        float closest = Vector3.Distance(ear, from + path.normalized * along);
        if (closest >= group.passSoundRange)
            yield break;

        // Seconds from now until the peak should start; negative means start partway in.
        float lead = along / speed - group.passSoundPeak;
        if (lead > 0f)
            yield return new WaitForSeconds(lead);

        AudioSource source = audio.PlayDedicatedSFX(group.passSound, 0f);
        if (source == null)
            yield break;

        float end = source.clip.length - PassTailCut;
        float start = lead < 0f ? Mathf.Min(-lead, end) : 0f;
        source.time = start;
        float loudness = 1f - closest / group.passSoundRange;
        passSounds.Add(source);

        while (source != null && source.isPlaying)
        {
            float t = source.time;
            float easeIn = Mathf.Clamp01((t - start) / PassFadeIn);
            float tail = Mathf.Clamp01((end - t) / Mathf.Max(0.01f, end - group.passSoundPeak));
            audio.SetSFXVolume(source, loudness * easeIn * tail);

            if (vehicle.gameObject.activeInHierarchy)
                source.panStereo = PassPan * Vector3.Dot((vehicle.position - ear).normalized, cam.transform.right);

            yield return null;
        }

        passSounds.Remove(source);
    }

    // Empty inspector slots are skipped throughout, so a half-filled array still works.
    private static bool HasAny<T>(T[] items) where T : Object
    {
        if (items == null) return false;

        foreach (T item in items)
            if (item != null) return true;

        return false;
    }

    private static T PickRandom<T>(T[] items) where T : Object
    {
        int filled = 0;
        if (items != null)
            foreach (T item in items)
                if (item != null) filled++;

        if (filled == 0) return null;

        int pick = Random.Range(0, filled);
        foreach (T item in items)
        {
            if (item == null) continue;
            if (pick-- == 0) return item;
        }

        return null;
    }

    // A fleet entry only counts once it actually has a prefab.
    private static Vehicle PickVehicle(Vehicle[] fleet)
    {
        int filled = 0;
        if (fleet != null)
            foreach (Vehicle entry in fleet)
                if (entry != null && entry.prefab != null) filled++;

        if (filled == 0) return null;

        int pick = Random.Range(0, filled);
        foreach (Vehicle entry in fleet)
        {
            if (entry == null || entry.prefab == null) continue;
            if (pick-- == 0) return entry;
        }

        return null;
    }

    private GameObject Take(GameObject prefab)
    {
        if (idle.TryGetValue(prefab, out Stack<GameObject> stack) && stack.Count > 0)
            return stack.Pop();

        return Instantiate(prefab, transform);
    }

    private void Release(GameObject prefab, GameObject instance)
    {
        instance.SetActive(false);

        if (!idle.TryGetValue(prefab, out Stack<GameObject> stack))
            idle[prefab] = stack = new Stack<GameObject>();

        stack.Push(instance);
    }
}
