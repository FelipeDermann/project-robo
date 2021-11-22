using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class PlayerControl : MonoBehaviour
{
    [Header("Player movement")]
    [SerializeField, Range(0f, 100f)] //MAKE SURE THAT MAX SPEED AND MAX SNAP SPEED ARE NEVER THE SAME
    float maxSpeed = 10f;
    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 10f, maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 0.1f)]
    public float turnSmoothTime;
    [SerializeField, Range(0f, 10f)]
    float fixedJumpHeight = 2f; 
    [SerializeField, Range(0f, 10f)]
    float groundJumpMinJumpHeight = 1f;
    [SerializeField, Range(0f, 1000f)]
    float groundJumpPower = 80f;
    [SerializeField, Range(0f, 0.5f)]
    float groundJumpTime = 0.2f;
    [SerializeField, Range(0, 5)]
    int maxAirJumps = 0;
    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f;
    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f; //MAX SNAP SPEED
    [SerializeField, Min(0f)]
    float probeDistance = 3f;
    [SerializeField]
    LayerMask probeMask = -1;

    [Header("Player jump")]
    public int jumpPhase;
    public float gravity;
    public float maxFallingSpeed;
    public bool desiredJump;
    public bool isJumping;
    public JumpType jumpType;

    [Header("Ground detection")]
    [SerializeField]
    private int groundContactCount;
    public int steepContactCount;
    bool justLanded;
    bool IsGrounded => GroundContactCount > 0;
    bool IsOnSteep => steepContactCount > 0;
    
    public int GroundContactCount
    {
        get => groundContactCount; 
        set
        {
            //if (groundContactCount == 0 && value > 0) Debug.Log("GROUNDED");
            groundContactCount = value;
        }
    }

    public Transform feetTransform;

    [Header("Misc. References")]
    public Rigidbody body;
    public Transform cam;
    public Renderer playerMesh;

    [Header("Debug info")]
    public bool moving;
    public Vector2 inputDirection;
    public Vector3 moveDir;
    public Vector3 velocity;
    public Vector3 desiredVelocity;
    float turnSmoothVelocity;

    RaycastHit slopeHit;
    PlayerInputs inputs;

    float angle;

    float minGroundDotProduct;
    Vector3 contactNormal, steepNormal;

    int stepsSinceLastGrounded, stepsSinceLastJump;

    Vector3 jumpDirection;

    bool cachedGrounded;

    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        gravity = -Mathf.Abs(gravity);
        maxFallingSpeed = -Mathf.Abs(maxFallingSpeed);
    }

    void Awake()
    {
        inputs = new PlayerInputs();

        inputs.Gameplay.Jump.canceled += ctx => JumpInputCancelled();
        inputs.Gameplay.Jump.started += ctx => JumpInputStarted();

        inputs.Gameplay.Attack.performed += ctx => Attack();

        inputs.Gameplay.Movement.performed += ctx => inputDirection = ctx.ReadValue<Vector2>();
        inputs.Gameplay.Movement.performed += ctx => moving = true;
        inputs.Gameplay.Movement.canceled += ctx => moving = false;

        body = GetComponent<Rigidbody>();
        OnValidate();
    }

    void Attack()
    {
        Debug.Log("AAATACK!");
    }

    // Update is called once per frame
    void Update()
    {
        GetMoveDirection();

        //desiredJump |= inputs.Gameplay.Jump.triggered;

        //playerMesh.material.SetColor(
        //    "_Color", OnGround ? Color.black : Color.white
        //);
    }

    void JumpInputStarted()
    {
        desiredJump = true;
    }

    void JumpInputCancelled()
    {
        desiredJump = false;
        if (isJumping) isJumping = false;
    }

    private void FixedUpdate()
    {
        ApplyMovement();
        CheckLandingEvent();
        ClearState();
    }
    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;
        if (IsGrounded || SnapToGround() || CheckSteepContacts())
        {
            stepsSinceLastGrounded = 0;
            if (stepsSinceLastJump > 1)
            {
                jumpPhase = 0;
            }
            if (GroundContactCount > 1)
            {
                contactNormal.Normalize();
            }
        }
        else
        {
            contactNormal = Vector3.up;
        }
    }

    void ClearState()
    {
        GroundContactCount = steepContactCount = 0;
        contactNormal = steepNormal = Vector3.zero;
    }

    void CheckLandingEvent()
    {
        if (justLanded)
        {
            justLanded = false;
            body.drag = 0;
        }

        var grounded = IsGrounded;
        if (grounded && !cachedGrounded)
        {
            justLanded = true;
            body.drag = 10;
        }
        cachedGrounded = grounded;
    }

    void GetMoveDirection()
    {
        //turn character to movement direction and make it move according to camera dir
        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.y) * Mathf.Rad2Deg + cam.eulerAngles.y;
        angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);

        moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        moveDir.Normalize();
        //moveDir = Vector2.ClampMagnitude(inputDirection, 1f);
        if (!moving) moveDir = Vector3.zero;

        desiredVelocity = new Vector3(moveDir.x, 0, moveDir.z) * maxSpeed;
    }

    void ApplyMovement()
    {
        UpdateState();

        AdjustVelocity();

        if (desiredJump)
        {
            desiredJump = false;
            Jump();
        }

        if (moving) body.MoveRotation(Quaternion.Euler(0f, angle, 0f));

        if (isJumping) velocity += jumpDirection * groundJumpPower * Time.deltaTime;
 
        if (!IsGrounded) velocity.y += gravity * Time.deltaTime;
        if (!IsGrounded && velocity.y < maxFallingSpeed) velocity.y = maxFallingSpeed;

        body.velocity = velocity;
    }

    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        float acceleration = IsGrounded ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;

        float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }

    void Jump()
    {
        jumpDirection = Vector3.up;

        if (IsGrounded)
        {
            jumpDirection = contactNormal;
            jumpType = JumpType.GroundJump;
        }
        else if (IsOnSteep)
        {
            jumpDirection = steepNormal;
            jumpPhase = 0;
            jumpType = JumpType.WallJump;
        }
        else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps)
        {
            if (jumpPhase == 0)
            {
                jumpPhase = 1;
            }
            jumpDirection = contactNormal;
            jumpType = JumpType.AirJump;
        }
        else
        {
            return;
        }

        stepsSinceLastJump = 0;
        jumpPhase += 1;

        float jumpSpeed = Mathf.Sqrt(-2f * gravity * fixedJumpHeight);
        jumpDirection = (jumpDirection + Vector3.up).normalized;
        jumpDirection.y = 1;
        velocity.y = 0;

        if (jumpType == JumpType.GroundJump)
        {
            isJumping = true;
            StopCoroutine(nameof(JumpTimer));
            StartCoroutine(nameof(JumpTimer));
            jumpSpeed = Mathf.Sqrt(-2f * gravity * groundJumpMinJumpHeight);
            velocity += jumpDirection * jumpSpeed;
            return;
        }

        velocity += jumpDirection * jumpSpeed;
    }

    IEnumerator JumpTimer()
    {
        yield return new WaitForSeconds(groundJumpTime);
        isJumping = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y >= minGroundDotProduct)
            {
                GroundContactCount += 1;
                contactNormal += normal;
            }
            else if (normal.y > -0.01f)
            {
                steepContactCount += 1;
                steepNormal += normal;
            }
        }
    }

    bool CheckSteepContacts()
    {
        if (steepContactCount > 1)
        {
            steepNormal.Normalize();
            if (steepNormal.y >= minGroundDotProduct)
            {
                GroundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }
        return false;
    }

    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    bool SnapToGround()
    {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
        {
            return false;
        }
        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed)
        {
            return false;
        }
        if (!Physics.Raycast(feetTransform.position, Vector3.down, out RaycastHit hit, probeDistance, probeMask))
        {
            return false;
        }
        if (hit.normal.y < minGroundDotProduct)
        {
            return false;
        }

        GroundContactCount = 1;
        contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        if (dot > 0f)
        {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }
        return true;
    }

    void OnEnable()
    {
        inputs.Gameplay.Enable();
    }

    void OnDisable()
    {
        inputs.Gameplay.Disable();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 rayEnd = new Vector3(feetTransform.position.x, feetTransform.position.y - probeDistance, feetTransform.position.z);
        Gizmos.DrawLine(feetTransform.position, rayEnd);
    }

    private void OnGUI()
    {
        GUILayout.Label($"Ground Contact count {GroundContactCount}");
    }
}
