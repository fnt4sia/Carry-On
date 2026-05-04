using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Place this in every stage scene.
/// Add up to 4 child GameObjects named "SpawnPoint_1" through "SpawnPoint_4"
/// and assign them to the spawnPoints array. On Start it moves each persisted
/// player to their matching spawn position.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints; // assign 1–4 in inspector

    private void Start()
    {
        var players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) => a.playerIndex.CompareTo(b.playerIndex));

        for (int i = 0; i < players.Length; i++)
        {
            if (i >= spawnPoints.Length || spawnPoints[i] == null)
            {
                Debug.LogWarning($"PlayerSpawner: no spawn point for player {i + 1}, using point 0");
                players[i].transform.position = spawnPoints[0].position;
                players[i].transform.rotation = spawnPoints[0].rotation;
                continue;
            }

            players[i].transform.position = spawnPoints[i].position;
            players[i].transform.rotation = spawnPoints[i].rotation;
        }
    }
}
