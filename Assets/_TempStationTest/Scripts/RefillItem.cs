using UnityEngine;

// TEMPORARY: Level1Test station-refill experiment. Lives in Assets/_TempStationTest and goes when
// that folder is deleted.

public enum RefillKind
{
    Wrap,
    Paint
}

// A refill a player carries from a RefillCrate to a RefillStation: a roll of bubble wrap, or a can
// of paint in one colour.
//
// PlayerGrab and MachineStation only know how to carry and load Luggage, so a refill is a Luggage
// too: it is grabbed, carried, thrown and loaded exactly like a bag. It is flagged delivered, which
// is what makes a DispatchGate skip it, so a can thrown into the booth never fills an order line.
[RequireComponent(typeof(Luggage))]
public class RefillItem : MonoBehaviour
{
    [SerializeField] private RefillKind kind;
    [Tooltip("Paint cans only: the colour a bag comes out of the coloring station.")]
    [SerializeField] private LuggageColor paintColor;

    public RefillKind Kind => kind;
    public LuggageColor PaintColor => paintColor;
    public Luggage Luggage { get; private set; }

    private void Awake()
    {
        Luggage = GetComponent<Luggage>();

        // Initialised here so Luggage.Start doesn't run Initialize later and clear the flag.
        Luggage.Initialize(prefabKey: null);
        Luggage.IsDelivered = true;
    }
}
