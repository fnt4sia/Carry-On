using UnityEngine;

// Trigger volume that catches any luggage that falls off the level or escapes the
// playfield and returns it to LuggageSpawner's pool (or destroys it as a fallback).
public class LuggageSink : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Luggage"))
        {
            Luggage luggage = other.GetComponentInParent<Luggage>();
            if (luggage != null)
            {
                LuggageSpawner.ReturnLuggage(luggage);
            }
            else
            {
                Destroy(other.gameObject);
            }
        }
    }
}
