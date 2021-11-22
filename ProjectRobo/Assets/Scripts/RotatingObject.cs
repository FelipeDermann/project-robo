using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RotationDirection
{
    Clockwise,
    CounterClockwise,
}

public class RotatingObject : MonoBehaviour
{
    [Header("Rotation Config")]
    public bool allowSpin;
    public RotationDirection direction;
    public Vector3 eulerAngleVelocity;
    public Rigidbody rb;

    Vector3 newRotation;
    float rotationToAdd;

    // Update is called once per frame
    void Update()
    {
        if (allowSpin)
        {
            //rotationToAdd = spinSpeed * Time.deltaTime;

            //if (direction == RotationDirection.Clockwise) 
            //    newRotation.y += rotationToAdd;
            //else
            //    newRotation.y -= rotationToAdd;

            Quaternion deltaRotation = Quaternion.Euler(eulerAngleVelocity * Time.fixedDeltaTime);
            rb.MoveRotation(rb.rotation * deltaRotation);
        }
    }
}
