using UnityEngine;

/// <summary>
/// Third Person Camera Controller
/// - Giữ CHUỘT PHẢI để xoay camera
/// - Tự động tránh bức tường (Wall Collision)
/// - Touch drag (Mobile)
/// </summary>
public class CameraDragRotate : MonoBehaviour {
    [Header("=== TARGET ===")]
    [Tooltip("Player Transform - Camera sẽ xoay quanh đối tượng này")]
    public Transform target;

    [Header("=== CAMERA SETTINGS ===")]
    [Tooltip("Khoảng cách lý tưởng từ camera đến player")]
    public float distance = 5f;

    [Tooltip("Khoảng cách tối thiểu camera đến player (tránh xuyên tường)")]
    public float minDistance = 1f;

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
    [Tooltip("Độ mượt của camera")]
    public float smoothTime = 0.08f;

    [Tooltip("Tốc độ thu ngắn khi có tường (nhanh)")]
    public float wallPullSpeed = 15f;

    [Tooltip("Tốc độ kéo dài khi không có tường (chậm)")]
    public float wallRecoverSpeed = 3f;

    [Header("=== WALL COLLISION ===")]
    [Tooltip("Layer mà camera sẽ tránh (thường là Default + Walls)")]
    public LayerMask wallMask;

    [Tooltip("Bán kính sphere cast để phát hiện tường (nhỏ hơn = chính xác hơn)")]
    public float wallCheckRadius = 0.2f;

    [Header("=== INPUT ===")]
    [Tooltip("Bật/tắt đảo ngược trục Y")]
    public bool invertY = false;

    // Private variables
    private float _currentX = 0f;
    private float _currentY = 20f;
    private float _currentDistance;
    private Vector3 _currentVelocity;
    private Vector2 _lastTouchPos;
    private bool _isDragging = false;

    void Start() {
        _currentDistance = distance;
        if (target != null) {
            Vector3 angles = transform.eulerAngles;
            _currentX = angles.y;
            _currentY = angles.x;
        }

        // Khóa chuột vào giữa màn hình và ẩn chuột đi
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate() {
        if (target == null) return;
        HandleInput();
        UpdateCameraPosition();
    }

    void HandleInput() {
        float inputX = 0f;
        float inputY = 0f;

        // ===== TOUCH INPUT (Mobile) — 1 ngón tay vuốt =====
        if (Input.touchCount == 1) {
            Touch touch = Input.GetTouch(0);
            switch (touch.phase) {
                case TouchPhase.Began:
                    _lastTouchPos = touch.position;
                    _isDragging = true;
                    break;
                case TouchPhase.Moved:
                    if (_isDragging) {
                        Vector2 delta = touch.position - _lastTouchPos;
                        inputX = delta.x / Screen.width * 180f;
                        inputY = delta.y / Screen.height * 90f;
                        _lastTouchPos = touch.position;
                    }
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    _isDragging = false;
                    break;
            }
        }
        // ===== MOUSE INPUT (PC) — Di chuyển chuột tự do (Free-look) =====
        else {
            // Không cần giữ chuột phải nữa, di chuyển chuột là xoay
            inputX = Input.GetAxis("Mouse X") * 5f;
            inputY = Input.GetAxis("Mouse Y") * 3f;
        }

        // Áp dụng input
        _currentX += inputX * rotationSpeedX * Time.deltaTime;
        float yDirection = invertY ? 1f : -1f;
        _currentY += inputY * rotationSpeedY * Time.deltaTime * yDirection;
        _currentY = Mathf.Clamp(_currentY, minVerticalAngle, maxVerticalAngle);
    }

    void UpdateCameraPosition() {
        Quaternion rotation = Quaternion.Euler(_currentY, _currentX, 0);

        // Gốc nhìn là vai/đầu của nhân vật
        Vector3 pivotPos = target.position + Vector3.up * height;

        // Hướng ra phía sau lưng nhân vật
        Vector3 desiredDirection = rotation * Vector3.back;
        float desiredDistance = distance;

        // ── Wall Collision: SphereCast từ pivot ra phía sau ──
        RaycastHit wallHit;
        if (Physics.SphereCast(pivotPos, wallCheckRadius, desiredDirection, out wallHit, desiredDistance, wallMask)) {
            // Có tường! Kéo camera vào gần hơn (trừ đi bán kính sphere)
            desiredDistance = Mathf.Max(wallHit.distance - wallCheckRadius, minDistance);
        }

        // Smooth khoảng cách: thu nhanh khi có tường, kéo ra chậm khi hết tường
        float distSpeed = (_currentDistance > desiredDistance) ? wallPullSpeed : wallRecoverSpeed;
        _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, Time.deltaTime * distSpeed);

        // Tính vị trí cuối
        Vector3 targetPosition = pivotPos + desiredDirection * _currentDistance;

        // Smooth di chuyển
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, smoothTime);

        // Camera nhìn vào pivot
        transform.LookAt(pivotPos);
    }

    public void ResetCamera() {
        _currentX = 0f;
        _currentY = 20f;
        _currentDistance = distance;
    }

    public void SetRotation(float x, float y) {
        _currentX = x;
        _currentY = Mathf.Clamp(y, minVerticalAngle, maxVerticalAngle);
    }

    void OnGUI() {
        // Chỉ vẽ tâm ngắm nếu đang khóa chuột (đang chơi)
        if (Cursor.lockState == CursorLockMode.Locked) {
            float size = 10f;
            float thickness = 2f;
            float center_x = Screen.width / 2f;
            float center_y = Screen.height / 2f;

            // Đổi màu GUI
            GUI.color = new Color(1f, 1f, 1f, 0.8f);

            // Vẽ thanh ngang
            GUI.DrawTexture(new Rect(center_x - size, center_y - thickness / 2, size * 2, thickness), Texture2D.whiteTexture);
            // Vẽ thanh dọc
            GUI.DrawTexture(new Rect(center_x - thickness / 2, center_y - size, thickness, size * 2), Texture2D.whiteTexture);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {
        if (target == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(target.position + Vector3.up * height, 0.3f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position + Vector3.up * height);
    }
#endif
}