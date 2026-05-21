using UnityEngine;

// Station that processes Sticky luggage. Accepts only sticky luggage; on completion
// calls Luggage.ConvertToWashed which swaps it for the clean washed-luggage prefab.
public class WashingMachine : MachineStation
{
    [SerializeField] private GameObject washedLuggagePrefab;

    public override bool CanAccept(Luggage luggage)
        => luggage != null && luggage.behaviorType == LuggageBehaviorType.Sticky;

    protected override Luggage OnProcessComplete(Luggage luggage)
        => luggage.ConvertToWashed(washedLuggagePrefab);
}
