using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI Companion chạy cùng Human Player.
/// Mặc định đi về Exit Zone. Khi Drone đến gần → chạy trốn ngược hướng.
/// Tag: "Player" (Drone vẫn nhận ra và có thể bắt được).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AICompanion : MonoBehaviour {
    // ─── Settings ────────────────────────────────────────────────
    [Header("Navigation")]
    [Tooltip("Transform của Exit Zone — gán trong Inspector")]
    public Transform exitTarget;

    [Tooltip("Bán kính phát hiện Drone (m). Khi Drone vào vùng này → bắt đầu chạy trốn")]
    public float fleeDetectionRadius = 25f;

    [Tooltip("Khoảng cách chạy trốn mỗi lần cập nhật")]
    public float fleeDistance = 12f;

    [Header("Speed")]
    [Tooltip("Tốc độ đi bình thường về Exit (m/s)")]
    public float normalSpeed = 4f;

    [Tooltip("Tốc độ chạy trốn khi Drone gần (m/s)")]
    public float fleeSpeed = 7f;

    [Header("Debug")]
    public bool showDebugGizmos = true;

    // ─── Private ─────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Transform _nearestDrone;
    private float _fleeUpdateTimer;
    private const float FleeUpdateInterval = 0.4f; // Cập nhật hướng chạy 2.5 lần/giây

    enum State { Navigate, Flee }
    private State _state = State.Navigate;

    // ════════════════════════════════════════════════════════════════
    void Awake() {
        _agent = GetComponent<NavMeshAgent>();
        gameObject.tag = "Player"; // Drone nhận diện được!
    }

    void Start() {
        // Tự tìm Exit nếu chưa gán
        if (exitTarget == null) {
            var exit = Object.FindFirstObjectByType<ExitZone>();
            if (exit != null) exitTarget = exit.transform;
        }
    }

    void Update() {
        // Dừng khi game kết thúc
        if (IslandGameManager.Instance != null && !IslandGameManager.Instance.IsPlaying) {
            _agent.ResetPath();
            return;
        }

        if (!_agent.isOnNavMesh) return;

        UpdateDroneDetection();
        UpdateBehavior();
    }

    // ════════════════════════════════════════════════════════════════
    #region State Machine

    void UpdateDroneDetection() {
        // Tìm drone gần nhất (cập nhật liên tục nhưng rẻ vì SearchCache)
        _nearestDrone = FindNearestDroneTransform();

        if (_nearestDrone != null) {
            float dist = Vector3.Distance(transform.position, _nearestDrone.position);
            _state = dist < fleeDetectionRadius ? State.Flee : State.Navigate;
        } else {
            _state = State.Navigate;
        }
    }

    void UpdateBehavior() {
        switch (_state) {
            case State.Navigate:
                DoNavigate();
                break;
            case State.Flee:
                DoFlee();
                break;
        }
    }

    void DoNavigate() {
        _agent.speed = normalSpeed;
        if (exitTarget != null) {
            _agent.SetDestination(exitTarget.position);
        }
    }

    void DoFlee() {
        if (_nearestDrone == null) return;

        _agent.speed = fleeSpeed;

        // Cập nhật đích chạy trốn theo interval (không cần mỗi frame)
        _fleeUpdateTimer -= Time.deltaTime;
        if (_fleeUpdateTimer <= 0f) {
            _fleeUpdateTimer = FleeUpdateInterval;

            Vector3 fleeDir = (transform.position - _nearestDrone.position);
            fleeDir.y = 0f;
            fleeDir.Normalize();

            // Thêm chút lệch ngẫu nhiên để không chạy thẳng quá dễ đoán
            Vector3 randomOffset = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
            fleeDir = (fleeDir + randomOffset).normalized;

            _agent.SetDestination(transform.position + fleeDir * fleeDistance);
        }
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Helpers

    Transform FindNearestDroneTransform() {
        // FindObjectsByType có cache tốt hơn FindObjectsOfType legacy
        var drones = Object.FindObjectsByType<AdvancedSeekerDrone>(FindObjectsSortMode.None);
        Transform nearest = null;
        float nearestSqDist = float.MaxValue;

        foreach (var drone in drones) {
            if (drone == null) continue;
            float sqDist = (transform.position - drone.transform.position).sqrMagnitude;
            if (sqDist < nearestSqDist) {
                nearestSqDist = sqDist;
                nearest = drone.transform;
            }
        }

        return nearest;
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Debug Gizmos

    void OnDrawGizmosSelected() {
        if (!showDebugGizmos) return;

        // Vùng phát hiện Drone
        Gizmos.color = _state == State.Flee
            ? new Color(1f, 0.2f, 0.2f, 0.2f)
            : new Color(0.2f, 1f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, fleeDetectionRadius);

        // Đường đến Exit
        if (exitTarget != null) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, exitTarget.position);
        }
    }
    #endregion
}
