using UnityEngine;

// Station that processes Fragile luggage. Accepts only fragile luggage; on completion
// calls Luggage.ConvertToWrapped which swaps it for the wrapped-luggage prefab.
public class Wrapper : MachineStation
{
    [SerializeField] private GameObject wrappedLuggagePrefab;

    public override bool CanAccept(Luggage luggage)
        => luggage != null && luggage.behaviorType == LuggageBehaviorType.Fragile;

    protected override Luggage OnProcessComplete(Luggage luggage)
        => luggage.ConvertToWrapped(wrappedLuggagePrefab);
}
