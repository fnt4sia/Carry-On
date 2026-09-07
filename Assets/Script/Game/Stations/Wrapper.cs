using UnityEngine;

// Wrapping station.
//
// Legacy levels wrap Fragile luggage only, swapping it for the wrapped-luggage prefab. The
// redesign wraps any bag that is not already wrapped, and does it *in place* so the bag keeps
// its flight colour — a gate asking for "wrapped, any colour" still needs the bag to look like
// the colour it is.
public class Wrapper : MachineStation
{
    [Tooltip("Redesign behaviour: wrap any bag that is not already wrapped, in place, keeping its " +
             "colour. Off = legacy behaviour, Fragile only, swapped for wrappedLuggagePrefab.")]
    [SerializeField] private bool wrapsAnyLuggage;

    [Tooltip("Legacy only: the prefab a Fragile bag is swapped for. Unused when wrapsAnyLuggage is on.")]
    [SerializeField] private GameObject wrappedLuggagePrefab;

    public override bool CanAccept(Luggage luggage)
    {
        if (luggage == null) return false;

        return wrapsAnyLuggage
            ? !luggage.IsWrapped
            : luggage.behaviorType == LuggageBehaviorType.Fragile;
    }

    protected override Luggage OnProcessComplete(Luggage luggage)
    {
        if (!wrapsAnyLuggage)
            return luggage.ConvertToWrapped(wrappedLuggagePrefab);

        luggage.MarkWrapped();
        return luggage;
    }
}
