using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Moving sky for a fixed camera. Children of this object are the clouds already in the sky when
// the scene opens; cloudPrefabs keep arriving from upwind for as long as the scene runs. A cloud
// is a prefab whose direct children are its puffs.
//
// Every cloud rides the wind at its own speed, so clouds overtake each other, and near clouds
// cross the screen faster than far ones — that difference is what makes the layer read as deep.
//
// A cloud is only ever created or destroyed where nothing can see it. New clouds are pushed
// upwind until they clear the view, and a cloud is removed only once it is downwind and out of
// both the camera's authored (home) view and wherever the camera is looking right now — so the
// CameraFocus lean can never catch a cloud popping in or out.
//
// Each puff also swells and settles on its own slow sine, so outlines keep billowing.
//
// Pure set dressing: no physics, no dependencies beyond the camera.
public class CloudDrift : MonoBehaviour
{
    // One cloud in the sky, with everything its billow needs.
    private class Cloud
    {
        public Transform Root;
        public float Speed;
        public Renderer[] Renderers;
        public Transform[] Puffs;
        public Vector3[] RestScale;
        public float[] Phase;
        public float[] Rate;
    }

    [Header("Wind")]
    [SerializeField, Tooltip("Camera the sky is framed for. Empty = Camera.main. Its pose at Awake is the home view.")]
    private Camera viewer;
    [SerializeField, Tooltip("Average world units per second across the view. Positive blows left-to-right, negative right-to-left.")]
    private float windSpeed = -3f;
    [SerializeField, Range(0f, 0.9f), Tooltip("Each cloud moves at windSpeed × (1 ± this), picked once per cloud.")]
    private float speedVariation = 0.4f;

    [Header("Spawning")]
    [SerializeField, Tooltip("One is picked at random per spawn.")]
    private GameObject[] cloudPrefabs;
    [SerializeField, Tooltip("Random seconds between spawns.")]
    private Vector2 spawnInterval = new(15f, 35f);
    [SerializeField, Min(1), Tooltip("No new cloud while this many are in the sky, counting the ones placed in the scene.")]
    private int maxClouds = 12;
    [SerializeField, Tooltip("How far ahead of the camera a new cloud sits. Far and low clouds hide behind the terminal on the left half of the MainMenu view, so keep this near. Must stay inside the camera's far clip.")]
    private Vector2 depthRange = new(320f, 700f);
    [SerializeField, Tooltip("World height of a new cloud. One narrow band reads as one cloud deck.")]
    private Vector2 heightRange = new(110f, 140f);
    [SerializeField, Tooltip("Random size multiplier for each new cloud.")]
    private Vector2 scaleRange = new(0.85f, 1.15f);

    [Header("Billow")]
    [SerializeField, Range(0f, 0.2f), Tooltip("How far each puff swells and settles, as a fraction of its size.")]
    private float billow = 0.06f;
    [SerializeField, Min(0.01f), Tooltip("Swells per second. Slow reads as billowing, fast as boiling.")]
    private float billowSpeed = 0.07f;

    private readonly List<Cloud> clouds = new();
    private readonly Plane[] homePlanes = new Plane[6];
    private readonly Plane[] livePlanes = new Plane[6];

    private Vector3 origin;
    private Vector3 forward;
    private Vector3 right;
    private Matrix4x4 homeWorldToCamera;
    private int puffCount;

    private void Awake()
    {
        if (viewer == null)
            viewer = Camera.main;

        if (viewer == null)
        {
            Debug.LogError($"{nameof(CloudDrift)} '{name}' needs a viewer camera.", this);
            enabled = false;
            return;
        }

        Transform view = viewer.transform;
        origin = view.position;
        forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        right = Vector3.Cross(Vector3.up, forward);
        homeWorldToCamera = viewer.worldToCameraMatrix;

        foreach (Transform child in transform)
            Register(child);
    }

    private IEnumerator Start()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(spawnInterval.x, spawnInterval.y));

            if (clouds.Count < maxClouds)
                Spawn();
        }
    }

    private void Update()
    {
        RefreshViews();

        float t = Time.time * billowSpeed * Mathf.PI * 2f;
        for (int i = clouds.Count - 1; i >= 0; i--)
        {
            Cloud cloud = clouds[i];
            if (cloud.Root == null)
            {
                clouds.RemoveAt(i);
                continue;
            }

            cloud.Root.position += right * (cloud.Speed * Time.deltaTime);

            if (IsDownwind(cloud) && !IsSeen(cloud))
            {
                Destroy(cloud.Root.gameObject);
                clouds.RemoveAt(i);
                continue;
            }

            for (int p = 0; p < cloud.Puffs.Length; p++)
                cloud.Puffs[p].localScale =
                    cloud.RestScale[p] * (1f + billow * Mathf.Sin(t * cloud.Rate[p] + cloud.Phase[p]));
        }
    }

    private Cloud Register(Transform root)
    {
        int count = root.childCount;
        var cloud = new Cloud
        {
            Root = root,
            Speed = windSpeed * (1f + Random.Range(-speedVariation, speedVariation)),
            Renderers = root.GetComponentsInChildren<Renderer>(),
            Puffs = new Transform[count],
            RestScale = new Vector3[count],
            Phase = new float[count],
            Rate = new float[count],
        };

        for (int p = 0; p < count; p++)
        {
            Transform puff = root.GetChild(p);
            cloud.Puffs[p] = puff;
            cloud.RestScale[p] = puff.localScale;
            // Golden-angle phases never line up, and the rate spread keeps any two puffs from
            // settling into lockstep over time.
            cloud.Phase[p] = puffCount * 2.39996f;
            cloud.Rate[p] = 1f + 0.35f * Mathf.Sin(puffCount * 1.7f);
            puffCount++;
        }

        clouds.Add(cloud);
        return cloud;
    }

    private void Spawn()
    {
        GameObject prefab = PickPrefab();
        if (prefab == null) return;

        GameObject spawned = Instantiate(prefab, transform);
        spawned.transform.localScale = prefab.transform.localScale * Random.Range(scaleRange.x, scaleRange.y);
        Cloud cloud = Register(spawned.transform);

        float depth = Random.Range(depthRange.x, depthRange.y);
        float height = Random.Range(heightRange.x, heightRange.y);

        // Start inside the view and back off upwind until neither view can see any of it.
        RefreshViews();
        float upwind = cloud.Speed >= 0f ? -1f : 1f;
        for (float angle = 20f; angle < 85f; angle += 2f)
        {
            Place(spawned.transform, depth, height, upwind * angle);
            if (!IsSeen(cloud)) break;
        }
    }

    private GameObject PickPrefab()
    {
        if (cloudPrefabs == null || cloudPrefabs.Length == 0) return null;

        // Skip empty slots so a half-filled array in the inspector still works.
        int start = Random.Range(0, cloudPrefabs.Length);
        for (int i = 0; i < cloudPrefabs.Length; i++)
        {
            GameObject prefab = cloudPrefabs[(start + i) % cloudPrefabs.Length];
            if (prefab != null) return prefab;
        }

        return null;
    }

    // Puts a cloud at a depth ahead of the home view and an angle off its forward, turned so the
    // side of the cloud it was authored from faces the camera.
    private void Place(Transform cloud, float depth, float height, float angle)
    {
        Vector3 flat = forward * depth + right * (depth * Mathf.Tan(angle * Mathf.Deg2Rad));
        cloud.SetPositionAndRotation(
            new Vector3(origin.x, height, origin.z) + flat,
            Quaternion.LookRotation(flat));
    }

    private void RefreshViews()
    {
        // Rebuilt every frame so a resized Game view (new aspect) is always respected.
        GeometryUtility.CalculateFrustumPlanes(viewer.projectionMatrix * homeWorldToCamera, homePlanes);
        GeometryUtility.CalculateFrustumPlanes(viewer, livePlanes);
    }

    private bool IsDownwind(Cloud cloud)
        => Vector3.Dot(cloud.Root.position - origin, right) * cloud.Speed > 0f;

    private bool IsSeen(Cloud cloud)
    {
        if (cloud.Renderers.Length == 0) return false;

        Bounds bounds = cloud.Renderers[0].bounds;
        for (int i = 1; i < cloud.Renderers.Length; i++)
            bounds.Encapsulate(cloud.Renderers[i].bounds);

        // Headroom for a puff at the top of its billow.
        bounds.Expand(bounds.size * (2f * billow));

        return GeometryUtility.TestPlanesAABB(homePlanes, bounds)
            || GeometryUtility.TestPlanesAABB(livePlanes, bounds);
    }
}
