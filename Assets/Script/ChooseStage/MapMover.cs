using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage-select token controlled through the shared InputActionAsset. It presents
/// LevelConfig nodes and delegates persistence/loading to their services.
/// </summary>
public class MapMover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 11f;
    [SerializeField] private float detectRadius = 1.6f;

    [Header("Refs")]
    [SerializeField] private LevelInfoPopup popup;
    [SerializeField] private InputActionAsset inputActions;

    private readonly List<GameObject> hiddenPlayers = new();
    private readonly Collider[] nodeHits = new Collider[16];
    private LevelNode currentNode;
    private InputAction moveAction;
    private InputAction confirmAction;
    private InputAction backAction;
    private bool loading;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError($"{nameof(MapMover)} needs the shared input action asset.");
            enabled = false;
            return;
        }

        moveAction = inputActions.FindAction("Map/Move", throwIfNotFound: false);
        confirmAction = inputActions.FindAction("Map/Confirm", throwIfNotFound: false);
        backAction = inputActions.FindAction("Map/Back", throwIfNotFound: false);
        if (moveAction == null || confirmAction == null || backAction == null)
        {
            Debug.LogError("GameInput is missing one or more Map actions.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        confirmAction?.Enable();
        backAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        confirmAction?.Disable();
        backAction?.Disable();
    }

    private void Start()
    {
        PlayerInput[] players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        foreach (PlayerInput player in players)
        {
            player.gameObject.SetActive(false);
            hiddenPlayers.Add(player.gameObject);
        }
    }

    private void Update()
    {
        if (loading || moveAction == null || confirmAction == null)
            return;

        Move();
        DetectNode();
        TryEnterLevel();

        if (!loading && backAction.WasPressedThisFrame())
            LeaveMap();
    }

    private void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Vector3 direction = new(input.x, 0f, input.y);
        if (direction.sqrMagnitude < 0.01f)
            return;

        transform.position += direction * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void DetectNode()
    {
        LevelNode found = null;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectRadius, nodeHits);
        for (int i = 0; i < hitCount; i++)
        {
            found = nodeHits[i] != null ? nodeHits[i].GetComponentInParent<LevelNode>() : null;
            nodeHits[i] = null;
            if (found != null)
                break;
        }

        if (found == currentNode)
            return;

        currentNode = found;
        if (popup == null)
            return;

        if (currentNode != null)
            popup.Show(currentNode);
        else
            popup.Hide();
    }

    private void TryEnterLevel()
    {
        if (currentNode == null || !confirmAction.WasPressedThisFrame())
            return;

        if (!currentNode.IsUnlocked)
        {
            AudioManager.Instance?.PlaySFX(Sfx.Wrong);
            return;
        }

        LevelConfig level = currentNode.Level;
        if (level == null || string.IsNullOrWhiteSpace(level.sceneName))
        {
            Debug.LogError($"Level node '{currentNode.name}' has no loadable level configuration.");
            return;
        }

        if (!SceneLoader.Load(level.sceneName))
            return;

        loading = true;
        foreach (GameObject player in hiddenPlayers)
            if (player != null)
                player.SetActive(true);
    }

    private void LeaveMap()
    {
        if (!SceneLoader.LoadLobby())
            return;

        loading = true;
        foreach (GameObject player in hiddenPlayers)
            if (player != null)
                player.SetActive(true);
    }
}
