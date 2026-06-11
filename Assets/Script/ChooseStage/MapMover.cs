using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// ChooseStage airplane token. Overcooked-style: snappy WASD / left-stick movement that
// faces the travel direction instantly (no easing). When it parks on a LevelNode the
// info card pops up; confirm loads that node's scene. Any joined device can drive it.
// Persisted players are hidden while on the map and re-activated before entering a stage.
public class MapMover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 11f;
    [SerializeField] private float detectRadius = 1.6f;

    [Header("Refs")]
    [SerializeField] private LevelInfoPopup popup;

    private LevelNode currentNode;
    private readonly List<GameObject> hiddenPlayers = new();
    private bool loading;

    private InputAction moveAction;
    private InputAction confirmAction;

    private void Awake()
    {
        moveAction = new InputAction("MapMove", InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Gamepad>/dpad/up").With("Down", "<Gamepad>/dpad/down")
            .With("Left", "<Gamepad>/dpad/left").With("Right", "<Gamepad>/dpad/right");
        moveAction.AddBinding("<Gamepad>/leftStick");

        confirmAction = new InputAction("MapConfirm", InputActionType.Button);
        confirmAction.AddBinding("<Keyboard>/enter");
        confirmAction.AddBinding("<Keyboard>/space");
        confirmAction.AddBinding("<Gamepad>/buttonSouth");
        confirmAction.AddBinding("<Gamepad>/start");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        confirmAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        confirmAction.Disable();
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
        confirmAction?.Dispose();
    }

    private void Start()
    {
        // Hide all player GameObjects while picking a stage.
        PlayerInput[] players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            p.gameObject.SetActive(false);
            hiddenPlayers.Add(p.gameObject);
        }
    }

    private void Update()
    {
        if (loading) return;
        Move();
        DetectNode();
        TryEnterLevel();
    }

    private void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude > 1f) input.Normalize();

        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        if (moveDir.sqrMagnitude < 0.01f) return;

        // Snappy: move at constant speed and snap to face the travel direction instantly.
        transform.position += moveDir * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
    }

    private void DetectNode()
    {
        LevelNode found = null;
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius);
        foreach (var hit in hits)
        {
            LevelNode node = hit.GetComponentInParent<LevelNode>();
            if (node != null) { found = node; break; }
        }

        if (found == currentNode) return;
        currentNode = found;

        if (popup != null)
        {
            if (currentNode != null) popup.Show(currentNode);
            else popup.Hide();
        }
    }

    private void TryEnterLevel()
    {
        if (currentNode == null || string.IsNullOrWhiteSpace(currentNode.sceneName)) return;
        if (!confirmAction.WasPressedThisFrame()) return;

        loading = true;

        // Re-activate persisted players before entering the stage.
        foreach (var p in hiddenPlayers)
            if (p != null) p.SetActive(true);

        SceneManager.LoadScene(currentNode.sceneName);
    }
}
