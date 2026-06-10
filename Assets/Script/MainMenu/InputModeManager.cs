using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Drives the lobby's input mode. Replaces the old Boot keyboard/mouse detection.
//   Pointer    -> mouse cursor visible, no UI highlight (menu clicked with mouse).
//   Navigation -> cursor hidden, a UI element highlighted for gamepad navigation.
// The initial mode is chosen by MainMenuManager from the first joining device
// (keyboard/mouse player -> Pointer, gamepad player -> Navigation). After that, mouse
// activity flips to Pointer and gamepad activity flips to Navigation. Keyboard is left
// out on purpose: the keyboard player uses the mouse to drive the menu.
public class InputModeManager : MonoBehaviour
{
    public enum Mode { Pointer, Navigation }

    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private GameObject defaultSelection; // highlighted first in Navigation mode (Start button)
    [Tooltip("Mouse pixels-per-frame before movement counts as pointer activity.")]
    [SerializeField] private float mouseMoveThreshold = 2f;

    public Mode CurrentMode { get; private set; } = Mode.Pointer;

    private void Awake()
    {
        if (eventSystem == null) eventSystem = EventSystem.current;
    }

    // Called once by MainMenuManager when the lobby opens, keyed off the first device.
    public void SetInitialMode(Mode mode)
    {
        if (mode == Mode.Pointer) ApplyPointer();
        else ApplyNavigation();
    }

    private void Update()
    {
        if (MouseActivity())
        {
            if (CurrentMode != Mode.Pointer) ApplyPointer();
            return;
        }

        if (GamepadActivity())
        {
            if (CurrentMode != Mode.Navigation) ApplyNavigation();
            return;
        }

        // Keep a valid selection while navigating in case it gets cleared.
        if (CurrentMode == Mode.Navigation && eventSystem != null
            && eventSystem.currentSelectedGameObject == null)
            SelectDefault();
    }

    private bool MouseActivity()
    {
        var m = Mouse.current;
        if (m == null) return false;

        if (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)
            return true;

        return m.delta.ReadValue().sqrMagnitude > mouseMoveThreshold * mouseMoveThreshold;
    }

    private bool GamepadActivity()
    {
        foreach (var pad in Gamepad.all)
        {
            if (!pad.wasUpdatedThisFrame) continue;

            if (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                pad.startButton.wasPressedThisFrame ||
                pad.leftStick.ReadValue().sqrMagnitude > 0.2f ||
                pad.dpad.ReadValue().sqrMagnitude > 0.2f)
                return true;
        }
        return false;
    }

    private void ApplyPointer()
    {
        CurrentMode = Mode.Pointer;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
    }

    private void ApplyNavigation()
    {
        CurrentMode = Mode.Navigation;
        Cursor.visible = false;
        SelectDefault();
    }

    private void SelectDefault()
    {
        if (eventSystem == null || defaultSelection == null) return;
        eventSystem.SetSelectedGameObject(defaultSelection);
    }
}
