using UnityEngine;

public enum LuggageBehaviorType
{
    Normal,
    Sticky,
    Fragile,
    Bomb
}

[CreateAssetMenu(fileName = "New Luggage Data", menuName = "Carry On/Luggage Data")]
public class LuggageData : ScriptableObject
{
    public LuggageBehaviorType behaviorType;
    public GameObject prefab;
}
