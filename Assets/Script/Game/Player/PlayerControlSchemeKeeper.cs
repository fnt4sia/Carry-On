using UnityEngine;
using UnityEngine.InputSystem;

// KeyboardLeft and KeyboardRight are both bound to the same <Keyboard> device, so the
// control scheme mask is the only thing separating the WASD player from the arrows
// player. Disabling a player GameObject (PoolHazard respawn, the ChooseStage map view)
// makes PlayerInput drop its InputUser pairing, and because the character prefab has no
// Default Control Scheme it comes back with no mask at all -- the re-enabled player then
// answers to every binding in the Player map, so a drowned arrows player also walks on
// WASD. Remember the pairing this player joined with and put it back on re-enable.
[RequireComponent(typeof(PlayerInput))]
[DisallowMultipleComponent]
public class PlayerControlSchemeKeeper : MonoBehaviour
{
    private PlayerInput playerInput;
    private string joinedScheme;
    private InputDevice[] joinedDevices;
    private bool restorePending;

    private void Awake() => playerInput = GetComponent<PlayerInput>();

    private void OnEnable() => restorePending = true;

    private void Update()
    {
        // Deferred by a frame on purpose: PlayerInput re-pairs the user in its own
        // OnEnable, and component enable order is not guaranteed, so restoring any
        // earlier risks being overwritten by that re-pairing.
        if (restorePending)
        {
            restorePending = false;
            // user.valid is the guard, not a formality: on a userless PlayerInput
            // SwitchCurrentControlScheme throws InvalidOperationException("Invalid user").
            if (joinedDevices != null
                && playerInput.user.valid
                && (playerInput.currentControlScheme != joinedScheme || !HoldsJoinedDevices())
                && JoinedDevicesConnected())
            {
                playerInput.SwitchCurrentControlScheme(joinedScheme, joinedDevices);
            }
        }

        Remember();
    }

    // The scheme coming back right doesn't mean the device did. With a default scheme set,
    // PlayerInput re-pairs to the first gamepad nobody else holds, not to the one this player
    // joined on: after the map, the right half of a split pad landed on another player's pad,
    // and whole pads swapped characters whenever join order differed from plug order.
    private bool HoldsJoinedDevices()
    {
        if (playerInput.devices.Count != joinedDevices.Length)
            return false;

        foreach (InputDevice joined in joinedDevices)
        {
            bool held = false;
            foreach (InputDevice device in playerInput.devices)
                if (device == joined)
                    held = true;
            if (!held)
                return false;
        }
        return true;
    }

    // A pad unplugged while its player was switched off can't be paired; leave what PlayerInput
    // chose until the pad is back.
    private bool JoinedDevicesConnected()
    {
        foreach (InputDevice joined in joinedDevices)
            if (joined == null || !joined.added)
                return false;
        return true;
    }

    // The scheme only changes at join time, but polling avoids assuming anything about
    // when PlayerSystem finishes the join. An empty scheme means the pairing is torn
    // down, so the last good value is kept instead of being overwritten with nothing.
    private void Remember()
    {
        string current = playerInput.currentControlScheme;
        if (string.IsNullOrEmpty(current) || current == joinedScheme)
            return;

        joinedScheme = current;
        joinedDevices = new InputDevice[playerInput.devices.Count];
        for (int i = 0; i < joinedDevices.Length; i++)
            joinedDevices[i] = playerInput.devices[i];

        // Both halves are needed. PlayerInput only rebuilds a valid InputUser on enable
        // if it has a default scheme to pair against, so without this the respawn comes
        // back userless and the restore above cannot run. On its own, though, a default
        // scheme brings the player back with no bindings resolved at all -- it takes the
        // explicit SwitchCurrentControlScheme to put the mask back.
        playerInput.defaultControlScheme = joinedScheme;
    }
}
