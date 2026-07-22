using UnityEngine;

// Delivery gate at the airplane. When luggage enters, resolves its score
// (correct delivery / missing-process penalty / wrong-gate penalty), credits the last
// grabber through the scene's plain score model, and despawns the luggage.
public class Gate : MonoBehaviour
{
    [SerializeField, Min(1)] private int gateNumber = 1;

    public int GateNumber => gateNumber;

    private void OnTriggerEnter(Collider other)
    {
        if (!Luggage.TryGetFromCollider(other, out Luggage luggage))
            return;
        if (luggage == null || luggage.IsDelivered) return;

        int playerIndex = luggage.GetLastGrabber() != null ? luggage.GetLastGrabber().GetPlayerIndex() : -1;
        LevelConfig config = LevelContext.CurrentConfig;
        if (config == null)
        {
            Debug.LogError($"{nameof(Gate)} '{name}' needs an active {nameof(LevelContext)}.", this);
            return;
        }

        int delta = ResolveScore(luggage, config);
        if (!RoundScoreContext.TryRecordDelivery(playerIndex, delta))
            return;

        luggage.IsDelivered = true;
        luggage.DestroyLuggage();
    }

    private int ResolveScore(Luggage luggage, LevelConfig config)
    {
        LuggageScoreState state = new(
            luggage.RequiresWashing,
            luggage.IsWashed,
            luggage.RequiresWrapping,
            luggage.IsWrapped,
            luggage.HasDestinationGate,
            luggage.DestinationGateNumber);

        return ScoringRules.Resolve(state, config, gateNumber);
    }
}
