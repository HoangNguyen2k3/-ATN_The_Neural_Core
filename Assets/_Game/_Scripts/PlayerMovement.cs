using UnityEngine;

public class PlayerMovement : MonoBehaviour {
    [Header("Cài đặt chung")]
    public CharacterController controller;
    public Transform cam; // Kéo Main Camera vào đây
    public Animator animator;

    [Header("Thông số di chuyển")]
    public float speed = 6f;
    public float turnSmoothTime = 0.1f;
    float turnSmoothVelocity;

    [Header("Nhảy & Trọng lực")]
    public float gravity = -19.62f; // Tăng trọng lực để rơi thật hơn (-9.81 * 2)
    public float jumpHeight = 1.5f;
    Vector3 velocity;
    bool isGrounded;

    [Header("Check mặt đất")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    void Update() {
        // 1. KIỂM TRA CHẠM ĐẤT
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0) {
            velocity.y = -2f; // Giữ nhân vật bám đất
        }

        // Cập nhật Animator: Trạng thái chạm đất
        if (animator != null) animator.SetBool("IsGrounded", isGrounded);

        // 2. LẤY INPUT
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // 3. DI CHUYỂN & XOAY THEO CAMERA
        if (direction.magnitude >= 0.1f) {
            // Góc xoay của nhân vật phụ thuộc vào hướng Camera
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

        // 4. NHẢY
        if (Input.GetButtonDown("Jump") && isGrounded) {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
        }

        // 5. TRỌNG LỰC
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}