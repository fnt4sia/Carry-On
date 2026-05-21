using UnityEngine;
using UnityEngine.InputSystem;

// Place this in every stage scene. Add up to 4 child GameObjects named "SpawnPoint_1"
// through "SpawnPoint_4" and assign them to the spawnPoints array. On Start it moves
// each persisted player to their matching spawn position.
public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance { get; private set; }

    [SerializeField] private Transform[] spawnPoints; // assign 1–4 in inspector

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || spawnPoints[0] == null)
        {
            Debug.LogError($"{nameof(PlayerSpawner)} on {name} needs at least one spawn point.");
            return;
        }

        var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) => a.playerIndex.CompareTo(b.playerIndex));

        for (int i = 0; i < players.Length; i++)
        {
            if (i >= spawnPoints.Length || spawnPoints[i] == null)
            {
                Debug.LogWarning($"PlayerSpawner: no spawn point for player {i + 1}, using point 0");
                players[i].transform.position = spawnPoints[0].position;
                players[i].transform.rotation = spawnPoints[0].rotation;
                players[i].transform.localScale = Vector3.one;
                continue;
            }

            players[i].transform.position = spawnPoints[i].position;
            players[i].transform.rotation = spawnPoints[i].rotation;
            players[i].transform.localScale = Vector3.one;
        }
    }

    public void MovePlayerToSpawn(PlayerInput player)
    {
        if (player == null)
            return;

        Transform spawnPoint = GetSpawnPoint(player.playerIndex);
        if (spawnPoint == null)
            return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        player.transform.position = spawnPoint.position;
        player.transform.rotation = spawnPoint.rotation;
        player.transform.localScale = Vector3.one;
    }

    private Transform GetSpawnPoint(int playerIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        if (playerIndex >= 0 && playerIndex < spawnPoints.Length && spawnPoints[playerIndex] != null)
            return spawnPoints[playerIndex];

        return spawnPoints[0];
    }
}
