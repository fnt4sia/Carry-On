using UnityEngine;

public class Wrapper : MachineStation
{
    public override bool CanAccept(Luggage luggage)
        => luggage != null && luggage.behaviorType == LuggageBehaviorType.Fragile;

    protected override void OnProcessComplete(Luggage luggage)
        => luggage.MarkWrapped();
}
