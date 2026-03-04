using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class LobbyPlayerPositioner : MonoBehaviour
{
    public Transform spawnCenter;
    public float spacing = 2f;
    public Camera lobbyCamera;

    private List<PlayerInput> players = new();

    public void AddPlayer(PlayerInput player)
    {
        players.Add(player);
        UpdatePositions();
    }

    private void UpdatePositions()
    {
        int count = players.Count;

        for (int i = 0; i < count; i++)
        {
            float offset = (i - (count - 1) / 2f) * spacing;

            Vector3 pos = spawnCenter.position + new Vector3(offset, 0, 0);
            players[i].transform.position = pos;

            FaceCamera(players[i].transform);
        }
    }

    private void FaceCamera(Transform player)
    {
        Vector3 lookDir = lobbyCamera.transform.position - player.position;
        lookDir.y = 0;

        player.rotation = Quaternion.LookRotation(-lookDir);
    }
}