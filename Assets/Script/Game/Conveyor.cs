using System.Collections.Generic;
using UnityEngine;

public class Conveyor : MonoBehaviour
{
    [Header("Conveyor Settings")]
    [SerializeField] private Vector3 moveDirection = Vector3.forward;

    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("How smoothly the luggage steers. Higher = tighter corner, Lower = slides wide.")]
    [SerializeField] private float turnSmoothness = 6f;
    [SerializeField] private bool isFreezeRotation = false;

    private List<Rigidbody> rigidbodiesOnConveyor = new List<Rigidbody>();

    private void FixedUpdate()
    {
        for (int i = rigidbodiesOnConveyor.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = rigidbodiesOnConveyor[i];
            
            if (rb == null || !rb.gameObject.activeInHierarchy)
            {
                rigidbodiesOnConveyor.RemoveAt(i);
                continue;
            }

            Luggage luggage = rb.GetComponent<Luggage>();
            if (luggage != null)
            {
                if (luggage.GetIsGrabbed())
                {
                    if (luggage.ActiveConveyor == this)
                    {
                        luggage.ActiveConveyor = null;
                        rb.isKinematic = false;
                    }
                    continue;
                }

                if (luggage.ActiveConveyor == null)
                {
                    luggage.ActiveConveyor = this;
                    luggage.kinematicVelocity = rb.linearVelocity;
                    rb.isKinematic = true;
                }
                else if (luggage.ActiveConveyor != this)
                {
                    continue;
                }
            }

            // Ideal velocity
            Vector3 targetVelocity = moveDirection.normalized * moveSpeed;

            luggage.kinematicVelocity = Vector3.Lerp(luggage.kinematicVelocity, targetVelocity, Time.fixedDeltaTime * turnSmoothness);
            


            Vector3 movementVector = luggage.kinematicVelocity * Time.fixedDeltaTime;

            Vector3 nextPos = rb.position + movementVector;
            nextPos.y = rb.position.y;
            
            rb.MovePosition(nextPos);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Luggage"))
        {
            Rigidbody luggageRb = other.GetComponentInParent<Rigidbody>();
            if (luggageRb != null)
            {
                if (!rigidbodiesOnConveyor.Contains(luggageRb))
                {
                    rigidbodiesOnConveyor.Add(luggageRb);
                    if (isFreezeRotation)
                    {
                        luggageRb.constraints = RigidbodyConstraints.FreezeRotationX;
                    }
                }
                
                Luggage luggage = luggageRb.GetComponent<Luggage>();
                if (luggage != null && !luggage.GetIsGrabbed()) 
                {
                    luggage.ActiveConveyor = this;

                    // Only rip velocity if it wasn't mathematically transferring from another conveyor
                    if (!luggageRb.isKinematic)
                    {
                        luggage.kinematicVelocity = luggageRb.linearVelocity;
                        luggageRb.isKinematic = true;
                    }
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Luggage"))
        {
            Rigidbody luggageRb = other.GetComponentInParent<Rigidbody>();


            if (luggageRb != null)
            {
                rigidbodiesOnConveyor.Remove(luggageRb);
                
                Luggage luggage = luggageRb.GetComponent<Luggage>();
                if (luggage != null && luggage.ActiveConveyor == this)
                {
                    luggage.ActiveConveyor = null;
                    luggageRb.isKinematic = false;
                    
                    // Return the math momentum back to the physics engine!
                    luggageRb.linearVelocity = luggage.kinematicVelocity;
                }

                if (isFreezeRotation)
                {
                    luggageRb.constraints = RigidbodyConstraints.None;
                }
            }
        }
    }
}
