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

        // Cập nhật Animator: Báo trạng thái chạm đất
        animator.SetBool("IsGrounded", isGrounded);

        // 2. NHẬN INPUT TỪ BÀN PHÍM (PC)
        float horizontal = Input.GetAxisRaw("Horizontal"); // Phím A/D hoặc Mũi tên trái/phải
        float vertical = Input.GetAxisRaw("Vertical");     // Phím W/S hoặc Mũi tên lên/xuống
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // 3. XỬ LÝ DI CHUYỂN & XOAY
        if (direction.magnitude >= 0.1f) {
            // Tính góc xoay theo hướng Camera đang nhìn
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;

            // Làm mượt góc xoay (để nhân vật không quay ngoắt 180 độ)
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // Tính hướng di chuyển thực sự sau khi đã xoay
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * speed * Time.deltaTime);

            // --> ANIMATOR: Báo là ĐANG CHẠY (Dùng Bool IsMoving)
            animator.SetBool("IsMoving", true);
        }
        else {
            // --> ANIMATOR: Báo là ĐANG ĐỨNG (Dùng Bool IsMoving)
            animator.SetBool("IsMoving", false);
        }

        // 4. XỬ LÝ NHẢY (Phím Space)
        if (Input.GetButtonDown("Jump") && isGrounded) {
            // Công thức vật lý: Vận tốc = Căn bậc 2 của (Độ cao * -2 * Trọng lực)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // Kích hoạt Trigger Nhảy
            animator.SetTrigger("Jump");
        }

        // 5. ÁP DỤNG TRỌNG LỰC
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}