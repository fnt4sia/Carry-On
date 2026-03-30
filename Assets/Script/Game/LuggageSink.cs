using UnityEngine;

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
