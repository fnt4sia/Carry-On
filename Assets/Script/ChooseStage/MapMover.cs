using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MapMover : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float rotationSpeed = 150f;
    public float detectRadius = 2f;

    private LevelNode currentNode;
    private List<GameObject> hiddenPlayers = new List<GameObject>();

    void Start()
    {
        PlayerInput[] players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            p.gameObject.SetActive(false);
            hiddenPlayers.Add(p.gameObject);
        }
    }

    void Update()
    {
        MovePlane();
        DetectNode();
        TryEnterLevel();
    }

    void MovePlane()
    {
        float forwardInput = 0f;
        float turnInput = 0f;

        // Only allow moving forward
        if (Keyboard.current.wKey.isPressed) forwardInput = 1f;

        // A and D for steering
        if (Keyboard.current.aKey.isPressed) turnInput = -1f;
        if (Keyboard.current.dKey.isPressed) turnInput = 1f;

        // Only allow steering if we are actually moving forward
        if (forwardInput > 0.1f)
        {
            // Safely rotate only the Y axis
            float newAngle = transform.eulerAngles.y + (turnInput * rotationSpeed * Time.deltaTime);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, newAngle, transform.eulerAngles.z);
        }

        transform.position += transform.forward * forwardInput * moveSpeed * Time.deltaTime;
    }

    void DetectNode()
    {
        currentNode = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius);

        foreach (var hit in hits)
        {
            LevelNode node = hit.GetComponent<LevelNode>();

            if (node != null)
            {
                currentNode = node;
                break;
            }
        }
    }

    void TryEnterLevel()
    {
        if (currentNode == null) return;

        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            foreach (var p in hiddenPlayers)
            {
                if (p != null) p.SetActive(true);
            }
            SceneManager.LoadScene(currentNode.levelIndex);
        }
    }
}