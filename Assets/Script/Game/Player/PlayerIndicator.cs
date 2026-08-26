using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
    [Tooltip("Gap between the top of the character's silhouette and the tip of the pin.")]
    [SerializeField, Min(0f)] private float hoverHeight = 0.45f;

    [Header("In-game auto-hide")]
    [Tooltip("How long the pin stays up after a gameplay scene starts before it shrinks " +
             "away. In the lobby the pin never hides.")]
    [SerializeField, Min(0f)] private float gameplayShowSeconds = 3f;
    [Tooltip("How long the shrink-away takes.")]
    [SerializeField, Min(0.01f)] private float shrinkDuration = 0.35f;

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
    private Vector3 baseScale;
    private float sceneStartTime;
    private SkinnedMeshRenderer[] bodyRenderers;

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

        baseScale = transform.localScale;
        sceneStartTime = Time.time;
        bodyRenderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        ApplyWorldUiLayer();
        ApplyPlayer();
        UpdatePlacementAndFacing();
    }

    // The player object (and this pin) persists across scene loads, so the show timer
    // restarts on every load rather than only once at Awake.
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => sceneStartTime = Time.time;

    private void LateUpdate()
    {
        // playerIndex is assigned as the player joins, which can land after Awake.
        if (player.playerIndex != lastIndex)
            ApplyPlayer();

        UpdatePlacementAndFacing();
        UpdateHide();
    }

    // Lobby pins stay up forever; in a gameplay scene the pin shrinks away shortly after
    // the round starts — by then everyone knows which body is theirs. Scaled time, so a
    // pause freezes the countdown with everything else.
    private void UpdateHide()
    {
        float t = GameManager.Instance == null
            ? 0f
            : Mathf.Clamp01((Time.time - sceneStartTime - gameplayShowSeconds) / shrinkDuration);
        transform.localScale = baseScale * (1f - (t * t * (3f - 2f * t)));
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
        transform.position = new Vector3(anchor.x, SilhouetteTop() + hoverHeight, anchor.z);

        // Re-resolve after a scene load: players persist, the camera does not.
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        // Align to the camera's plane instead of aiming at its position: aiming tilts pins
        // near the screen edge inward and the perspective skew reads as a stretched sprite.
        // Camera-plane alignment keeps every pin screen-parallel, so the art always shows
        // at its authored proportions. (+Z ends up pointing away from the viewer, which is
        // the orientation a world-space canvas needs to not render mirrored.)
        transform.rotation = targetCamera.transform.rotation;
    }

    // Top of the character's art, not of the capsule: every body shares one capsule so that
    // nobody is blocked by a doorway another walks through, which means the capsule stops at
    // the skull and Annie's hat and Bun Jovi's ears stick out above it. Hanging the pin off
    // the renderers keeps it clear of each silhouette with the same hoverHeight on every prefab.
    private float SilhouetteTop()
    {
        float top = float.NegativeInfinity;
        if (bodyRenderers != null)
        {
            foreach (SkinnedMeshRenderer r in bodyRenderers)
                if (r != null)
                    top = Mathf.Max(top, r.bounds.max.y);
        }

        if (!float.IsNegativeInfinity(top))
            return top;

        return sourceCollider != null
            ? sourceCollider.bounds.max.y
            : player.transform.position.y;
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
