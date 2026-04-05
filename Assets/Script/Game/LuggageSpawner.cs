using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LuggageSpawner : MonoBehaviour
{
    public static LuggageSpawner Instance { get; private set; }

    [SerializeField] private List<LuggageData> luggageDataList;
    [SerializeField] private float spawnInterval;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private Dictionary<LuggageData, Queue<GameObject>> poolDictionary = new Dictionary<LuggageData, Queue<GameObject>>();

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

        spawnPosition = transform.position;
        spawnRotation = Quaternion.Euler(0, 0, 90);

        if (selectedData.prefab != null)
        {
            GameObject spawnedLuggage = null;

            if (poolDictionary.ContainsKey(selectedData) && poolDictionary[selectedData].Count > 0)
            {
                spawnedLuggage = poolDictionary[selectedData].Dequeue();
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
                luggage.data = selectedData;

                LuggageBehaviorType[] types = { LuggageBehaviorType.Normal, LuggageBehaviorType.Fragile, LuggageBehaviorType.Sticky };
                luggage.behaviorType = types[Random.Range(0, types.Length)];
                luggage.ApplyBehaviorVisual();
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

        LuggageData key = luggage.data;
        if (key == null)
        {
            Destroy(luggage.gameObject);
            return;
        }

        if (!Instance.poolDictionary.ContainsKey(key))
        {
            Instance.poolDictionary[key] = new Queue<GameObject>();
        }

        Instance.poolDictionary[key].Enqueue(luggage.gameObject);
    }
}
