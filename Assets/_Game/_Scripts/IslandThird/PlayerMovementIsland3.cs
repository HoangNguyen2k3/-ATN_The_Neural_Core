using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementIsland3 : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseSpeed = 6f;
    public float turnSmoothTime = 0.1f;
    public float gravity = -9.81f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    [Header("References")]
    public Transform cam;
    public Animator animator;
    public PlayerCombatController combatController;

    private CharacterController _controller;
    private float _turnSmoothVelocity;
    private Vector3 _velocity;
    private bool _isGrounded;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        
        if (combatController == null)
            combatController = GetComponent<PlayerCombatController>();

        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;
    }

    void Update()
    {
        // 1. Kiểm tra mặt đất & áp dụng trọng lực
        CheckGround();

        // 2. Nếu đang Dodge, quyền di chuyển thuộc về PlayerCombatController, ngưng nhận input
        if (combatController != null && combatController.IsDodging)
        {
            ApplyGravity();
            return;
        }

        // 3. Nhận Input WASD
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // 4. Xử lý di chuyển & xoay theo Camera
        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            if (cam != null) targetAngle += cam.eulerAngles.y;

            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // Nếu đang đỡ đòn (Block), tốc độ sẽ bị giảm (SpeedMultiplier < 1)
            float currentSpeed = baseSpeed;
            if (combatController != null)
            {
                currentSpeed *= combatController.SpeedMultiplier;
            }

            _controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);

            if (animator != null) 
            {
                animator.SetFloat("Speed", currentSpeed);
                animator.SetFloat("MotionSpeed", 1f);
            }
        }
        else
        {
            if (animator != null) 
            {
                animator.SetFloat("Speed", 0f);
                animator.SetFloat("MotionSpeed", 1f);
            }
        }

        // 5. Cập nhật vị trí theo trọng lực
        ApplyGravity();
    }

    private void CheckGround()
    {
        if (groundCheck != null)
        {
            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        }
        else
        {
            // Fallback nếu chưa kéo groundCheck vào inspector
            _isGrounded = _controller.isGrounded;
        }

        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        if (animator != null) animator.SetBool("Grounded", _isGrounded);
    }

    private void ApplyGravity()
    {
        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}
