using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LuggageSpawner : MonoBehaviour
{
    [SerializeField] private List<GameObject> luggagePrefab;
    [SerializeField] private float spawnInterval;
    [SerializeField] private List<float> massList;
    [SerializeField] private List<Vector3> scaleList;
    [SerializeField] private int gateNumber;

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
        randomIndex = Random.Range(0, luggagePrefab.Count);

        spawnPosition = new Vector3(transform.position.x, transform.position.y, transform.position.z);
        spawnRotation = Quaternion.Euler(0, 0, 90);

        GameObject newLuggage = Instantiate(luggagePrefab[randomIndex], spawnPosition, spawnRotation);

        Transform cubeTransform = newLuggage.transform.Find("Cube");
        MeshRenderer cubeRenderer = cubeTransform.GetComponent<MeshRenderer>();
        cubeRenderer.material.color = new Color(Random.value, Random.value, Random.value);

        randomIndex = Random.Range(0, massList.Count);

        Rigidbody luggageRigidbody = newLuggage.GetComponent<Rigidbody>();
        luggageRigidbody.mass = massList[randomIndex];

        Transform luggageTransform = newLuggage.transform;
        luggageTransform.localScale = scaleList[randomIndex];

        Luggage luggage = newLuggage.GetComponent<Luggage>();
        if (gateNumber == 1) luggage.gateNumber = 0;
        else luggage.gateNumber = Random.Range(1, gateNumber + 1);

        yield return new WaitForSeconds(5f);
        StartCoroutine(SpawnLuggage());
    }
}
