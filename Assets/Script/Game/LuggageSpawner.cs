using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LuggageSpawner : MonoBehaviour
{
    public static LuggageSpawner Instance { get; private set; }

    [SerializeField] private List<LuggageData> luggageDataList;
    [SerializeField] private float spawnInterval;

    private int randomIndex;
    private float randomRotationY;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private Dictionary<LuggageType, Queue<GameObject>> poolDictionary = new Dictionary<LuggageType, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
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
            LuggageType lType = selectedData.luggageType;
            GameObject spawnedLuggage = null;

            if (poolDictionary.ContainsKey(lType) && poolDictionary[lType].Count > 0)
            {
                spawnedLuggage = poolDictionary[lType].Dequeue();
                spawnedLuggage.transform.position = spawnPosition;
                spawnedLuggage.transform.rotation = spawnRotation;
                spawnedLuggage.SetActive(true);
            }
            else
            {
                spawnedLuggage = Instantiate(selectedData.prefab, spawnPosition, spawnRotation);
            }
            
            Luggage luggage = spawnedLuggage.GetComponent<Luggage>();
            if (luggage != null)
            {
                luggage.luggageType = lType;
            }
        }

        yield return new WaitForSeconds(spawnInterval);
        StartCoroutine(SpawnLuggage());
    }

    public static void ReturnLuggage(Luggage luggage)
    {
        if (Instance == null)
        {
            Destroy(luggage.gameObject);
            return;
        }

        luggage.DropAllGrabbers();

        Rigidbody rb = luggage.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        luggage.ActiveConveyor = null;
        luggage.gameObject.SetActive(false);

        LuggageType lType = luggage.luggageType;
        if (!Instance.poolDictionary.ContainsKey(lType))
        {
            Instance.poolDictionary[lType] = new Queue<GameObject>();
        }

        Instance.poolDictionary[lType].Enqueue(luggage.gameObject);
    }
}
