using UnityEngine;

public class Gateway : MonoBehaviour
{
    [Header("Gateway State")]
    [SerializeField] private bool isOpen = false;

    [Header("Components")]
    [SerializeField] private Animator gateAnimator;

    private void Start()
    {
        UpdateGateVisuals();
        gateAnimator.SetBool("IsOpen", isOpen);
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
        Debug.Log("kepanggil bang");
        isOpen = !isOpen;
        UpdateGateVisuals();
    }

    private void UpdateGateVisuals()
    {
        if (gateAnimator != null)
        {
            gateAnimator.SetBool("IsOpen", isOpen);
        }
    }
}
