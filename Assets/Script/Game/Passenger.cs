using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody))]
public class Passenger : MonoBehaviour
{
    public bool needsLuggage;
    public LuggageType requestedLuggage;
    // public GameObject requestIconUI;

    [Header("Wander Settings")]
    public float wanderRadius = 10f;
    public float waitMin = 2f;
    public float waitMax = 5f;

    [Header("Luggage Request Settings")]
    [Range(0f, 1f)] public float luggageRequestChance = 0.5f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private bool isWaiting;
    
    // To handle physics vs navmesh collision
    private bool isPushed;
    private float pushRecoverTimer;

    // To store original color
    private Renderer[] renderers;
    private Color originalColor = Color.white;
    private bool colorSaved = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // Kinematic while navmesh is driving
        
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0 && renderers[0].material.HasProperty("_Color"))
        {
            originalColor = renderers[0].material.color;
            colorSaved = true;
        }
    }

    private void Start()
    {
        DetermineLuggageNeed();
        SetNewWanderDestination();
    }

    private void DetermineLuggageNeed()
    {
        needsLuggage = Random.value <= luggageRequestChance;

        if (needsLuggage)
        {
            System.Array values = System.Enum.GetValues(typeof(LuggageType));
            if (values.Length > 0)
            {
                requestedLuggage = (LuggageType)values.GetValue(Random.Range(0, values.Length));
            }
            // if (requestIconUI != null) requestIconUI.SetActive(true);
            SetColor(Color.red);
        }
        else
        {
            // if (requestIconUI != null) requestIconUI.SetActive(false);
            if (colorSaved) SetColor(originalColor);
        }
    }

    private void SetColor(Color col)
    {
        foreach (Renderer r in renderers)
        {
            if (r.material.HasProperty("_Color"))
                r.material.color = col;
        }
    }

    private void Update()
    {
        HandlePhysicsRecovery();

        if (isPushed) return; // Don't wander while being pushed

        if (!isWaiting && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                StartCoroutine(WaitAndWander());
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // If the player or luggage bumps into the NPC, enable physics!
        if (collision.gameObject.GetComponentInParent<PlayerMovement>() != null || collision.gameObject.CompareTag("Luggage"))
        {
            if (!isPushed)
            {
                isPushed = true;
                if (agent.enabled) agent.enabled = false;
                rb.isKinematic = false;
            }
            pushRecoverTimer = 1f; // Reset pushing timer
        }

        // Check if the object colliding is luggage and we need luggage
        if (needsLuggage && collision.gameObject.CompareTag("Luggage"))
        {
            TryAcceptLuggage(collision.collider);
        }
    }

    private void TryAcceptLuggage(Collider luggageCollider)
    {
        Luggage luggage = luggageCollider.GetComponentInParent<Luggage>();
        if (luggage == null || luggage.IsDelivered) return;

        luggage.IsDelivered = true;
        int playerIndex = luggage.GetLastGrabber() != null ? luggage.GetLastGrabber().GetPlayerIndex() : -1;

        if (luggage.luggageType == requestedLuggage)
        {
            GameManager.Instance.AddScore(15);
            if (playerIndex == 0) GameManager.Instance.AddPlayer1Score(15);
            else if (playerIndex == 1) GameManager.Instance.AddPlayer2Score(15);
            
            // Satisfied
            needsLuggage = false;
            if (colorSaved) SetColor(originalColor);
        }
        else
        {
            GameManager.Instance.AddScore(-10);
            if (playerIndex == 0) GameManager.Instance.AddPlayer1Score(-10);
            else if (playerIndex == 1) GameManager.Instance.AddPlayer2Score(-10);
            
            needsLuggage = false;
            if (colorSaved) SetColor(originalColor);
        }

        luggage.DestroyLuggage();
    }

    private void HandlePhysicsRecovery()
    {
        if (isPushed)
        {
            if (rb.linearVelocity.magnitude < 0.1f)
            {
                pushRecoverTimer -= Time.deltaTime;
                if (pushRecoverTimer <= 0f)
                {
                    isPushed = false;
                    rb.isKinematic = true;
                    
                    // Stand back up if they fell over
                    transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
                    
                    // Re-align perfectly onto NavMesh so they don't get stuck mid-air
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    {
                        transform.position = hit.position;
                    }

                    agent.enabled = true;
                    SetNewWanderDestination();
                }
            }
            else if (rb.linearVelocity.magnitude > 0.5f)
            {
                pushRecoverTimer = 1f; // Re-reset the timer as long as they are moving fast
            }
        }
    }

    private IEnumerator WaitAndWander()
    {
        isWaiting = true;
        yield return new WaitForSeconds(Random.Range(waitMin, waitMax));
        if (!isPushed)
        {
            SetNewWanderDestination();
        }
        isWaiting = false;
    }

    private void SetNewWanderDestination()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;
        NavMeshHit hit;
        
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }
}
