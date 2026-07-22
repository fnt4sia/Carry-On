using System.Collections;
using UnityEngine;

/// <summary>
/// Low-frequency decorative airplane loop. Reuses one instance and remains entirely
/// separate from round rules so its prototype art can be swapped on the prefab.
/// </summary>
public class AmbientAirplaneSpawner : MonoBehaviour
{
    [SerializeField] private GameObject airplanePrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform destinationPoint;
    [SerializeField, Min(0.1f)] private float checkInterval = 1f;
    [SerializeField, Range(0f, 1f)] private float spawnChancePerCheck = 1f / 14f;
    [SerializeField] private Vector2 speedRange = new(10f, 40f);

    private GameObject pooledAirplane;

    private void Awake()
    {
        if (spawnPoint != null && destinationPoint != null)
            return;

        Debug.LogError($"{nameof(AmbientAirplaneSpawner)} '{name}' needs path anchors.", this);
        enabled = false;
    }

    private IEnumerator Start()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(checkInterval);
            if (GameManager.Instance == null || !GameManager.Instance.IsRoundStarted || GameManager.Instance.IsRoundEnded)
                continue;
            if (Random.value > spawnChancePerCheck)
                continue;

            GameObject airplane = GetAirplane();
            if (airplane == null)
                continue;

            float speed = Random.Range(speedRange.x, speedRange.y);
            while (airplane.activeSelf && Vector3.Distance(airplane.transform.position, destinationPoint.position) > 0.1f)
            {
                airplane.transform.position = Vector3.MoveTowards(
                    airplane.transform.position,
                    destinationPoint.position,
                    speed * Time.deltaTime);
                yield return null;
            }

            airplane.SetActive(false);
        }
    }

    private GameObject GetAirplane()
    {
        if (airplanePrefab == null)
            return null;

        if (pooledAirplane == null)
            pooledAirplane = Instantiate(airplanePrefab, transform);

        pooledAirplane.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        pooledAirplane.SetActive(true);
        return pooledAirplane;
    }
}
