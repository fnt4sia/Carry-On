using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Simple water hazard: touching it temporarily removes the player, then respawns
// them at the level's PlayerSpawner point after a short delay.
[DisallowMultipleComponent]
public class PoolHazard : MonoBehaviour
{
    [SerializeField, Min(0f)] private float respawnDelay = 5f;
    [SerializeField] private Transform fallbackRespawnPoint;

    private readonly HashSet<PlayerInput> respawningPlayers = new HashSet<PlayerInput>();

    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (PlayerInput player in respawningPlayers)
            if (player != null)
                player.gameObject.SetActive(true);
        respawningPlayers.Clear();
    }

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerInput player = other.GetComponentInParent<PlayerInput>();
        if (player == null || respawningPlayers.Contains(player))
            return;

        StartCoroutine(RespawnPlayer(player));
    }

    private IEnumerator RespawnPlayer(PlayerInput player)
    {
        respawningPlayers.Add(player);

        PlayerGrab grab = player.GetComponent<PlayerGrab>();
        if (grab != null)
            grab.Drop(forceRelease: true);

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        player.gameObject.SetActive(false);
        yield return new WaitForSeconds(respawnDelay);

        PlayerSpawner spawner = PlayerSpawner.Instance != null
            ? PlayerSpawner.Instance
            : FindFirstObjectByType<PlayerSpawner>();

        if (spawner != null)
        {
            spawner.MovePlayerToSpawn(player);
        }
        else if (fallbackRespawnPoint != null)
        {
            player.transform.position = fallbackRespawnPoint.position;
            player.transform.rotation = fallbackRespawnPoint.rotation;
            player.transform.localScale = Vector3.one;
        }

        player.gameObject.SetActive(true);
        respawningPlayers.Remove(player);
    }
}
