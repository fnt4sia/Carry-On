using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hardcoded input for the DesignScene so level designers can play-test
/// without going through Boot → MainMenu → ChooseStage.
///
/// Player 1 (Ramp Agent Body): WASD move, Left Shift dash, E grab
/// Player 2 (Robot Body):      Arrow keys move, Right Shift dash, Right Ctrl grab
///
/// Attach this to any GameObject in DesignScene.
/// Drag the two player GameObjects into the inspector slots.
/// </summary>
public class DesignSceneInput : MonoBehaviour
{
    [SerializeField] private PlayerMovement player1Movement;
    [SerializeField] private PlayerGrab     player1Grab;

    [SerializeField] private PlayerMovement player2Movement;
    [SerializeField] private PlayerGrab     player2Grab;

    private bool p1GrabWasHeld;
    private bool p2GrabWasHeld;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // ── Player 1: WASD + Left Shift dash + E grab ──────────────────
        if (player1Movement != null)
        {
            Vector2 p1Move = new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));

            bool p1Dash = kb.leftShiftKey.wasPressedThisFrame;
            player1Movement.InjectInput(p1Move, p1Dash);
        }

        if (player1Grab != null)
        {
            bool p1GrabHeld = kb.eKey.isPressed;
            bool p1GrabDown = p1GrabHeld && !p1GrabWasHeld;
            bool p1GrabUp   = !p1GrabHeld && p1GrabWasHeld;
            player1Grab.InjectGrabInput(p1GrabDown, p1GrabUp);
            p1GrabWasHeld = p1GrabHeld;
        }

        // ── Player 2: Arrow keys + Right Shift dash + Right Ctrl grab ──
        if (player2Movement != null)
        {
            Vector2 p2Move = new Vector2(
                (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f),
                (kb.upArrowKey.isPressed    ? 1f : 0f) - (kb.downArrowKey.isPressed  ? 1f : 0f));

            bool p2Dash = kb.rightShiftKey.wasPressedThisFrame;
            player2Movement.InjectInput(p2Move, p2Dash);
        }

        if (player2Grab != null)
        {
            bool p2GrabHeld = kb.rightCtrlKey.isPressed;
            bool p2GrabDown = p2GrabHeld && !p2GrabWasHeld;
            bool p2GrabUp   = !p2GrabHeld && p2GrabWasHeld;
            player2Grab.InjectGrabInput(p2GrabDown, p2GrabUp);
            p2GrabWasHeld = p2GrabHeld;
        }
    }
}
