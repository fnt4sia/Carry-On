using System;
using TMPro;
using UnityEngine;

// TEMPORARY: Level1Test station-refill experiment (Assets/_TempStationTest).
//
// A machine that has to be stocked before it works: Overcooked's idea, bent a little. It starts
// empty. A player brings a refill from a RefillCrate, loads it and holds Grab to stock the machine
// (the same crank a bag takes). After that it handles usesPerRefill bags, each loaded and cranked as
// usual, and then it is empty again.
//
//   Wrap   takes bubble-wrap rolls and wraps a bag in place, like Wrapper.
//   Paint  takes paint cans and repaints a bag the can's colour. It is meant for colours the belt
//          never spawns, so a gate can ask for "blue, wrapped": two machines' work for one bag,
//          while a second player keeps the machines stocked.
//
// A refill tops the machine back up to full. A can of another colour swaps the paint, whatever is
// left of the old one.
public class RefillStation : MachineStation
{
    [Serializable]
    private struct Paint
    {
        public LuggageColor color;
        [Tooltip("The material a bag of this colour wears: the one its Luggage <Colour> variant sets.")]
        public Material bagMaterial;
        public Color labelColor;
    }

    [Header("Refill")]
    [SerializeField] private RefillKind kind;
    [SerializeField, Min(1)] private int usesPerRefill = 3;
    [Tooltip("World-space label over the machine: what it holds, or that it needs stocking.")]
    [SerializeField] private TMP_Text stockLabel;
    [SerializeField] private Color stockedColor = Color.white;
    [SerializeField] private Color emptyColor = new(1f, 0.3f, 0.26f);

    [Header("Paint")]
    [Tooltip("Paint machines only. One entry per luggage colour.")]
    [SerializeField] private Paint[] paints = Array.Empty<Paint>();

    private int usesLeft;
    private LuggageColor paint;
    private RefillItem loadingRefill;

    private void Start() => RefreshLabel();

    public override bool CanAccept(Luggage luggage)
    {
        if (luggage == null)
            return false;

        if (luggage.TryGetComponent(out RefillItem refill))
        {
            if (refill.Kind != kind)
                return false;

            // A full machine turns away a refill that would change nothing.
            bool newPaint = kind == RefillKind.Paint && refill.PaintColor != paint;
            return usesLeft < usesPerRefill || newPaint;
        }

        if (usesLeft <= 0)
            return false;

        return kind == RefillKind.Wrap ? !luggage.IsWrapped : luggage.color != paint;
    }

    protected override void OnLuggagePlaced(Luggage luggage)
        => loadingRefill = luggage.GetComponent<RefillItem>();

    protected override Luggage OnProcessComplete(Luggage luggage)
    {
        if (loadingRefill != null)
        {
            usesLeft = usesPerRefill;
            paint = loadingRefill.PaintColor;

            // Used up. The base class still walks it out of the machine, so it is only hidden now and
            // destroyed once the cycle ends (OnStationReset).
            loadingRefill.gameObject.SetActive(false);
        }
        else
        {
            usesLeft--;
            if (kind == RefillKind.Wrap)
                luggage.MarkWrapped();
            else
                Repaint(luggage);
        }

        RefreshLabel();
        return luggage;
    }

    protected override void OnStationReset()
    {
        if (loadingRefill != null)
            Destroy(loadingRefill.gameObject);
        loadingRefill = null;
    }

    // A bag's colour is one material on its body renderer, set by its Luggage <Colour> variant. The
    // body is whichever renderer wears one of those materials.
    private void Repaint(Luggage bag)
    {
        if (!TryGetPaint(paint, out Paint target) || target.bagMaterial == null)
        {
            Debug.LogError($"{name} has no bag material for {paint}.", this);
            return;
        }

        foreach (Renderer body in bag.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = body.sharedMaterials;
            if (materials.Length == 0 || !IsBagMaterial(materials[0]))
                continue;

            PaintedLuggage.Remember(bag, body);
            materials[0] = target.bagMaterial;
            body.sharedMaterials = materials;
            bag.color = paint;
            return;
        }

        Debug.LogError($"{name} found no body renderer wearing a luggage colour on '{bag.name}'.", bag);
    }

    private bool IsBagMaterial(Material material)
    {
        foreach (Paint entry in paints)
            if (entry.bagMaterial == material)
                return true;
        return false;
    }

    private bool TryGetPaint(LuggageColor color, out Paint found)
    {
        foreach (Paint entry in paints)
        {
            if (entry.color != color)
                continue;

            found = entry;
            return true;
        }

        found = default;
        return false;
    }

    private void RefreshLabel()
    {
        if (stockLabel == null)
            return;

        if (usesLeft <= 0)
        {
            stockLabel.text = kind == RefillKind.Wrap ? "NEEDS WRAP" : "NEEDS PAINT";
            stockLabel.color = emptyColor;
            return;
        }

        if (kind == RefillKind.Wrap)
        {
            stockLabel.text = $"WRAP x{usesLeft}";
            stockLabel.color = stockedColor;
            return;
        }

        stockLabel.text = $"{paint.ToString().ToUpperInvariant()} x{usesLeft}";
        stockLabel.color = TryGetPaint(paint, out Paint entry) ? entry.labelColor : stockedColor;
    }
}
