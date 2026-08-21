using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// World-space "1P / 2P" pin floating above a player. The prefab owns all the art;
/// this component only tints the disc, writes the label, keeps the pin over the
/// character's head and turns it to face the camera.
///
/// Same pattern as the luggage timer: authored prefab, thin presenter, WorldUI layer
/// so the pin draws over level geometry through the overlay camera.
/// </summary>
[DisallowMultipleComponent]
public class PlayerIndicator : MonoBehaviour
{
    [SerializeField] private PlayerInput player;
    [SerializeField] private Collider sourceCollider;
    [SerializeField] private Image innerImage;  // the disc inside the pin, tinted per player
    [SerializeField] private TMP_Text label;    // "1P"

    [Header("Presentation")]
    [SerializeField] private string worldUiLayerName = WorldUIOverlayCamera.LayerName;
    [Tooltip("Gap between the top of the player's collider and the tip of the pin.")]
    [SerializeField, Min(0f)] private float hoverHeight = 0.45f;

    [Tooltip("Disc colour per player, in playerIndex order.")]
    [SerializeField]
    private Color[] playerColors =
    {
        new(0.180f, 0.435f, 0.851f), // 1P blue
        new(0.851f, 0.231f, 0.188f), // 2P red
        new(0.184f, 0.659f, 0.310f), // 3P green
        new(0.851f, 0.467f, 0.024f), // 4P orange
    };

    private Camera targetCamera;
    private int lastIndex = -1;

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<PlayerInput>();
        if (sourceCollider == null && player != null)
            sourceCollider = player.GetComponent<Collider>();

        if (player == null || innerImage == null || label == null)
        {
            Debug.LogError(
                $"{nameof(PlayerIndicator)} '{name}' is missing its prefab references.", this);
            enabled = false;
            return;
        }

        ApplyWorldUiLayer();
        ApplyPlayer();
        UpdatePlacementAndFacing();
    }

    private void LateUpdate()
    {
        // playerIndex is assigned as the player joins, which can land after Awake.
        if (player.playerIndex != lastIndex)
            ApplyPlayer();

        UpdatePlacementAndFacing();
    }

    private void ApplyPlayer()
    {
        lastIndex = player.playerIndex;
        label.text = $"{lastIndex + 1}P";

        if (playerColors != null && playerColors.Length > 0)
            innerImage.color = playerColors[Mathf.Clamp(lastIndex, 0, playerColors.Length - 1)];
    }

    private void UpdatePlacementAndFacing()
    {
        Vector3 anchor = sourceCollider != null
            ? sourceCollider.bounds.center
            : player.transform.position;
        float top = sourceCollider != null
            ? sourceCollider.bounds.max.y
            : player.transform.position.y;
        transform.position = new Vector3(anchor.x, top + hoverHeight, anchor.z);

        // Re-resolve after a scene load: players persist, the camera does not.
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        // A world-space canvas reads correctly when its +Z points AWAY from the viewer, so
        // the look direction is self -> camera, not camera -> self. Aiming +Z at the camera
        // shows the back of the canvas and the label comes out mirrored.
        Vector3 direction = transform.position - targetCamera.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction, targetCamera.transform.up);
    }

    private void ApplyWorldUiLayer()
    {
        int layer = LayerMask.NameToLayer(worldUiLayerName);
        if (layer < 0)
        {
            Debug.LogWarning(
                $"{nameof(PlayerIndicator)} could not find layer '{worldUiLayerName}'.", this);
            return;
        }

        SetLayerRecursively(transform, layer);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }
}
