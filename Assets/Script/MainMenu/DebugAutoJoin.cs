using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor-only sandbox bootstrap. It creates normal PlayerInputManager players with
/// the production control schemes, so DesignScene never needs a second input path.
/// </summary>
[DefaultExecutionOrder(-200)]
public class DebugAutoJoin : MonoBehaviour
{
    [SerializeField, Range(1, 2)] private int keyboardPlayerCount = 2;

    private void Start()
    {
#if UNITY_EDITOR
        PlayerInputManager manager = PlayerSystem.Instance?.Manager;
        Keyboard keyboard = Keyboard.current;
        if (manager == null || keyboard == null)
        {
            Debug.LogError($"{nameof(DebugAutoJoin)} needs the Runtime/PlayerSystem prefab and a keyboard.");
            return;
        }

        if (PlayerInput.all.Count == 0 && keyboardPlayerCount >= 1)
            PlayerSystem.Instance.JoinPlayer(PlayerSystem.SchemeKeyboardLeft, keyboard);

        if (PlayerInput.all.Count < 2 && keyboardPlayerCount >= 2)
            PlayerSystem.Instance.JoinPlayer(PlayerSystem.SchemeKeyboardRight, keyboard);
#endif
    }
}
