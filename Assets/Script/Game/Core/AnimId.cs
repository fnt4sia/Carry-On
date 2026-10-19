using UnityEngine;

public static class AnimId
{
    public const string IsTriggeredName = "isTriggered";
    public const string IsMovingName = "isMoving";
    public const string IsDashingName = "isDashing";
    public const string IsGrabbingName = "isGrabbing";
    public const string IsThrowingName = "isThrowing";
    public const string DashProgressName = "dashProgress";
    public const string IsOpenName = "IsOpen";
    public const string UpperBodyLayerName = "Upper Body";
    public const string UpperBodyEmptyName = "Empty";

    public static readonly int IsTriggered = Animator.StringToHash(IsTriggeredName);
    public static readonly int IsMoving = Animator.StringToHash(IsMovingName);
    public static readonly int IsDashing = Animator.StringToHash(IsDashingName);
    public static readonly int IsGrabbing = Animator.StringToHash(IsGrabbingName);
    public static readonly int IsThrowing = Animator.StringToHash(IsThrowingName);
    public static readonly int DashProgress = Animator.StringToHash(DashProgressName);
    public static readonly int IsOpen = Animator.StringToHash(IsOpenName);
    public static readonly int UpperBodyEmpty = Animator.StringToHash(UpperBodyEmptyName);
}
