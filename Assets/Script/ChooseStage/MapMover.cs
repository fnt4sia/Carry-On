using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// ChooseStage map controller. Moves a token across the level-select map and loads the
// overlapped LevelNode's scene on confirm. The token is a shared cursor any joined
// device can drive: movement reads WASD / arrows / D-pad / left stick, confirm reads
// Enter / Space / gamepad South / Start. Hides persisted players while on the map and
// re-activates them before entering a stage.
public class MapMover : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float rotationSpeed = 720f;
    public float detectRadius = 2f;

    private LevelNode currentNode;
    private readonly List<GameObject> hiddenPlayers = new();
    private bool loading = false;

    private InputAction moveAction;
    private InputAction confirmAction;

    void Awake()
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

    void OnEnable()
    {
        moveAction.Enable();
        confirmAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        confirmAction.Disable();
    }

    void OnDestroy()
    {
        moveAction?.Dispose();
        confirmAction?.Dispose();
    }

    void Start()
    {
        // Hide all player GameObjects while in ChooseStage
        PlayerInput[] players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            p.gameObject.SetActive(false);
            hiddenPlayers.Add(p.gameObject);
        }
    }

    void Update()
    {
        if (loading) return;
        Move();
        DetectNode();
        TryEnterLevel();
    }

    void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Free movement — no car steering, same feel as PlayerMovement
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        // Rotate to face movement direction
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    void DetectNode()
    {
        currentNode = null;
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius);
        foreach (var hit in hits)
        {
            LevelNode node = hit.GetComponent<LevelNode>();
            if (node != null)
            {
                currentNode = node;
                break;
            }
        }
    }

    void TryEnterLevel()
    {
        if (currentNode == null) return;
        if (!confirmAction.WasPressedThisFrame()) return;

        loading = true;

        // Re-activate all players before entering the stage
        foreach (var p in hiddenPlayers)
            if (p != null) p.SetActive(true);

        if (!string.IsNullOrWhiteSpace(currentNode.sceneName))
            SceneManager.LoadScene(currentNode.sceneName);
        else
            SceneManager.LoadScene(currentNode.levelIndex);
    }
}
