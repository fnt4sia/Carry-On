using System;
using UnityEngine;

[Flags]
public enum LuggageBehaviorType
{
    Normal  = 0,
    Sticky  = 1 << 0,
    Fragile = 1 << 1,
    Bomb    = 1 << 2
}

[CreateAssetMenu(fileName = "New Luggage Data", menuName = "Carry On/Luggage Data")]
public class LuggageData : ScriptableObject
{
    public LuggageBehaviorType behaviorType;
    public GameObject prefab;

    [Header("Lifetime")]
    [Tooltip("Seconds before the luggage expires if not delivered. Bombs explode in place; others despawn with penalty.")]
    public float lifetime = 20f;
}
