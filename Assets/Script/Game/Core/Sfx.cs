public static class Sfx
{
    public const string PlayerDash = "Player Dash";
    public const string PlayerThrowBuildup = "Player Throw buildup";
    public const string PlayerThrow = "Player Throw throwonly";
    public const string Score = "Score";
    public const string Wrong = "wrong";
    public const string Star = "Star";
    public const string Stamp = "Stamp";
    public const string Start = "Start";
    public const string TimesUp = "Time's Up";
    public const string ButtonSelect = "Button Select";

    public const string GroundLuggageCollision1 = "groundLuggage Collision1";
    public const string GroundLuggageCollision2 = "groundLuggage Collision2";
    public const string GroundLuggageCollision3 = "groundLuggage Collision3";
    public const string WindowLuggageCollision1 = "windowLuggage Collision1";
    public const string WindowLuggageCollision2 = "windowLuggage Collision2";
    public const string WindowLuggageCollision3 = "windowLuggage Collision3";

    public static string LuggageCollision(bool windowLike, int tier)
    {
        return (windowLike, tier) switch
        {
            (true, 1) => WindowLuggageCollision1,
            (true, 2) => WindowLuggageCollision2,
            (true, _) => WindowLuggageCollision3,
            (false, 1) => GroundLuggageCollision1,
            (false, 2) => GroundLuggageCollision2,
            _ => GroundLuggageCollision3
        };
    }
}
