using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MapMover : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float detectRadius = 2f;

    private LevelNode currentNode;

    void Update()
    {
        MovePlane();
        DetectNode();
        TryEnterLevel();
    }

    void MovePlane()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) input.y += 1;
        if (Keyboard.current.sKey.isPressed) input.y -= 1;
        if (Keyboard.current.aKey.isPressed) input.x -= 1;
        if (Keyboard.current.dKey.isPressed) input.x += 1;

        Vector3 move = new Vector3(input.x, 0, input.y);

        transform.position += move * moveSpeed * Time.deltaTime;
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
            SceneManager.LoadScene(currentNode.levelIndex);
        }
    }
}