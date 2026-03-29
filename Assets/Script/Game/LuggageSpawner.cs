using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LuggageSpawner : MonoBehaviour
{
    [SerializeField] private List<LuggageData> luggageDataList;
    [SerializeField] private float spawnInterval;

    private int randomIndex;
    private float randomRotationY;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    void Start()
    {
        StartCoroutine(SpawnLuggage());
    }

    private IEnumerator SpawnLuggage()
    {
        if (luggageDataList == null || luggageDataList.Count == 0) yield return null;

        int randomIndexData = Random.Range(0, luggageDataList.Count);
        LuggageData selectedData = luggageDataList[randomIndexData];
        
        spawnPosition = new Vector3(transform.position.x, transform.position.y, transform.position.z);
        spawnRotation = Quaternion.Euler(0, 0, 90);

        if (selectedData.prefab != null)
        {
            GameObject newLuggage = Instantiate(selectedData.prefab, spawnPosition, spawnRotation);
            
            Luggage luggage = newLuggage.GetComponent<Luggage>();
            luggage.luggageType = selectedData.luggageType;
        }

        yield return new WaitForSeconds(spawnInterval);
        StartCoroutine(SpawnLuggage());
    }
}
