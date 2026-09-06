using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Draws the current flight on the board above a gate: one slot per colour line, each showing
// a colour swatch and a "delivered/required" count.
//
// Every slot is authored in the scene. This component only shows, hides and fills them, so a
// flight that asks for fewer colours than there are slots simply leaves the spares hidden.
[DisallowMultipleComponent]
public class GateManifestDisplay : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        [Tooltip("The whole slot, toggled off when the flight has fewer colours than there are slots.")]
        public GameObject root;
        [Tooltip("Image tinted to the colour this line wants.")]
        public Image swatch;
        [Tooltip("Reads 'delivered/required', e.g. 1/3.")]
        public TMP_Text countText;
    }

    [SerializeField] private Gate gate;

    [Tooltip("Authored slots, left to right. A flight fills as many as it has colour lines.")]
    [SerializeField] private List<Slot> slots = new();

    [Header("Swatch Colours")]
    [SerializeField] private Color red = new(0.85f, 0.19f, 0.19f);
    [SerializeField] private Color blue = new(0.16f, 0.44f, 0.85f);
    [SerializeField] private Color green = new(0.22f, 0.70f, 0.31f);
    [SerializeField] private Color yellow = new(0.95f, 0.78f, 0.15f);

    [Header("Count Colours")]
    [SerializeField] private Color pendingText = Color.white;
    [SerializeField] private Color filledText = new(0.40f, 0.85f, 0.40f);

    private void Awake()
    {
        if (gate == null)
            gate = GetComponentInParent<Gate>();

        if (gate == null)
        {
            Debug.LogError($"{nameof(GateManifestDisplay)} '{name}' has no {nameof(Gate)} to read.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        gate.ManifestChanged += Redraw;
        Redraw();
    }

    private void OnDisable()
    {
        if (gate != null)
            gate.ManifestChanged -= Redraw;
    }

    private void Redraw()
    {
        IReadOnlyList<Gate.ManifestLine> manifest = gate.Manifest;
        int lineCount = manifest.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.root == null) continue;

            bool used = i < lineCount;
            if (slot.root.activeSelf != used)
                slot.root.SetActive(used);
            if (!used) continue;

            Gate.ManifestLine line = manifest[i];

            if (slot.swatch != null)
                slot.swatch.color = SwatchFor(line.Color);

            if (slot.countText != null)
            {
                slot.countText.text = $"{line.Delivered}/{line.Required}";
                slot.countText.color = line.IsFilled ? filledText : pendingText;
            }
        }

        if (lineCount > slots.Count)
        {
            Debug.LogWarning(
                $"{nameof(GateManifestDisplay)} '{name}' has {slots.Count} slots but the flight asks " +
                $"for {lineCount} colours. Author more slots or lower the gate's max colours per flight.",
                this);
        }
    }

    private Color SwatchFor(LuggageColor color) => color switch
    {
        LuggageColor.Red => red,
        LuggageColor.Blue => blue,
        LuggageColor.Green => green,
        LuggageColor.Yellow => yellow,
        _ => Color.white
    };
}
