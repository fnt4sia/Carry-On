using UnityEngine;

// Delivery gate at the airplane. When luggage enters, resolves its score
// (correct delivery / missing-process penalty / bomb penalty), credits the last
// grabber via GameManager, and despawns the luggage.
public class Gate : MonoBehaviour
{
    [SerializeField, Min(1)] private int gateNumber = 1;

    public int GateNumber => gateNumber;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Luggage")) return;

        Luggage luggage = other.GetComponentInParent<Luggage>();
        if (luggage == null || luggage.IsDelivered) return;

        luggage.IsDelivered = true;

        int playerIndex = luggage.GetLastGrabber() != null ? luggage.GetLastGrabber().GetPlayerIndex() : -1;
        int delta = ResolveScore(luggage);

        GameManager.Instance.AddScore(delta);
        if (playerIndex >= 0)
            GameManager.Instance.AddPlayerScore(playerIndex, delta);

        luggage.DestroyLuggage();
    }

    private int ResolveScore(Luggage luggage)
    {
        GameManager gm = GameManager.Instance;

        if (luggage.IsBomb)
            return gm.GetBombDeliveredPenalty();

        if (luggage.HasDestinationGate && luggage.DestinationGateNumber != gateNumber)
            return gm.GetWrongGatePenalty();

        bool missingWash = luggage.RequiresWashing && !luggage.IsWashed;
        bool missingWrap = luggage.RequiresWrapping && !luggage.IsWrapped;
        if (missingWash || missingWrap)
            return gm.GetMissingProcessPenalty();

        return gm.GetCorrectDeliveryScore();
    }
}
