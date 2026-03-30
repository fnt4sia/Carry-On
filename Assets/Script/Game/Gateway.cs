using UnityEngine;

public class Gateway : MonoBehaviour
{
    [Header("Gateway State")]
    [SerializeField] private bool isOpen = false;

    [Header("Components")]
    [Tooltip("Animator to handle the open/close animation. Uses a boolean parameter 'IsOpen'.")]
    public Animator gateAnimator;
    
    [Tooltip("The obstacle collider that blocks players/luggage. If assigned, it will automatically disable when open.")]
    public Collider physicalCollider;

    private void Start()
    {
        if (gateAnimator == null) gateAnimator = GetComponent<Animator>();
        if (physicalCollider == null) physicalCollider = GetComponent<Collider>();

        // Ensure the initial state matches the bool setting
        UpdateGateVisuals();
    }

    /// <summary>
    /// Forces the gateway to open.
    /// </summary>
    public void Open()
    {
        if (!isOpen)
        {
            isOpen = true;
            UpdateGateVisuals();
        }
    }

    /// <summary>
    /// Forces the gateway to close.
    /// </summary>
    public void Close()
    {
        if (isOpen)
        {
            isOpen = false;
            UpdateGateVisuals();
        }
    }

    /// <summary>
    /// Swaps the current state. Useful for Toggle mode on pressure plates.
    /// </summary>
    public void Toggle()
    {
        isOpen = !isOpen;
        UpdateGateVisuals();
    }

    private void UpdateGateVisuals()
    {
        // Tell the animator about the new state
        if (gateAnimator != null)
        {
            gateAnimator.SetBool("IsOpen", isOpen);
        }

        // Optional: Programmatically toggle a physical collider to let players pass through
        // if they haven't set up animation events to do it
        if (physicalCollider != null)
        {
            physicalCollider.enabled = !isOpen; 
        }

        Debug.Log($"[Gateway] {gameObject.name} state changed. IsOpen: {isOpen}");
    }
}
