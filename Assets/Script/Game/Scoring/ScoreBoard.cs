using System;

/// <summary>
/// Plain C# score state. It has no scene or UI dependency and can be unit-tested
/// without entering Play Mode. The score is the team's: there is no per-player tally.
/// </summary>
public class ScoreBoard
{
    public event Action<int> ScoreChanged;
    public event Action<int> DeliveryCountChanged;
    public event Action<int> ScoreApplied;

    public int Score { get; private set; }
    public int DeliveryCount { get; private set; }

    public void Reset()
    {
        Score = 0;
        DeliveryCount = 0;
        ScoreChanged?.Invoke(Score);
        DeliveryCountChanged?.Invoke(DeliveryCount);
    }

    public void ApplyScore(int delta)
    {
        Score = Math.Max(0, Score + delta);
        ScoreChanged?.Invoke(Score);
        ScoreApplied?.Invoke(delta);
    }

    public void RecordDelivery(int scoreDelta)
    {
        DeliveryCount++;
        DeliveryCountChanged?.Invoke(DeliveryCount);
        ApplyScore(scoreDelta);
    }
}
