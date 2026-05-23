using UnityEngine;

/// <summary>
/// Controller cho Human Player trong Island 2 Gameplay.
/// Điều khiển bằng CharacterController, xoay theo Camera và có Animator.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class HumanPlayerController : MonoBehaviour {
    [Header("Cài đặt chung")]
    public CharacterController controller;
    public Transform cam; // Kéo Main Camera vào đây
    public Animator animator;

    [Header("Thông số di chuyển")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 10f; // Chạy nhanh khi giữ Shift
    public float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity;

    [Header("Nhảy & Trọng lực")]
    public float gravity = -19.62f;
    public float jumpHeight = 1.5f;
    private Vector3 velocity;
    private bool isGrounded;

    [Header("Check mặt đất")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    void Awake() {
        if (controller == null) controller = GetComponent<CharacterController>();
        gameObject.tag = "Player"; // Dùng chung tag Player để AI Sensor nhìn thấy
    }

    void Start() {
        if (cam == null && Camera.main != null) {
            cam = Camera.main.transform;
        }
    }

    void Update() {
        // Dừng điều khiển khi game kết thúc
        if (IslandGameManager.Instance != null && !IslandGameManager.Instance.IsPlaying) {
            if (animator != null) {
                animator.SetBool("IsMoving", false);
            }
            return;
        }

        HandleMovementAndAnimation();
    }

    void HandleMovementAndAnimation() {
        // 1. KIỂM TRA CHẠM ĐẤT
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0) {
            velocity.y = -2f; // Giữ nhân vật bám đất
        }

        if (animator != null) animator.SetBool("IsGrounded", isGrounded);

        // 2. LẤY INPUT (mobile joystick hoặc keyboard fallback)
        Vector2 moveInput = MobileInputBridge.HasMoveInput
            ? MobileInputBridge.MoveInput
            : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        bool isSprinting = MobileInputBridge.SprintHeld
            || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // 3. DI CHUYỂN & XOAY THEO CAMERA
        if (direction.magnitude >= 0.1f) {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);

            if (animator != null) animator.SetBool("IsMoving", true);
        }
        else {
            if (animator != null) animator.SetBool("IsMoving", false);
        }

        // 4. NHẢY
        if ((MobileInputBridge.JumpDown || Input.GetButtonDown("Jump")) && isGrounded) {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
        }

        // 5. TRỌNG LỰC
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}