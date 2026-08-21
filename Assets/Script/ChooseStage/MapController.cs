using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage select. One plane per joined player flies over a flat 2D map; the level ticket
/// only appears once <b>every</b> plane is parked on the same node, and Confirm then loads
/// that node's level.
///
/// Players persist across scenes but are deactivated while the map is open, so their own
/// PlayerInput components cannot drive anything here. Instead each plane gets a runtime
/// clone of the shared input asset, masked to that player's control scheme and paired to
/// that player's devices — which is what lets two halves of one keyboard fly two planes.
/// Confirm and Back stay on the shared asset so any device can press them.
/// </summary>
public class MapController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private MapPlane planePrefab;
    [Tooltip("Where plane 1 starts. Extra planes fan out along +X from here.")]
    [SerializeField] private Transform planeSpawnPoint;
    [SerializeField] private LevelTicket ticket;
    [SerializeField] private MultiplayerCamera mapCamera;

    [Header("Map")]
    [Tooltip("Centre of the playable area. Planes are clamped to centre +/- half extents.")]
    [SerializeField] private Vector3 mapCenter = Vector3.zero;
    [SerializeField] private Vector2 mapHalfExtents = new(60f, 42f);
    [Tooltip("How close a plane must be to a node to count as parked on it.")]
    [SerializeField, Min(0.1f)] private float nodeRadius = 6f;
    [Tooltip("Gap between planes when they spawn.")]
    [SerializeField, Min(0f)] private float planeSpacing = 6f;

    private readonly List<GameObject> hiddenPlayers = new();
    private readonly List<MapPlane> planes = new();
    private readonly List<InputActionAsset> clonedAssets = new();
    private readonly List<Transform> cameraTargets = new();
    private LevelNode[] nodes;

    private InputAction confirmAction;
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

        confirmAction = inputActions.FindAction("Map/Confirm", throwIfNotFound: false);
        backAction = inputActions.FindAction("Map/Back", throwIfNotFound: false);
        if (confirmAction == null || backAction == null)
        {
            Debug.LogError("GameInput is missing Map/Confirm or Map/Back.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        confirmAction?.Enable();
        backAction?.Enable();
    }

    private void OnDisable()
    {
        confirmAction?.Disable();
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

        ticket?.Hide();
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
            AddPlane(0, move, origin);
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
            AddPlane(index, clone.FindAction("Map/Move", throwIfNotFound: false), spot);
        }
    }

    private void AddPlane(int playerIndex, InputAction move, Vector3 position)
    {
        MapPlane plane = Instantiate(planePrefab, position, planePrefab.transform.rotation, transform);
        plane.name = $"Map Plane {playerIndex + 1}P";
        plane.Initialize(playerIndex, move, mapCenter, mapHalfExtents);
        planes.Add(plane);
        cameraTargets.Add(plane.transform);
    }

    private void Update()
    {
        if (loading)
            return;

        RefreshSharedNode();

        if (confirmAction.WasPressedThisFrame())
            TryEnterLevel();
        else if (backAction.WasPressedThisFrame())
            LeaveMap();
    }

    /// <summary>
    /// The ticket is a group decision: it shows only while every plane is on one node, so a
    /// single straggler hides it again.
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
        if (sharedNode != null)
            ticket?.Show(sharedNode);
        else
            ticket?.Hide();
    }

    private LevelNode NodeUnder(Vector3 position)
    {
        LevelNode best = null;
        float bestSqr = nodeRadius * nodeRadius;

        foreach (LevelNode node in nodes)
        {
            if (node == null)
                continue;

            Vector3 delta = node.transform.position - position;
            delta.y = 0f;   // the plane flies above the flat map, so height must not count
            float sqr = delta.sqrMagnitude;
            if (sqr > bestSqr)
                continue;

            bestSqr = sqr;
            best = node;
        }

        return best;
    }

    private void TryEnterLevel()
    {
        if (sharedNode == null)
            return;

        if (!sharedNode.IsUnlocked)
        {
            AudioManager.Instance?.PlaySFX(Sfx.Wrong);
            return;
        }

        LevelConfig level = sharedNode.Level;
        if (level == null || string.IsNullOrWhiteSpace(level.sceneName))
        {
            Debug.LogError($"Level node '{sharedNode.name}' has no loadable level configuration.", sharedNode);
            return;
        }

        if (!SceneLoader.Load(level.sceneName))
            return;

        RestorePlayers();
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
                Gizmos.DrawWireSphere(node.transform.position, nodeRadius);
    }
}
