using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class MultiplayerCamera : MonoBehaviour
{
    [SerializeField] private List<Transform> players;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float smoothTime;
    [SerializeField] private float zoomLimiter;
    [SerializeField] private float yMinimum;
    [SerializeField] private float yMaximum;
    [SerializeField] private float minimumCameraOffset;
    [SerializeField] private float maximumCameraOffset;

    private Vector3 velocity;
    private Vector3 offset;
    private Vector3 newPosition;
    private Vector3 targetPosition;
    private float distance;
    private float newY;
    private float smoothOffset;
    private void Start()
    {
        Move();
    }

    void LateUpdate()
    {
        if (players.Count == 0) return;

        Move();
    }

    void Move()
    {
        Vector3 centerPoint = GetCenterPoint();

        distance = GetGreatestDistance();

        newY = Mathf.Lerp(yMinimum, yMaximum, distance / zoomLimiter);
        newY = Mathf.Clamp(newY, yMinimum, yMaximum);

        smoothOffset = Mathf.Lerp(minimumCameraOffset, maximumCameraOffset, Mathf.InverseLerp(yMinimum, yMaximum, newY));

        offset = new(smoothOffset, 0, smoothOffset);

        newPosition = new(centerPoint.x, newY, centerPoint.z);
        targetPosition = newPosition + offset;

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
