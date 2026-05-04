using UnityEngine;

public class Scanner : MachineStation
{
    public override bool CanAccept(Luggage luggage) => luggage != null;

    protected override void OnProcessComplete(Luggage luggage)
        => luggage.MarkScanned();
}
