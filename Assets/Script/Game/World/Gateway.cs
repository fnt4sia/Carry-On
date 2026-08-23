using UnityEngine;

// Animated door, normally driven by GatewayPressurePlate. Open/Close/Toggle flip the isOpen
// flag and push it to the animator's "IsOpen" bool, which plays the matching clip.
public class Gateway : MonoBehaviour
{
    [Header("Gateway State")]
    [SerializeField] private bool isOpen = false;

    [Header("Components")]
    [SerializeField] private Animator gateAnimator;

    public bool IsOpen => isOpen;

    private void Start()
    {
        UpdateGateVisuals();
    }

    public void Open()
    {
        if (!isOpen)
        {
            isOpen = true;
            UpdateGateVisuals();
        }
    }

    public void Close()
    {
        if (isOpen)
        {
            isOpen = false;
            UpdateGateVisuals();
        }
    }

    public void Toggle()
    {
        isOpen = !isOpen;
        UpdateGateVisuals();
    }

    private void UpdateGateVisuals()
    {
        if (gateAnimator != null)
        {
            gateAnimator.SetBool(AnimId.IsOpen, isOpen);
        }
    }
}
