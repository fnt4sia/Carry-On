using UnityEngine;

/// <summary>
/// Scene-scoped access to the current plain score model. GameManager owns the
/// model; gameplay mechanics can submit outcomes without depending on GameManager.
/// </summary>
public static class RoundScoreContext
{
    public static ScoreBoard Active { get; private set; }

    public static void Bind(ScoreBoard scoreBoard)
    {
        Active = scoreBoard;
    }

    public static void Unbind(ScoreBoard scoreBoard)
    {
        if (Active == scoreBoard)
            Active = null;
    }

    public static bool TryRecordDelivery(int playerIndex, int scoreDelta)
    {
        if (Active == null)
        {
            Debug.LogWarning("No active round score model; delivery outcome was ignored.");
            return false;
        }

        Active.RecordDelivery(playerIndex, scoreDelta);
        return true;
    }
}
