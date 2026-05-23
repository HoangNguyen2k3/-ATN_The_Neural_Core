using UnityEngine;

public class ThirdPersonController : MonoBehaviour {
    [Header("Cài đặt chung")]
    public CharacterController controller;
    public Transform cam; // Kéo Main Camera vào đây
    public Animator animator;

    [Header("Thông số di chuyển")]
    public float speed = 6f;
    public float turnSmoothTime = 0.1f;
    float turnSmoothVelocity;

    [Header("Nhảy & Trọng lực")]
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f; // Độ cao nhảy (mét)
    Vector3 velocity;
    bool isGrounded;

    [Header("Check mặt đất")]
    public Transform groundCheck; // Object rỗng đặt dưới chân
    public float groundDistance = 0.4f; // Bán kính cầu kiểm tra
    public LayerMask groundMask; // Layer của đất/sàn

    void Update() {
        // 1. KIỂM TRA CHẠM ĐẤT
        // Tạo một quả cầu nhỏ dưới chân để xem có chạm vào Layer "Ground" không
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0) {
            velocity.y = -2f; // Giữ nhân vật dính xuống đất khi đi dốc
        }

        if (animator != null) animator.SetBool("IsGrounded", isGrounded);

        // 2. NHẬN INPUT (mobile joystick hoặc keyboard fallback)
        Vector2 moveInput = MobileInputBridge.HasMoveInput
            ? MobileInputBridge.MoveInput
            : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        // 3. XỬ LÝ DI CHUYỂN & XOAY
        if (direction.magnitude >= 0.1f) {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * speed * Time.deltaTime);
            if (animator != null) animator.SetBool("IsMoving", true);
        }
        else {
            if (animator != null) animator.SetBool("IsMoving", false);
        }

        // 4. XỬ LÝ NHẢY
        if ((MobileInputBridge.JumpDown || Input.GetButtonDown("Jump")) && isGrounded) {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
        }

        // 5. ÁP DỤNG TRỌNG LỰC
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}