using UnityEngine;

public class WashingMachine : MachineStation
{
    public override bool CanAccept(Luggage luggage)
        => luggage != null && luggage.behaviorType == LuggageBehaviorType.Sticky;

    protected override void OnProcessComplete(Luggage luggage)
        => luggage.MarkWashed();
}
