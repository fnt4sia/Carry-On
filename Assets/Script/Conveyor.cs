using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Conveyor : MonoBehaviour
{

    [SerializeField] private Vector3 moveDirection;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float moveSpeedMax;
    [SerializeField] private bool isFreezeRotation;

    private void OnTriggerEnter(Collider other)
    {
        if (!isFreezeRotation) return;

        if (other.CompareTag("Luggage"))
        {
            Rigidbody luggageRb = other.GetComponentInParent<Rigidbody>();
            luggageRb.constraints = RigidbodyConstraints.FreezeRotationX ;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Luggage"))
        {
            Rigidbody luggageRb = other.GetComponentInParent<Rigidbody>();
            if (luggageRb.velocity.magnitude < moveSpeedMax)
            {
                luggageRb.AddForce(moveDirection * moveSpeed, ForceMode.Acceleration);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Luggage"))
        {
            Rigidbody luggageRb = other.GetComponentInParent<Rigidbody>();
            luggageRb.constraints = RigidbodyConstraints.None;
        }
    }
}
