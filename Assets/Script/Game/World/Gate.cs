using UnityEngine;

public class Gate : MonoBehaviour
{
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

        bool missingWash = luggage.behaviorType.HasFlag(LuggageBehaviorType.Sticky);
        bool missingWrap = luggage.behaviorType.HasFlag(LuggageBehaviorType.Fragile);
        if (missingWash || missingWrap)
            return gm.GetMissingProcessPenalty();

        return gm.GetCorrectDeliveryScore();
    }
}
