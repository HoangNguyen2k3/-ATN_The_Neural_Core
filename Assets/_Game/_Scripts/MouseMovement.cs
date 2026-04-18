using UnityEngine;

public class MouseMovement : MonoBehaviour {

    [Header("Mục tiêu")]
    public Transform target; // Kéo Player vào đây
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0); // Bù trừ chiều cao (ngang vai/đầu nhân vật)

    [Header("Thiết lập Camera")]
    public float distance = 4f; // Khoảng cách từ cam đến player
    public float mouseSensitivity = 2f;

    [Header("Giới hạn góc nhìn (Chống chìm đất)")]
    public float minYAngle = -15f; // Không cho phép nhìn từ dưới gầm lên (-15 độ)
    public float maxYAngle = 70f;  // Giới hạn nhìn từ trên chóp xuống

    [Header("Va chạm Camera")]
    public LayerMask collisionMask; // Chọn layer Ground/Wall để cam không xuyên qua

    private float currentX = 0f;
    private float currentY = 0f;

    void Start() {
        /*        // Ẩn con trỏ chuột
                Cursor.lockState = CursorLockMode.Locked;*/
    }

    void LateUpdate() { // Dùng LateUpdate cho Camera để tránh bị giật lag (jitter)
        if (target == null) return;

        // Nhận input vuốt/chuột
        currentX += Input.GetAxis("Mouse X") * mouseSensitivity;
        currentY -= Input.GetAxis("Mouse Y") * mouseSensitivity;

        // KHÓA GÓC TRỤC Y: Giải quyết triệt để việc cam chìm xuống đất
        currentY = Mathf.Clamp(currentY, minYAngle, maxYAngle);

        // Tính toán vị trí xoay
        Vector3 direction = new Vector3(0, 0, -distance);
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 expectedPosition = target.position + targetOffset + rotation * direction;

        // RAYCAST: Tránh xuyên tường/đất
        RaycastHit hit;
        if (Physics.Linecast(target.position + targetOffset, expectedPosition, out hit, collisionMask)) {
            // Nếu đụng đất/tường, dời camera tới điểm va chạm (cộng thêm tí offset để không sát rạt)
            transform.position = hit.point + hit.normal * 0.15f;
        }
        else {
            // Không vướng gì thì ở vị trí bình thường
            transform.position = expectedPosition;
        }

        // Luôn nhìn về phía nhân vật
        transform.LookAt(target.position + targetOffset);
    }
}