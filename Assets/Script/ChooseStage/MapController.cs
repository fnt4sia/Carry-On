using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage select. One plane per joined player flies over the island map. Each node's badge
/// shows its best stars while empty and who is parked on it once anyone is. While
/// <b>every</b> plane waits on the same unlocked node its border fills; one plane leaving
/// drains it again. A full border commits the group: the planes lock, the boarding pass pops
/// up over the node as the transition, and the level loads a few seconds later. There is no
/// Confirm press — boarding together is the confirmation.
///
/// Players persist across scenes but are deactivated while the map is open, so their own
/// PlayerInput components cannot drive anything here. Instead each plane gets a runtime
/// clone of the shared input asset, masked to that player's control scheme and paired to
/// that player's devices — which is what lets two halves of one keyboard fly two planes.
/// Back stays on the shared asset so any device can press it.
/// </summary>
public class MapController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private MapPlane planePrefab;
    [Tooltip("Where plane 1 starts. Extra planes fan out along +X from here.")]
    [SerializeField] private Transform planeSpawnPoint;
    [SerializeField] private LevelTicket ticket;
    [SerializeField] private ArenaFollowCamera mapCamera;

    [Header("Map")]
    [Tooltip("Centre of the playable area. Planes are clamped to centre +/- half extents.")]
    [SerializeField] private Vector3 mapCenter = Vector3.zero;
    [SerializeField] private Vector2 mapHalfExtents = new(60f, 42f);
    [Tooltip("Gap between planes when they spawn.")]
    [SerializeField, Min(0f)] private float planeSpacing = 6f;
    [Tooltip("Colour of the one plane spawned when the map is opened straight from the editor " +
             "with nobody joined. Matches 1P's colour.")]
    [SerializeField] private Color soloPlaneColor = new(0.180f, 0.435f, 0.851f);

    [Header("Boarding")]
    [Tooltip("Seconds every plane must wait on one node before its level loads. A plane " +
             "leaving drains the border at the same rate.")]
    [SerializeField, Min(0.1f)] private float boardingSeconds = 2f;
    [Tooltip("Seconds the boarding pass stays up once the border is full, before the level loads. " +
             "The planes can't move meanwhile.")]
    [SerializeField, Min(0f)] private float ticketSeconds = 3f;

    private readonly List<GameObject> hiddenPlayers = new();
    private readonly List<MapPlane> planes = new();
    private readonly List<InputActionAsset> clonedAssets = new();
    private readonly List<Transform> cameraTargets = new();
    private LevelNode[] nodes;

    private InputAction backAction;
    private LevelNode sharedNode;
    private bool loading;

    private void Awake()
    {
        if (inputActions == null || planePrefab == null)
        {
            Debug.LogError($"{nameof(MapController)} needs the shared input asset and a plane prefab.", this);
            enabled = false;
            return;
        }

        backAction = inputActions.FindAction("Map/Back", throwIfNotFound: false);
        if (backAction == null)
        {
            Debug.LogError("GameInput is missing Map/Back.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        backAction?.Enable();
    }

    private void OnDisable()
    {
        backAction?.Disable();
    }

    private void OnDestroy()
    {
        // Runtime clones are assets, not scene objects — nothing else will collect them.
        foreach (InputActionAsset clone in clonedAssets)
            if (clone != null)
            {
                clone.Disable();
                Destroy(clone);
            }
        clonedAssets.Clear();
    }

    private void Start()
    {
        nodes = FindObjectsByType<LevelNode>(FindObjectsSortMode.None);
        if (nodes.Length == 0)
            Debug.LogWarning($"{nameof(MapController)} found no {nameof(LevelNode)} in the scene.", this);

        SpawnPlanes();

        if (mapCamera != null)
            mapCamera.SetTargets(cameraTargets);
    }

    private void SpawnPlanes()
    {
        PlayerInput[] players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) => a.playerIndex.CompareTo(b.playerIndex));

        Vector3 origin = planeSpawnPoint != null ? planeSpawnPoint.position : mapCenter;

        if (players.Length == 0)
        {
            // Opened the map directly in the editor. One plane on the unmasked shared asset
            // keeps the scene testable without going through the lobby.
            InputAction move = inputActions.FindAction("Map/Move", throwIfNotFound: false);
            inputActions.FindActionMap("Map")?.Enable();
            AddPlane(0, soloPlaneColor, move, origin);
            return;
        }

        for (int i = 0; i < players.Length; i++)
        {
            PlayerInput player = players[i];

            // Read the pairing before deactivating: a disabled PlayerInput stops driving
            // anything, but the devices and scheme it was using are what the clone needs.
            var devices = new InputDevice[player.devices.Count];
            for (int d = 0; d < devices.Length; d++)
                devices[d] = player.devices[d];
            string scheme = player.currentControlScheme;
            int index = player.playerIndex;
            PlayerIndicator pin = player.GetComponentInChildren<PlayerIndicator>(true);
            Color color = pin != null ? pin.CurrentColor : Color.white;

            player.gameObject.SetActive(false);
            hiddenPlayers.Add(player.gameObject);

            InputActionAsset clone = Instantiate(inputActions);
            if (!string.IsNullOrEmpty(scheme))
                clone.bindingMask = InputBinding.MaskByGroup(scheme);
            if (devices.Length > 0)
                clone.devices = devices;
            clone.FindActionMap("Map")?.Enable();
            clonedAssets.Add(clone);

            Vector3 spot = origin + Vector3.right * ((i - (players.Length - 1) * 0.5f) * planeSpacing);
            AddPlane(index, color, clone.FindAction("Map/Move", throwIfNotFound: false), spot);
        }
    }

    private void AddPlane(int playerIndex, Color color, InputAction move, Vector3 position)
    {
        MapPlane plane = Instantiate(planePrefab, position, planePrefab.transform.rotation, transform);
        plane.name = $"Map Plane {playerIndex + 1}P";
        plane.Initialize(playerIndex, color, move, mapCenter, mapHalfExtents);
        planes.Add(plane);
        cameraTargets.Add(plane.transform);
    }

    private void Update()
    {
        if (loading)
            return;

        RefreshSharedNode();

        foreach (LevelNode node in nodes)
        {
            if (node == null)
                continue;

            node.ShowOccupancy(planes);

            // Every node eases toward its own target, so the one the group just left
            // drains visibly instead of snapping back to empty.
            float target = node == sharedNode && CanBoard(node) ? 1f : 0f;
            node.SetBoardingProgress(Mathf.MoveTowards(
                node.BoardingProgress, target, Time.deltaTime / boardingSeconds));

            if (node.BoardingProgress >= 1f)
            {
                StartCoroutine(Board(node));
                return;
            }
        }

        if (backAction.WasPressedThisFrame())
            LeaveMap();
    }

    /// <summary>
    /// Boarding is a group decision: it runs only while every plane is on one node, so a
    /// single straggler stops it.
    /// </summary>
    private void RefreshSharedNode()
    {
        LevelNode agreed = null;
        bool allOnSameNode = planes.Count > 0;

        foreach (MapPlane plane in planes)
        {
            LevelNode node = NodeUnder(plane.transform.position);
            plane.CurrentNode = node;

            if (node == null)
                allOnSameNode = false;
            else if (agreed == null)
                agreed = node;
            else if (agreed != node)
                allOnSameNode = false;
        }

        LevelNode next = allOnSameNode ? agreed : null;
        if (next == sharedNode)
            return;

        sharedNode = next;

        // Only unlocked stages get here, so one that can't board is one whose scene can't load:
        // the border won't fill, so say so the moment the group arrives.
        if (sharedNode != null && !CanBoard(sharedNode))
        {
            AudioManager.Instance?.PlaySFX(Sfx.Wrong);
            Debug.LogError($"Level node '{sharedNode.name}' has no level in Build Settings to load.", sharedNode);
        }
    }

    /// <summary>
    /// Locked stages never fill. Neither does one whose scene isn't in Build Settings — an
    /// old save can still have an archived stage unlocked, and SceneLoader would refuse it.
    /// </summary>
    private static bool CanBoard(LevelNode node)
    {
        LevelConfig level = node.Level;
        return node.IsUnlocked
            && level != null
            && !string.IsNullOrWhiteSpace(level.sceneName)
            && Application.CanStreamedLevelBeLoaded(level.sceneName);
    }

    /// <summary>
    /// The unlocked node whose landing ring the position is inside. Locked stages are scenery:
    /// a plane flies straight over them — no badge, no ticket, no landing.
    /// </summary>
    private LevelNode NodeUnder(Vector3 position)
    {
        LevelNode best = null;
        float bestSqr = float.MaxValue;

        foreach (LevelNode node in nodes)
        {
            if (node == null || !node.IsUnlocked)
                continue;

            Vector3 delta = node.RingCenter - position;
            delta.y = 0f;   // the plane flies above the flat map, so height must not count
            float sqr = delta.sqrMagnitude;
            float radius = node.RingRadius;
            if (sqr > radius * radius || sqr > bestSqr)
                continue;

            bestSqr = sqr;
            best = node;
        }

        return best;
    }

    /// <summary>
    /// The border is full, so the group is committed: lock the planes, pop the boarding pass up
    /// over the node, and hold it for <see cref="ticketSeconds"/> as the transition into the
    /// level before loading it.
    /// </summary>
    private IEnumerator Board(LevelNode node)
    {
        loading = true;
        SetPlanesLocked(true);
        ticket?.Show(node);

        yield return new WaitForSeconds(ticketSeconds);

        // Only fails if a load is already running; hand the map back rather than hang.
        if (!SceneLoader.Load(node.Level.sceneName))
        {
            node.SetBoardingProgress(0f);
            ticket?.Hide();
            SetPlanesLocked(false);
            loading = false;
            yield break;
        }

        RestorePlayers();
    }

    private void SetPlanesLocked(bool locked)
    {
        foreach (MapPlane plane in planes)
            plane.Locked = locked;
    }

    private void LeaveMap()
    {
        if (!SceneLoader.LoadLobby())
            return;

        RestorePlayers();
    }

    private void RestorePlayers()
    {
        loading = true;
        foreach (GameObject player in hiddenPlayers)
            if (player != null)
                player.SetActive(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireCube(mapCenter, new Vector3(mapHalfExtents.x * 2f, 0.1f, mapHalfExtents.y * 2f));

        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
        foreach (LevelNode node in FindObjectsByType<LevelNode>(FindObjectsSortMode.None))
            if (node != null)
                Gizmos.DrawWireSphere(node.RingCenter, node.RingRadius);
    }
}
