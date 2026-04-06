using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controller cho Human Player trong Island 2 Gameplay.
/// Di chuyển bằng WASD theo hướng camera, sprint bằng Shift.
/// Sử dụng NavMeshAgent để tôn trọng địa hình và obstacle.
/// Tag phải là "HumanPlayer" để Drone và ExitZone nhận biết đúng.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class HumanPlayerController : MonoBehaviour {
    [Header("Movement Settings")]
    [Tooltip("Tốc độ đi bộ bình thường (m/s)")]
    public float walkSpeed = 5f;

    [Tooltip("Tốc độ chạy nhanh khi giữ Shift (m/s)")]
    public float sprintSpeed = 10f;

    [Tooltip("Tốc độ xoay mặt nhân vật")]
    public float rotationSpeed = 15f;

    [Header("Camera Reference")]
    [Tooltip("Transform của Camera (để tính hướng di chuyển theo camera). Tự tìm Camera.main nếu bỏ trống.")]
    public Transform cameraTransform;

    // ─── Private ─────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private bool _isSprinting;
    private Vector3 _moveDir;

    // ════════════════════════════════════════════════════════════════
    void Awake() {
        _agent = GetComponent<NavMeshAgent>();
        gameObject.tag = "Player"; // Dùng chung tag Player để AI Sensor nhìn thấy
    }

    void Start() {
        // Tự tìm camera nếu chưa gán
        if (cameraTransform == null && Camera.main != null) {
            cameraTransform = Camera.main.transform;
        }

        // Để NavMeshAgent tự xử lý vị trí, tắt update physics tay
        _agent.updatePosition = true;
        _agent.updateRotation = false; // Mình tự xoay để mượt hơn
        _agent.stoppingDistance = 0.1f;
    }

    void Update() {
        // Dừng khi game không còn chơi
        if (IslandGameManager.Instance != null && !IslandGameManager.Instance.IsPlaying) {
            _agent.ResetPath();
            return;
        }

        if (!_agent.isOnNavMesh) return;

        HandleMovement();
        HandleRotation();
    }

    // ════════════════════════════════════════════════════════════════
    #region Movement

    void HandleMovement() {
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S
        _isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) {
            // Không nhấn phím → dừng mượt mà
            _agent.ResetPath();
            _agent.velocity = Vector3.Lerp(_agent.velocity, Vector3.zero, Time.deltaTime * 10f);
            _moveDir = Vector3.zero;
            return;
        }

        // Tính hướng di chuyển dựa theo camera hiện tại
        Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 camRight   = cameraTransform != null ? cameraTransform.right   : Vector3.right;

        // Chiếu xuống mặt phẳng XZ (bỏ trục Y)
        camForward.y = 0f;
        camRight.y   = 0f;
        camForward.Normalize();
        camRight.Normalize();

        _moveDir = (camForward * v + camRight * h).normalized;

        // Tốc độ theo sprint
        float speed = _isSprinting ? sprintSpeed : walkSpeed;
        _agent.speed = speed;

        // Điểm đích xa 4m theo hướng di chuyển → NavMesh pathfind ngắn
        Vector3 destination = transform.position + _moveDir * 4f;
        _agent.SetDestination(destination);
    }

    void HandleRotation() {
        // Xoay nhân vật mượt mà theo hướng đang di chuyển
        if (_moveDir.sqrMagnitude > 0.01f) {
            Quaternion targetRot = Quaternion.LookRotation(_moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    // OnTriggerEnter KHÔNG cần ở đây — Drone xử lý trong AdvancedSeekerDrone.cs
}
