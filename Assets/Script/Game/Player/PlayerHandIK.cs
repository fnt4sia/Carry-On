using UnityEngine;

// Pins the hands onto the carried luggage so they track it instead of holding a fixed
// carry pose. Unity's built-in OnAnimatorIK needs a Humanoid avatar and this rig is
// Generic with no avatar, so the two-bone solve is done here.
//
// Only the upper arm and forearm are written. The Rigify twist bones that sit between
// them (DEF-upper_arm.R.001, DEF-forearm.R.001) are children, so they inherit the
// correction and stay valid without being touched.
//
// The grip is usually outside the arm's reach (Annie's arm is 0.763 against a case parked
// ~1.7 from her shoulder), so the target is clamped onto the arm's reach sphere: the hands
// reach toward the case with the elbow still slightly bent, without ever landing on it.
// See maxReach.
public class PlayerHandIK : MonoBehaviour
{
    [System.Serializable]
    public class Arm
    {
        [Tooltip("Optional. Swinging the clavicle first buys roughly a third more reach " +
                 "(0.76 to about 1.03 on Annie) before the two-bone solve runs.")]
        public Transform clavicle;
        public Transform upperArm;
        public Transform forearm;
        public Transform hand;

        [Range(0f, 1f)] public float clavicleWeight = 0.6f;
        [Tooltip("Cap on how far the clavicle may swing, so the shoulder doesn't dislocate.")]
        public float clavicleMaxAngle = 28f;

        [Tooltip("Elbow hint, in player-local space. Decides which way the elbow bends.")]
        public Vector3 elbowHint = new(2f, -1f, -1.5f);

        [Tooltip("Where this hand grips, in luggage-local space (before the luggage's own scale).")]
        public Vector3 grip = new(0.65f, 1.2f, -0.62f);

        [Tooltip("Hand rotation relative to the luggage.")]
        public Vector3 handEuler;

        [HideInInspector] public Quaternion solvedClavicle;
        [HideInInspector] public Quaternion solvedUpperArm;
        [HideInInspector] public Quaternion solvedForearm;
        [HideInInspector] public Quaternion solvedHand;
    }

    [SerializeField] private PlayerGrab playerGrab;
    [SerializeField] private Arm rightArm = new();
    [SerializeField] private Arm leftArm = new();

    [Header("Torso assist")]
    [Tooltip("Spine bones ordered root first (DEF-spine .. DEF-spine.003). The arms alone " +
             "cannot cover the 0.43 of vertical shoulder bob in the walk cycle while the case " +
             "is pinned in world space, so the torso has to give them some slack.")]
    [SerializeField] private Transform[] spine;

    [Tooltip("Holds the chest square to the case instead of letting the walk swing it. " +
             "Halves the worst walk error on its own.")]
    [SerializeField, Range(0f, 1f)] private float spineStabilize;

    [Tooltip("Degrees of forward lean spread across the spine while carrying. Buys reach so " +
             "the arms are not at full extension, which is what leaves slack for the bob.")]
    [SerializeField, Range(0f, 60f)] private float carryLean;

    [Tooltip("How much of the torso assist survives while a throw is being charged. The " +
             "wind-up lives almost entirely in the spine, so holding the carry pose at full " +
             "strength would erase it. The arms stay pinned to the case either way.")]
    [SerializeField, Range(0f, 1f)] private float throwTorsoWeight = 0.25f;

    [Header("Blend")]
    [SerializeField, Range(0f, 1f)] private float maxWeight = 1f;
    [SerializeField, Min(0.01f)] private float blendTime = 0.15f;
    [SerializeField] private bool matchHandRotation = true;

    [Tooltip("How far the hand may extend toward the grip, as a fraction of the arm's " +
             "length. When the grip is out of reach the target is clamped onto this " +
             "sphere, so the arm reaches toward the case with the elbow still bent " +
             "instead of locking straight at a point it can never touch.")]
    [SerializeField, Range(0.5f, 1f)] private float maxReach = 0.95f;

    private float weight;
    private float torsoWeight = 1f;
    private bool hasSolvedPose;
    private Quaternion[] restSpine;

    private void Reset() => playerGrab = GetComponent<PlayerGrab>();

    private void Awake()
    {
        if (playerGrab == null) playerGrab = GetComponent<PlayerGrab>();
        CaptureRestSpine();
    }

    // Awake runs before the Animator's first write, so the bones still hold the pose the
    // prefab was authored in. That is the pose the chest is stabilised back toward.
    // Public because an editor preview has to call it before it samples a clip, otherwise
    // the lazy capture below would latch onto whatever frame happened to be sampled first.
    public void CaptureRestSpine()
    {
        if (spine == null) { restSpine = null; return; }
        restSpine = new Quaternion[spine.Length];
        for (int i = 0; i < spine.Length; i++)
            restSpine[i] = spine[i] != null ? spine[i].localRotation : Quaternion.identity;
    }

    // LateUpdate, so this runs after the Animator has written the clip's pose.
    private void LateUpdate()
    {
        Luggage held = playerGrab != null ? playerGrab.GetHeldLuggage() : null;
        weight = Mathf.MoveTowards(weight, held != null ? maxWeight : 0f, Time.deltaTime / blendTime);

        bool charging = playerGrab != null && playerGrab.IsChargingThrow;
        torsoWeight = Mathf.MoveTowards(torsoWeight, charging ? throwTorsoWeight : 1f, Time.deltaTime / blendTime);

        Apply(held != null ? held.transform : null, weight);
    }

    // Public so an editor preview can pose the arms without entering play mode.
    public void Apply(Transform luggage, float blendWeight)
    {
        if (blendWeight <= 0f)
        {
            hasSolvedPose = false;
            return;
        }

        // The torso runs first either way, so the release eases out of the lean instead of
        // snapping upright the frame the case leaves the hands.
        ApplyTorso(blendWeight);

        if (luggage != null)
        {
            SolveArm(rightArm, luggage, blendWeight);
            SolveArm(leftArm, luggage, blendWeight);
            hasSolvedPose = true;
        }
        else if (hasSolvedPose)
        {
            // Just released. Ease the last solved pose back into the animation rather than
            // keep tracking the luggage, which is now flying away from the hand.
            ReleaseArm(rightArm, blendWeight);
            ReleaseArm(leftArm, blendWeight);
        }
    }

    // Squares the chest to the case and leans it in. Both are scaled by the carry blend so
    // they fade with the grab rather than popping.
    private void ApplyTorso(float blendWeight)
    {
        if (spine == null || spine.Length == 0) return;
        if (restSpine == null || restSpine.Length != spine.Length) CaptureRestSpine();

        blendWeight *= torsoWeight;

        if (spineStabilize > 0f)
        {
            float t = spineStabilize * blendWeight;
            for (int i = 0; i < spine.Length; i++)
                if (spine[i] != null)
                    spine[i].localRotation = Quaternion.Slerp(spine[i].localRotation, restSpine[i], t);
        }

        if (carryLean > 0f)
        {
            // Spread over the whole chain so it curves rather than hinging at one joint.
            // Root first: each Rotate carries its children with it and the next one adds to that.
            float step = carryLean * blendWeight / spine.Length;
            foreach (Transform s in spine)
                if (s != null) s.Rotate(transform.right, step, Space.World);
        }
    }

    private void SolveArm(Arm arm, Transform luggage, float blendWeight)
    {
        if (arm.upperArm == null || arm.forearm == null || arm.hand == null) return;

        Quaternion animatedClavicle = arm.clavicle != null ? arm.clavicle.localRotation : Quaternion.identity;
        Quaternion animatedUpperArm = arm.upperArm.localRotation;
        Quaternion animatedForearm = arm.forearm.localRotation;
        Quaternion animatedHand = arm.hand.localRotation;

        Vector3 target = luggage.TransformPoint(arm.grip);

        // Swing the clavicle partway toward the target first. The walk clip carries the
        // shoulder up to 0.59 away from where the case is pinned, which is more than the
        // 0.76 arm can cover on its own.
        SwingClavicle(arm, target);

        // Measured after the clavicle swing, since that is what buys the extra reach.
        // The grip usually sits beyond what the arm can span, so pull the target back
        // onto the reach sphere: the hand reaches toward the case with the elbow still
        // bent, instead of the solve locking the arm straight at an untouchable point.
        float armLength = Vector3.Distance(arm.upperArm.position, arm.forearm.position)
                        + Vector3.Distance(arm.forearm.position, arm.hand.position);
        Vector3 toTarget = target - arm.upperArm.position;
        float needed = toTarget.magnitude;
        float maxExtension = armLength * maxReach;
        if (needed > maxExtension && needed > 0.0001f)
            target = arm.upperArm.position + toTarget * (maxExtension / needed);

        SolveTwoBone(arm.upperArm, arm.forearm, arm.hand, target, transform.TransformPoint(arm.elbowHint));

        if (matchHandRotation)
            arm.hand.rotation = luggage.rotation * Quaternion.Euler(arm.handEuler);

        if (arm.clavicle != null)
        {
            arm.solvedClavicle = arm.clavicle.localRotation;
            arm.clavicle.localRotation = Quaternion.Slerp(animatedClavicle, arm.solvedClavicle, blendWeight);
        }
        arm.solvedUpperArm = arm.upperArm.localRotation;
        arm.solvedForearm = arm.forearm.localRotation;
        arm.solvedHand = arm.hand.localRotation;

        arm.upperArm.localRotation = Quaternion.Slerp(animatedUpperArm, arm.solvedUpperArm, blendWeight);
        arm.forearm.localRotation = Quaternion.Slerp(animatedForearm, arm.solvedForearm, blendWeight);
        arm.hand.localRotation = Quaternion.Slerp(animatedHand, arm.solvedHand, blendWeight);
    }

    // Rotates the clavicle a fraction of the way toward the target, capped so the shoulder
    // stays anatomically sane. The two-bone solve then finishes the reach.
    private static void SwingClavicle(Arm arm, Vector3 target)
    {
        if (arm.clavicle == null || arm.clavicleWeight <= 0f) return;

        Vector3 claviclePos = arm.clavicle.position;
        Vector3 toHand = arm.hand.position - claviclePos;
        Vector3 toTarget = target - claviclePos;
        if (toHand.sqrMagnitude < 1e-8f || toTarget.sqrMagnitude < 1e-8f) return;

        Quaternion.FromToRotation(toHand, toTarget).ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-8f) return;
        if (angle > 180f) angle -= 360f;

        angle = Mathf.Clamp(angle * arm.clavicleWeight, -arm.clavicleMaxAngle, arm.clavicleMaxAngle);
        arm.clavicle.rotation = Quaternion.AngleAxis(angle, axis) * arm.clavicle.rotation;
    }

    private static void ReleaseArm(Arm arm, float blendWeight)
    {
        if (arm.upperArm == null || arm.forearm == null || arm.hand == null) return;

        if (arm.clavicle != null)
            arm.clavicle.localRotation = Quaternion.Slerp(arm.clavicle.localRotation, arm.solvedClavicle, blendWeight);
        arm.upperArm.localRotation = Quaternion.Slerp(arm.upperArm.localRotation, arm.solvedUpperArm, blendWeight);
        arm.forearm.localRotation = Quaternion.Slerp(arm.forearm.localRotation, arm.solvedForearm, blendWeight);
        arm.hand.localRotation = Quaternion.Slerp(arm.hand.localRotation, arm.solvedHand, blendWeight);
    }

    // Law-of-cosines two-bone solve. The corrections are applied as deltas on top of the
    // pose the Animator just wrote, which is what keeps the in-between twist bones valid.
    private static void SolveTwoBone(Transform upperArm, Transform forearm, Transform hand,
                                     Vector3 target, Vector3 pole)
    {
        Vector3 shoulderPos = upperArm.position;
        Vector3 elbowPos = forearm.position;
        Vector3 handPos = hand.position;

        float upperLength = Vector3.Distance(shoulderPos, elbowPos);
        float forearmLength = Vector3.Distance(elbowPos, handPos);
        if (upperLength <= 0.0001f || forearmLength <= 0.0001f) return;

        // Clamp into the arm's reach, otherwise the elbow locks straight and jitters.
        float reach = Mathf.Clamp(
            Vector3.Distance(shoulderPos, target),
            Mathf.Abs(upperLength - forearmLength) + 0.001f,
            upperLength + forearmLength - 0.001f);

        float shoulderAngleNow = AngleBetween(handPos - shoulderPos, elbowPos - shoulderPos);
        float elbowAngleNow = AngleBetween(shoulderPos - elbowPos, handPos - elbowPos);

        float shoulderAngleWanted = Mathf.Acos(Mathf.Clamp(
            (forearmLength * forearmLength - upperLength * upperLength - reach * reach)
            / (-2f * upperLength * reach), -1f, 1f));
        float elbowAngleWanted = Mathf.Acos(Mathf.Clamp(
            (reach * reach - upperLength * upperLength - forearmLength * forearmLength)
            / (-2f * upperLength * forearmLength), -1f, 1f));

        Vector3 bendAxis = Vector3.Cross(handPos - shoulderPos, pole - shoulderPos);
        if (bendAxis.sqrMagnitude < 1e-8f) bendAxis = Vector3.Cross(handPos - shoulderPos, elbowPos - shoulderPos);
        if (bendAxis.sqrMagnitude < 1e-8f) return;
        bendAxis.Normalize();

        Quaternion upperArmWorld = upperArm.rotation;
        Quaternion forearmWorld = forearm.rotation;

        upperArm.localRotation *= Quaternion.AngleAxis(
            (shoulderAngleWanted - shoulderAngleNow) * Mathf.Rad2Deg,
            Quaternion.Inverse(upperArmWorld) * bendAxis);
        forearm.localRotation *= Quaternion.AngleAxis(
            (elbowAngleWanted - elbowAngleNow) * Mathf.Rad2Deg,
            Quaternion.Inverse(forearmWorld) * bendAxis);

        // Finally swing the whole arm so the hand lands on the target.
        upperArm.rotation = Quaternion.FromToRotation(hand.position - shoulderPos, target - shoulderPos)
                            * upperArm.rotation;
    }

    private static float AngleBetween(Vector3 from, Vector3 to) =>
        Mathf.Acos(Mathf.Clamp(Vector3.Dot(from.normalized, to.normalized), -1f, 1f));
}
