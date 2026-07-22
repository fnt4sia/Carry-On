public readonly struct LuggageScoreState
{
    public LuggageScoreState(
        bool requiresWashing,
        bool isWashed,
        bool requiresWrapping,
        bool isWrapped,
        bool hasDestinationGate,
        int destinationGateNumber)
    {
        RequiresWashing = requiresWashing;
        IsWashed = isWashed;
        RequiresWrapping = requiresWrapping;
        IsWrapped = isWrapped;
        HasDestinationGate = hasDestinationGate;
        DestinationGateNumber = destinationGateNumber;
    }

    public bool RequiresWashing { get; }
    public bool IsWashed { get; }
    public bool RequiresWrapping { get; }
    public bool IsWrapped { get; }
    public bool HasDestinationGate { get; }
    public int DestinationGateNumber { get; }
}

public static class ScoringRules
{
    public static int Resolve(in LuggageScoreState luggage, LevelConfig config, int gateNumber)
    {
        if (luggage.HasDestinationGate && luggage.DestinationGateNumber != gateNumber)
            return config.scoreWrongGateDelivery;

        bool missingWash = luggage.RequiresWashing && !luggage.IsWashed;
        bool missingWrap = luggage.RequiresWrapping && !luggage.IsWrapped;
        return missingWash || missingWrap
            ? config.scoreMissingProcess
            : config.scoreCorrectDelivery;
    }
}
