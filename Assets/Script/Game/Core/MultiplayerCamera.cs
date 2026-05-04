using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MultiplayerCamera : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float smoothTime;
    [SerializeField] private float zoomLimiter;
    [SerializeField] private float yMinimum;
    [SerializeField] private float yMaximum;
    [SerializeField] private float minimumCameraOffset;
    [SerializeField] private float maximumCameraOffset;

    private readonly List<Transform> players = new();
    private Vector3 velocity;

    private void Start()
    {
        FindAllPlayers();
        Move();
    }

    private void FindAllPlayers()
    {
        players.Clear();
        var allPlayers = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        System.Array.Sort(allPlayers, (a, b) => a.playerIndex.CompareTo(b.playerIndex));
        foreach (var p in allPlayers)
            players.Add(p.transform);
    }

    void LateUpdate()
    {
        if (players.Count == 0) return;

        // Remove any destroyed player references
        players.RemoveAll(p => p == null);

        Move();
    }

    void Move()
    {
        if (players.Count == 0) return;

        Vector3 centerPoint = GetCenterPoint();
        float distance = GetGreatestDistance();

        float newY = Mathf.Lerp(yMinimum, yMaximum, distance / zoomLimiter);
        newY = Mathf.Clamp(newY, yMinimum, yMaximum);

        float smoothOffset = Mathf.Lerp(minimumCameraOffset, maximumCameraOffset,
            Mathf.InverseLerp(yMinimum, yMaximum, newY));

        Vector3 offset = new(smoothOffset, 0, smoothOffset);
        Vector3 targetPosition = new Vector3(centerPoint.x, newY, centerPoint.z) + offset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }

    Vector3 GetCenterPoint()
    {
        if (players.Count == 1)
            return players[0].position;

        Bounds bounds = new(players[0].position, Vector3.zero);
        foreach (Transform t in players)
            bounds.Encapsulate(t.position);

        return bounds.center;
    }

    float GetGreatestDistance()
    {
        if (players.Count == 1) return 0f;

        Bounds bounds = new(players[0].position, Vector3.zero);
        foreach (Transform t in players)
            bounds.Encapsulate(t.position);

        return bounds.size.magnitude;
    }
}
