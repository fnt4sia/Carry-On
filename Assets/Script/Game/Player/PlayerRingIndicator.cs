using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The ring of segments on the floor under a player. The prefab owns the art; this
/// component only tints the segments to that player's colour, keeps the ring flat on
/// the ground under the body, and spins it so there is always something moving.
///
/// Sibling of <see cref="PlayerIndicator"/>: the pin over the head names the player and
/// shrinks away a few seconds into a round, this ring stays up for the whole game. It
/// reads its colour from the pin so the two can never disagree about who is 1P.
/// </summary>
[DisallowMultipleComponent]
public class PlayerRingIndicator : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Ring opacity. Low on purpose — this sits under the player all game and " +
             "must not compete with the luggage.")]
    [SerializeField, Range(0f, 1f)] private float alpha = 0.35f;
    [Tooltip("Height above the character's feet, enough to clear the floor without z-fighting.")]
    [SerializeField, Min(0f)] private float groundOffset = 0.02f;

    [Header("Spin")]
    [Tooltip("Degrees per second. Scaled time, so a pause freezes it with everything else.")]
    [SerializeField] private float spinSpeed = 45f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private PlayerIndicator pin;
    private Collider sourceCollider;
    private Renderer[] segments;
    private MaterialPropertyBlock block;
    private Color lastColor;
    private float spin;

    // Everything is resolved from the player this ring was nested under, so the same
    // prefab drops onto any of the three bodies with nothing to wire by hand.
    private void Awake()
    {
        PlayerInput player = GetComponentInParent<PlayerInput>();
        if (player == null)
        {
            Debug.LogError(
                $"{nameof(PlayerRingIndicator)} '{name}' is not under a PlayerInput.", this);
            enabled = false;
            return;
        }

        pin = player.GetComponentInChildren<PlayerIndicator>(true);
        sourceCollider = player.GetComponent<Collider>();
        segments = GetComponentsInChildren<Renderer>(true);

        lastColor = Color.clear;
        UpdatePlacement();
        UpdateColor();
    }

    private void LateUpdate()
    {
        spin += spinSpeed * Time.deltaTime;
        UpdatePlacement();
        UpdateColor();
    }

    // The ring is a child of the player, so it would otherwise swing with every turn of
    // the body. Writing world rotation instead keeps the spin steady and readable no
    // matter which way the character is facing.
    private void UpdatePlacement()
    {
        Vector3 anchor = sourceCollider != null
            ? sourceCollider.bounds.center
            : transform.parent.position;
        float feet = sourceCollider != null
            ? sourceCollider.bounds.min.y
            : transform.parent.position.y;

        transform.SetPositionAndRotation(
            new Vector3(anchor.x, feet + groundOffset, anchor.z),
            Quaternion.Euler(0f, spin, 0f));
    }

    private void UpdateColor()
    {
        Color color = pin != null ? pin.CurrentColor : Color.white;
        color.a = alpha;
        if (color == lastColor)
            return;

        lastColor = color;
        block ??= new MaterialPropertyBlock();
        block.SetColor(BaseColorId, color);

        // A property block, not a material instance: every player shares the one ring
        // material and nothing gets cloned or leaked.
        foreach (Renderer segment in segments)
            if (segment != null)
                segment.SetPropertyBlock(block);
    }
}
