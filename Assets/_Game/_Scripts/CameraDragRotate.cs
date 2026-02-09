using UnityEngine;

/// <summary>
/// Third Person Camera Controller - Xoay camera quanh player giống Unity Starter Assets
/// Script này điều khiển một "camera pivot" xoay quanh player
/// Cinemachine sẽ follow pivot này thay vì player trực tiếp
/// </summary>
public class CameraDragRotate : MonoBehaviour {
    [Header("=== TARGET ===")]
    [Tooltip("Player Transform - Camera sẽ xoay quanh đối tượng này")]
    public Transform target;

    [Header("=== CAMERA SETTINGS ===")]
    [Tooltip("Khoảng cách từ camera đến player")]
    public float distance = 5f;

    [Tooltip("Độ cao camera so với player")]
    public float height = 2f;

    [Header("=== ROTATION SETTINGS ===")]
    [Tooltip("Độ nhạy xoay ngang")]
    public float rotationSpeedX = 120f;

    [Tooltip("Độ nhạy xoay dọc")]
    public float rotationSpeedY = 80f;

    [Tooltip("Góc nhìn xuống tối thiểu")]
    public float minVerticalAngle = -20f;

    [Tooltip("Góc nhìn lên tối đa")]
    public float maxVerticalAngle = 60f;

    [Header("=== SMOOTHING ===")]
    [Tooltip("Độ mượt của camera (cao = mượt hơn)")]
    public float smoothTime = 0.1f;

    [Header("=== INPUT ===")]
    [Tooltip("Bật/tắt đảo ngược trục Y")]
    public bool invertY = false;

    // Private variables
    private float currentX = 0f;
    private float currentY = 20f;
    private Vector3 currentVelocity;
    private Vector2 lastTouchPosition;
    private bool isDragging = false;

    void Start() {
        // Khởi tạo góc ban đầu dựa trên vị trí hiện tại của camera
        if (target != null) {
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;
            currentY = angles.x;
        }
    }

    void LateUpdate() {
        if (target == null) return;

        HandleInput();
        UpdateCameraPosition();
    }

    void HandleInput() {
        float inputX = 0f;
        float inputY = 0f;

        // ===== TOUCH INPUT (Mobile) =====
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase) {
                case TouchPhase.Began:
                    lastTouchPosition = touch.position;
                    isDragging = true;
                    break;

                case TouchPhase.Moved:
                    if (isDragging) {
                        Vector2 delta = touch.position - lastTouchPosition;
                        // Chia cho screen width để normalize
                        inputX = delta.x / Screen.width * 180f;
                        inputY = delta.y / Screen.height * 90f;
                        lastTouchPosition = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isDragging = false;
                    break;
            }
        }
        // ===== MOUSE INPUT (PC) =====
        else if (Input.GetMouseButton(0)) {
            inputX = Input.GetAxis("Mouse X") * 5f;
            inputY = Input.GetAxis("Mouse Y") * 3f;
        }

        // Áp dụng input vào góc xoay
        currentX += inputX * rotationSpeedX * Time.deltaTime;
        
        float yDirection = invertY ? 1f : -1f;
        currentY += inputY * rotationSpeedY * Time.deltaTime * yDirection;
        
        // Giới hạn góc dọc
        currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);
    }

    void UpdateCameraPosition() {
        // Tính toán vị trí camera dựa trên góc xoay
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        
        // Vị trí offset từ target
        Vector3 offset = rotation * new Vector3(0, 0, -distance);
        offset.y += height;
        
        // Vị trí mục tiêu của camera
        Vector3 targetPosition = target.position + offset;
        
        // Smooth movement
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
        
        // Camera luôn nhìn vào target
        Vector3 lookAtPoint = target.position + Vector3.up * (height * 0.5f);
        transform.LookAt(lookAtPoint);
    }

    // Gọi method này nếu muốn reset camera về vị trí mặc định
    public void ResetCamera() {
        currentX = 0f;
        currentY = 20f;
    }

    // Cho phép script khác set góc camera
    public void SetRotation(float x, float y) {
        currentX = x;
        currentY = Mathf.Clamp(y, minVerticalAngle, maxVerticalAngle);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {
        if (target == null) return;
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(target.position, 0.5f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position + Vector3.up * height * 0.5f);
    }
#endif
}