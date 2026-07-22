using System.Collections;
using UnityEngine;

// Lily-pad stepping platform (Teratai). A player steps on it; after a short delay it
// smoothly sinks a little and swaps to the "sunk" model, holds for a while, then
// smoothly rises back and swaps to the "raised" model. All motion is eased
// (SmoothStep) so it glides instead of snapping.
[DisallowMultipleComponent]
public class LilyPadPlatform : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Raised / original state model (Teratai02).")]
    [SerializeField] private GameObject raisedVisual;
    [Tooltip("Sunk / pressed state model (Teratai01).")]
    [SerializeField] private GameObject sunkVisual;

    [Header("Motion")]
    [Tooltip("Transform that moves down and back up. Defaults to this object.")]
    [SerializeField] private Transform mover;
    [Tooltip("How far it sinks, in local units.")]
    [SerializeField, Min(0f)] private float sinkDepth = 0.3f;
    [Tooltip("Delay after the step before it starts sinking.")]
    [SerializeField, Min(0f)] private float stepDelay = 0.75f;
    [Tooltip("Seconds to ease down.")]
    [SerializeField, Min(0.01f)] private float sinkDuration = 0.35f;
    [Tooltip("Seconds to stay sunk before rising.")]
    [SerializeField, Min(0f)] private float holdDuration = 7f;
    [Tooltip("Seconds to ease back up.")]
    [SerializeField, Min(0.01f)] private float riseDuration = 0.5f;

    [Header("Trigger Filter")]
    [Tooltip("Only players trigger the sink. If false, luggage triggers it too.")]
    [SerializeField] private bool playerOnly = true;

    private Vector3 raisedLocalPos;
    private bool cycling;

    private void Reset()
    {
        mover = transform;
    }

    private void Awake()
    {
        if (mover == null) mover = transform;
        raisedLocalPos = mover.localPosition;
        SetSunk(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (cycling) return;
        if (!IsValidStepper(other)) return;
        StartCoroutine(Cycle());
    }

    private bool IsValidStepper(Collider other)
    {
        if (!playerOnly)
            return other.GetComponentInParent<PlayerMovement>() != null || Luggage.TryGetFromCollider(other, out _);

        return other.CompareTag("Player") || other.GetComponentInParent<PlayerMovement>() != null;
    }

    private IEnumerator Cycle()
    {
        cycling = true;

        yield return new WaitForSeconds(stepDelay);

        // Sink and swap to the pressed model as the motion begins.
        SetSunk(true);
        yield return MoveTo(raisedLocalPos - Vector3.up * sinkDepth, sinkDuration);

        yield return new WaitForSeconds(holdDuration);

        // Rise back, then restore the raised model.
        yield return MoveTo(raisedLocalPos, riseDuration);
        SetSunk(false);

        cycling = false;
    }

    private IEnumerator MoveTo(Vector3 targetLocal, float duration)
    {
        Vector3 start = mover.localPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            mover.localPosition = Vector3.LerpUnclamped(start, targetLocal, eased);
            yield return null;
        }
        mover.localPosition = targetLocal;
    }

    private void SetSunk(bool sunk)
    {
        if (raisedVisual != null) raisedVisual.SetActive(!sunk);
        if (sunkVisual != null) sunkVisual.SetActive(sunk);
    }
}
