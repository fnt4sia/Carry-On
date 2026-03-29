using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PassengerManager : MonoBehaviour
{
    [SerializeField] private GameObject passengerPrefab;
    [SerializeField] private Transform[] spawnZones; // Empty GameObjects where a passenger can spawn/enter
    [SerializeField] private int maxPassengers = 15;
    [SerializeField] private float spawnIntervalMin = 8f;
    [SerializeField] private float spawnIntervalMax = 15f;

    private List<GameObject> activePassengers = new List<GameObject>();

    private void Start()
    {
        if (spawnZones == null || spawnZones.Length == 0)
        {
            return;
        }

        StartCoroutine(SpawnPassengerRoutine());
    }

    private IEnumerator SpawnPassengerRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(spawnIntervalMin, spawnIntervalMax));
            
            activePassengers.RemoveAll(item => item == null);
            if (activePassengers.Count < maxPassengers)
            {
                SpawnRandomPassenger();
            }
        }
    }

    private void SpawnRandomPassenger()
    {
        if (spawnZones.Length > 0)
        {
            Transform spawnZone = spawnZones[Random.Range(0, spawnZones.Length)];
            GameObject newPassenger = Instantiate(passengerPrefab, spawnZone.position, spawnZone.rotation);
            activePassengers.Add(newPassenger);
        }
    }
}
