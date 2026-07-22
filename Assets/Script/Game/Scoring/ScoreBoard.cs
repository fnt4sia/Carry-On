using System;

/// <summary>
/// Plain C# score state. It has no scene or UI dependency and can be unit-tested
/// without entering Play Mode.
/// </summary>
public class ScoreBoard
{
    private readonly int[] playerScores;
    private readonly int[] playerDeliveries;

    public ScoreBoard(int playerCapacity = 4)
    {
        playerScores = new int[playerCapacity];
        playerDeliveries = new int[playerCapacity];
    }

    public event Action<int> ScoreChanged;
    public event Action<int, int> PlayerScoreChanged;
    public event Action<int> DeliveryCountChanged;
    public event Action<int, int> PlayerDeliveryCountChanged;
    public event Action<int, int> ScoreApplied;

    public int Score { get; private set; }
    public int DeliveryCount { get; private set; }
    public int PlayerCapacity => playerScores.Length;

    public void Reset()
    {
        Score = 0;
        DeliveryCount = 0;
        Array.Clear(playerScores, 0, playerScores.Length);
        Array.Clear(playerDeliveries, 0, playerDeliveries.Length);
        PublishAll();
    }

    public void ApplyScore(int delta, int playerIndex = -1)
    {
        Score = Math.Max(0, Score + delta);
        ScoreChanged?.Invoke(Score);

        if (!IsValidPlayer(playerIndex))
        {
            ScoreApplied?.Invoke(delta, playerIndex);
            return;
        }

        playerScores[playerIndex] += delta;
        PlayerScoreChanged?.Invoke(playerIndex, playerScores[playerIndex]);
        ScoreApplied?.Invoke(delta, playerIndex);
    }

    public void RecordDelivery(int playerIndex, int scoreDelta)
    {
        DeliveryCount++;
        DeliveryCountChanged?.Invoke(DeliveryCount);

        if (IsValidPlayer(playerIndex))
        {
            playerDeliveries[playerIndex]++;
            PlayerDeliveryCountChanged?.Invoke(playerIndex, playerDeliveries[playerIndex]);
        }

        ApplyScore(scoreDelta, playerIndex);
    }

    public int GetPlayerScore(int playerIndex)
        => IsValidPlayer(playerIndex) ? playerScores[playerIndex] : 0;

    public int GetPlayerDeliveryCount(int playerIndex)
        => IsValidPlayer(playerIndex) ? playerDeliveries[playerIndex] : 0;

    public int[] CopyPlayerScores() => (int[])playerScores.Clone();
    public int[] CopyPlayerDeliveries() => (int[])playerDeliveries.Clone();

    private bool IsValidPlayer(int playerIndex)
        => playerIndex >= 0 && playerIndex < playerScores.Length;

    private void PublishAll()
    {
        ScoreChanged?.Invoke(Score);
        DeliveryCountChanged?.Invoke(DeliveryCount);
        for (int i = 0; i < playerScores.Length; i++)
        {
            PlayerScoreChanged?.Invoke(i, playerScores[i]);
            PlayerDeliveryCountChanged?.Invoke(i, playerDeliveries[i]);
        }
    }
}
