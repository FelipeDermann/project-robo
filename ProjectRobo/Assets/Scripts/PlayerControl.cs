using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : MonoBehaviour
{
    [Header("Player movement")]
    public float moveSpeed;
    public float turnSmoothTime;
    public float jumpHeight;
    public float gravity;
    public float minimumGravity;
    public bool moving;

    [Header("Ground detection")]
    public bool isGrounded;
    public Transform groundCheckPos;
    public float groundCheckRadius;
    public LayerMask groundMask;

    [Header("Misc. References")]
    public CharacterController controller;
    public Transform cam;

    Vector2 inputDirection;
    public Vector3 moveDirection;
    float turnSmoothVelocity;

    PlayerInputs inputs;

    void Awake()
    {
        inputs = new PlayerInputs();

        inputs.Gameplay.Jump.performed += ctx => Jump();

        inputs.Gameplay.Movement.performed += ctx => inputDirection = ctx.ReadValue<Vector2>();
        inputs.Gameplay.Movement.performed += ctx => moving = true;
        inputs.Gameplay.Movement.canceled += ctx => moving = false;
    }

    // Update is called once per frame
    void Update()
    {
        GroundCheck();
        Move();
        controller.Move(moveDirection * Time.deltaTime);
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
        if (moving)
        {
            //turn character to movement direction and make it move according to camera dir
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.y) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            moveDir.Normalize();
            moveDirection = new Vector3(moveDir.x * moveSpeed, moveDirection.y, moveDir.z * moveSpeed);
        }
        else moveDirection = new Vector3(0, moveDirection.y,0);
    }

    void GroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheckPos.position, groundCheckRadius, groundMask);

        if (isGrounded && moveDirection.y < 0)
        {
            moveDirection.y = minimumGravity;
        }

        moveDirection.y += gravity * Time.deltaTime;
    }

    void Jump()
    {
        Debug.Log("JUMP PRESSED");
        if (isGrounded)
        {
            moveDirection.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
}
