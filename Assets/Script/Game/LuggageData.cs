using UnityEngine;

public enum LuggageType
{
    Luggage,
    CampingBag,
    IkeaBag,
    GunBoxes,
}


[CreateAssetMenu(fileName = "New Luggage Data", menuName = "Carry On/Luggage Data")]
public class LuggageData : ScriptableObject
{
    public LuggageType luggageType;
    public GameObject prefab;
}
