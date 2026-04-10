using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MapMover : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float rotationSpeed = 720f;
    public float detectRadius = 2f;

    private LevelNode currentNode;
    private readonly List<GameObject> hiddenPlayers = new();
    private bool loading = false;

    void Start()
    {
        // Hide all player GameObjects while in ChooseStage
        PlayerInput[] players = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            p.gameObject.SetActive(false);
            hiddenPlayers.Add(p.gameObject);
        }
    }

    void Update()
    {
        if (loading) return;
        Move();
        DetectNode();
        TryEnterLevel();
    }

    void Move()
    {
        float h = 0f, v = 0f;

        if (Keyboard.current.wKey.isPressed) v += 1f;
        if (Keyboard.current.sKey.isPressed) v -= 1f;
        if (Keyboard.current.dKey.isPressed) h += 1f;
        if (Keyboard.current.aKey.isPressed) h -= 1f;

        Vector2 input = new Vector2(h, v);
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Free movement — no car steering, same feel as PlayerMovement
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        // Rotate to face movement direction instantly (or slerp for smoothness)
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
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
        if (!Keyboard.current.enterKey.wasPressedThisFrame) return;

        loading = true;

        // Re-activate all players before entering the stage
        foreach (var p in hiddenPlayers)
            if (p != null) p.SetActive(true);

        SceneManager.LoadScene(currentNode.levelIndex);
    }
}
