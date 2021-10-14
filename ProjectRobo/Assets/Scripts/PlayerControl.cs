using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : MonoBehaviour
{
    [Header("Player movement")]
    public float moveSpeed;
    public CharacterController controller;

    Vector2 inputDirection;
    Vector3 moveDirection;
    Vector3 moveVelocity;

    PlayerInputs inputs;

    void Awake()
    {
        inputs = new PlayerInputs();

        inputs.Gameplay.Jump.performed += ctx => Jump();

        inputs.Gameplay.Movement.performed += ctx => inputDirection = ctx.ReadValue<Vector2>();
        inputs.Gameplay.Movement.canceled += ctx => inputDirection = Vector2.zero;
    }

    // Update is called once per frame
    void Update()
    {
        Move();
    }

    void OnEnable()
    {
        inputs.Gameplay.Enable();
    }

    void OnDisable()
    {
        inputs.Gameplay.Disable();
    }

    void Move()
    {
        moveDirection.x = inputDirection.x;
        moveDirection.z = inputDirection.y;

        moveVelocity = moveDirection * moveSpeed;

        controller.Move(moveVelocity * Time.deltaTime);
    }

    void Jump()
    {
        Debug.Log("I JUMPED!!");
    }
}
