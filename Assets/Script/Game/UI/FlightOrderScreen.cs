using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The dispatch gate's order board, drawn like a departures screen: one slot per colour the flight
// wants (a bag icon over "in booth/needed"), the combo, and a timer bar along the bottom that drains
// from right to left. While a flight is dispatched it shows the verdict instead: the APPROVED stamp
// and the coins, or MISSED.
//
// Every element is authored in the scene, per the UI rule; this only shows, hides and fills them.
[DisallowMultipleComponent]
public class FlightOrderScreen : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public GameObject root;
        public Image icon;
        [Tooltip("Reads 'in booth/needed', e.g. 1/2.")]
        public TMP_Text count;
    }

    [SerializeField] private DispatchGate gate;

    [Header("Order")]
    [SerializeField] private GameObject orderGroup;
    [Tooltip("Authored slots, left to right. An order fills as many as it has colours.")]
    [SerializeField] private List<Slot> slots = new();
    [SerializeField] private TMP_Text comboText;
    [Tooltip("Shown while the booth holds a bag the order doesn't want.")]
    [SerializeField] private GameObject wrongBagWarning;

    [Header("Bag Icons")]
    [SerializeField] private Sprite redBag;
    [SerializeField] private Sprite greenBag;
    [SerializeField] private Sprite yellowBag;
    [SerializeField] private Sprite blueBag;

    [Header("Count Colours")]
    [SerializeField] private Color pendingText = Color.white;
    [SerializeField] private Color filledText = new(0.36f, 0.93f, 0.52f);
    [SerializeField] private Color overText = new(1f, 0.35f, 0.3f);

    [Header("Timer")]
    [Tooltip("Filled image, horizontal, origin left: it empties toward the left as time runs out.")]
    [SerializeField] private Image timerFill;
    [SerializeField] private Color timerFull = new(0.36f, 0.93f, 0.52f);
    [SerializeField] private Color timerHalf = new(1f, 0.72f, 0.16f);
    [SerializeField] private Color timerLow = new(1f, 0.3f, 0.26f);

    [Header("Verdict")]
    [SerializeField] private GameObject resultGroup;
    [SerializeField] private GameObject approved;
    [SerializeField] private GameObject missed;
    [SerializeField] private TMP_Text resultCoins;

    private void Awake()
    {
        if (gate == null)
        {
            Debug.LogError($"{nameof(FlightOrderScreen)} '{name}' has no {nameof(DispatchGate)} to read.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        gate.OrderChanged += Redraw;
        gate.Dispatched += ShowVerdict;
        Redraw();
    }

    private void OnDisable()
    {
        if (gate == null) return;
        gate.OrderChanged -= Redraw;
        gate.Dispatched -= ShowVerdict;
    }

    private void Update()
    {
        if (timerFill == null) return;

        float left = gate.OrderTime > 0f ? Mathf.Clamp01(gate.TimeLeft / gate.OrderTime) : 0f;
        timerFill.fillAmount = left;
        timerFill.color = left > 0.5f
            ? Color.Lerp(timerHalf, timerFull, (left - 0.5f) * 2f)
            : Color.Lerp(timerLow, timerHalf, left * 2f);
    }

    private void Redraw()
    {
        if (gate.CurrentPhase == DispatchGate.Phase.Loading)
            ShowOrder(true);

        IReadOnlyList<DispatchGate.OrderLine> order = gate.Order;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.root == null) continue;

            bool used = i < order.Count;
            if (slot.root.activeSelf != used)
                slot.root.SetActive(used);
            if (!used) continue;

            DispatchGate.OrderLine line = order[i];
            if (slot.icon != null)
                slot.icon.sprite = IconFor(line.Color);

            if (slot.count != null)
            {
                slot.count.text = $"{line.InBooth}/{line.Required}";
                slot.count.color = line.IsOver ? overText : line.IsFilled ? filledText : pendingText;
            }
        }

        if (wrongBagWarning != null && wrongBagWarning.activeSelf != gate.WrongBags > 0)
            wrongBagWarning.SetActive(gate.WrongBags > 0);

        if (comboText != null)
        {
            bool onStreak = gate.Streak > 0;
            if (comboText.gameObject.activeSelf != onStreak)
                comboText.gameObject.SetActive(onStreak);
            if (onStreak && LevelContext.CurrentConfig != null)
                comboText.text = $"COMBO x{LevelContext.CurrentConfig.ComboMultiplier(gate.Streak + 1):0.#}";
        }

        if (order.Count > slots.Count)
        {
            Debug.LogWarning(
                $"{nameof(FlightOrderScreen)} '{name}' has {slots.Count} slots but the order asks for " +
                $"{order.Count} colours. Author more slots or lower the gate's max colours per order.", this);
        }
    }

    private void ShowVerdict(bool success, int coins)
    {
        ShowOrder(false);
        if (approved != null) approved.SetActive(success);
        if (missed != null) missed.SetActive(!success);
        if (resultCoins != null)
        {
            resultCoins.gameObject.SetActive(success);
            resultCoins.text = $"+{coins}";
        }
    }

    private void ShowOrder(bool show)
    {
        if (orderGroup != null && orderGroup.activeSelf != show) orderGroup.SetActive(show);
        if (resultGroup != null && resultGroup.activeSelf == show) resultGroup.SetActive(!show);
    }

    private Sprite IconFor(LuggageColor color) => color switch
    {
        LuggageColor.Red => redBag,
        LuggageColor.Green => greenBag,
        LuggageColor.Yellow => yellowBag,
        LuggageColor.Blue => blueBag,
        _ => null
    };
}
