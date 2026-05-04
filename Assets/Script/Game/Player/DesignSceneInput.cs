using UnityEngine;
using UnityEngine.InputSystem;

public class DesignSceneInput : MonoBehaviour
{
    [Header("Players")]
    [SerializeField] private GameObject player1;
    [SerializeField] private GameObject player2;

    private PlayerMovement p1Movement;
    private PlayerGrab     p1Grab;
    private PlayerMovement p2Movement;
    private PlayerGrab     p2Grab;

    private bool p1GrabWasHeld;
    private bool p2GrabWasHeld;

    private void Awake()
    {
        if (player1 != null)
        {
            p1Movement = player1.GetComponent<PlayerMovement>();
            p1Grab     = player1.GetComponent<PlayerGrab>();
        }

        if (player2 != null)
        {
            p2Movement = player2.GetComponent<PlayerMovement>();
            p2Grab     = player2.GetComponent<PlayerGrab>();
        }
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // ── Player 1: WASD + Left Shift dash + E grab + F station ──────
        if (p1Movement != null)
        {
            Vector2 move = new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));

            p1Movement.InjectInput(move, kb.leftShiftKey.wasPressedThisFrame);
        }

        if (p1Grab != null)
        {
            bool held = kb.eKey.isPressed;
            p1Grab.InjectGrabInput(held && !p1GrabWasHeld, !held && p1GrabWasHeld);
            p1GrabWasHeld = held;

            p1Grab.InjectStationUse(kb.fKey.wasPressedThisFrame);
        }

        // ── Player 2: Arrow keys + Right Shift dash + Right Ctrl grab + Slash station ──
        if (p2Movement != null)
        {
            Vector2 move = new Vector2(
                (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f),
                (kb.upArrowKey.isPressed    ? 1f : 0f) - (kb.downArrowKey.isPressed  ? 1f : 0f));

            p2Movement.InjectInput(move, kb.rightShiftKey.wasPressedThisFrame);
        }

        if (p2Grab != null)
        {
            bool held = kb.rightCtrlKey.isPressed;
            p2Grab.InjectGrabInput(held && !p2GrabWasHeld, !held && p2GrabWasHeld);
            p2GrabWasHeld = held;

            p2Grab.InjectStationUse(kb.slashKey.wasPressedThisFrame);
        }
    }
}
