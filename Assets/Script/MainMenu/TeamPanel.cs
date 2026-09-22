using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The four-seat strip at the bottom of the lobby screen. Seats fill left to right in join order:
// a taken seat is lit and names its device, the next free seat pulses the button that would take
// it, and the rest sit dim and blank.
//
// Everything is authored in MainMenu (LobbyHUD/TeamPanel) and TeamSlot.prefab. This decides what
// each seat says; TeamSlot draws it. MainMenuManager decides when the panel is shown.
//
// Button names follow the Xbox layout (A to join, Y to split), which is what PC games label
// generically. Swap these for glyph sprites when the art exists.
public class TeamPanel : MonoBehaviour
{
    [SerializeField, Tooltip("One per seat, in join order.")]
    private TeamSlot[] slots;

    [SerializeField, Tooltip("Colour of the (Y) SPLIT line under a whole pad. Match TeamSlot's prompt colour.")]
    private Color splitHintColor = new Color(1f, 0.894f, 0.431f);

    private IReadOnlyList<PlayerInput> players;

    // A pad plugged in or pulled out changes what the free seat should ask for, and that can
    // happen with nobody touching anything.
    private void OnEnable() => InputSystem.onDeviceChange += HandleDeviceChange;

    private void OnDisable() => InputSystem.onDeviceChange -= HandleDeviceChange;

    private void HandleDeviceChange(InputDevice device, InputDeviceChange change) => Refresh(players);

    public void Refresh(IReadOnlyList<PlayerInput> joinedPlayers)
    {
        players = joinedPlayers;
        if (slots == null) return;

        int count = joinedPlayers?.Count ?? 0;
        bool seatFree = count < slots.Length;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            if (i < count) slots[i].ShowPlayer(DeviceLabel(joinedPlayers[i], seatFree));
            else if (i == count) slots[i].ShowPrompt(JoinPrompt());
            else slots[i].ShowEmpty();
        }
    }

    // What to press to take the next seat — the key on its own, one line like every other tag, so
    // the panel's margins stay even. The dim figure and the pulse already say "free, press this",
    // and the banner's join prompt spells the wording out in full.
    //
    // A seat is only 57 px wide, so a second word does not fit; a free pad also wins over the
    // keyboard, the lobby's whole point being couch co-op.
    private static string JoinPrompt()
    {
        return
            PlayerSystem.FirstFreeGamepad() != null ? "(A)" :
            PlayerSystem.IsSchemeFree(PlayerSystem.SchemeKeyboardLeft) ? "SPACE" :
            PlayerSystem.IsSchemeFree(PlayerSystem.SchemeKeyboardRight) ? "R-SHIFT" :
            string.Empty;
    }

    // The split hint hangs under the pad it would halve, which is also the only place it makes
    // sense: that player is the one who has to press it.
    private string DeviceLabel(PlayerInput player, bool seatFree)
    {
        if (player == null) return string.Empty;

        switch (player.currentControlScheme)
        {
            case PlayerSystem.SchemeKeyboardLeft: return "WASD";
            // "ARROWS" is 59 px at font 13 against a 62 px seat pitch — it touches its neighbour.
            case PlayerSystem.SchemeKeyboardRight: return "ARROW";
            case PlayerSystem.SchemeGamepadLeft: return PadName(player) + " L";
            case PlayerSystem.SchemeGamepadRight: return PadName(player) + " R";
            default:
                string name = PadName(player);
                if (!seatFree) return name;
                // 85%: at full size the hint is as wide as the seat pitch and touches its neighbour.
                return $"{name}\n<size=85%><color=#{ColorUtility.ToHtmlStringRGB(splitHintColor)}>(Y) SPLIT</color></size>";
        }
    }

    // Pads are numbered by plug order, which is the only number a player can see for themselves.
    private static string PadName(PlayerInput player)
    {
        foreach (InputDevice device in player.devices)
        {
            if (device is not Gamepad pad) continue;

            for (int i = 0; i < Gamepad.all.Count; i++)
                if (Gamepad.all[i] == pad) return "PAD " + (i + 1);
        }

        return "PAD";
    }
}
