using UnityEngine;

public enum LuggageType
{
    Luggage,
    CampingBag,
    IkeaBag,
    GunBoxes,
}

public enum WeightClass
{
    Light,
    Medium,
    Heavy
}

[CreateAssetMenu(fileName = "New Luggage Data", menuName = "Carry On/Luggage Data")]
public class LuggageData : ScriptableObject
{
    public LuggageType luggageType;
    public WeightClass weightClass;
    public GameObject prefab;
}
