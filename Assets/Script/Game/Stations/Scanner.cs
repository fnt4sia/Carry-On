using UnityEngine;

// Station that accepts any luggage and marks it as scanned (bomb detection).
// Does not modify the luggage's behavior — just records that it's been checked,
// which other systems can read via Luggage.IsScanned.
public class Scanner : MachineStation
{
    public override bool CanAccept(Luggage luggage) => luggage != null;

    protected override Luggage OnProcessComplete(Luggage luggage)
    {
        luggage.MarkScanned();
        return luggage;
    }
}
