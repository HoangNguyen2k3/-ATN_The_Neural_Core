using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// AI Companion chạy cùng Human Player.
/// Có 2 chế độ: Đi dạo lung tung (isPatrol = true) hoặc đi thẳng về Exit (isPatrol = false).
/// Khi Drone đến gần → luôn ưu tiên chạy trốn ngược hướng.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AICompanion : MonoBehaviour {
    // ─── Settings ────────────────────────────────────────────────
    [Header("Behavior Mode")]
    [Tooltip("Bật True để AI đi dạo ngẫu nhiên (Rất tốt khi Train AI). Bật False để AI đi tìm lối thoát.")]
    public bool isPatrol = false;

    [Header("Navigation - Exit Mode")]
    [Tooltip("Transform của Exit Zone — gán trong Inspector")]
    public Transform exitTarget;

    [Header("Navigation - Patrol Mode")]
    [Tooltip("Bán kính đi dạo ngẫu nhiên quanh vị trí hiện tại")]
    public float patrolRadius = 15f;
    [Tooltip("Thời gian đứng chờ trước khi đi đến điểm mới")]
    public float patrolWaitTime = 2f;

    [Header("Fleeing (Chạy trốn)")]
    [Tooltip("Bán kính phát hiện Drone (m). Khi Drone vào vùng này → bắt đầu chạy trốn")]
    public float fleeDetectionRadius = 25f;
    [Tooltip("Khoảng cách chạy trốn mỗi lần cập nhật")]
    public float fleeDistance = 12f;

    [Header("Speed")]
    public float normalSpeed = 4f;
    public float fleeSpeed = 7f;

    [Header("Animation & Debug")]
    public Animator animator;
    public bool showDebugGizmos = true;

    // ─── Private ─────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Transform _nearestDrone;
    private float _fleeUpdateTimer;
    private float _patrolTimer;
    private const float FleeUpdateInterval = 0.4f;

    enum State { Navigate, Patrol, Flee }
    private State _state = State.Navigate;

    // ════════════════════════════════════════════════════════════════
    void Awake() {
        _agent = GetComponent<NavMeshAgent>();
        gameObject.tag = "Player"; // Drone nhận diện được
    }

    void Start() {
        if (exitTarget == null) {
            var exit = Object.FindFirstObjectByType<ExitZone>();
            if (exit != null) exitTarget = exit.transform;
        }
        _patrolTimer = patrolWaitTime; // Khởi tạo timer đi dạo
    }

    void Update() {
        if (IslandGameManager.Instance != null && !IslandGameManager.Instance.IsPlaying) {
            _agent.ResetPath();
            UpdateAnimation(0f);
            return;
        }

        if (!_agent.isOnNavMesh) return;

        UpdateDroneDetection();
        UpdateBehavior();
        UpdateAnimation(_agent.velocity.magnitude);
    }

    void UpdateAnimation(float speed) {
        if (animator != null) {
            animator.SetFloat("Speed", speed);
        }
    }

    // ════════════════════════════════════════════════════════════════
    #region State Machine

    void UpdateDroneDetection() {
        _nearestDrone = FindNearestDroneTransform();

        // Ưu tiên 1: Chạy trốn nếu Drone ở gần
        if (_nearestDrone != null) {
            float dist = Vector3.Distance(transform.position, _nearestDrone.position);
            if (dist < fleeDetectionRadius) {
                _state = State.Flee;
                return;
            }
        }

        // Ưu tiên 2: Làm theo Mode đã chọn
        _state = isPatrol ? State.Patrol : State.Navigate;
    }

    void UpdateBehavior() {
        switch (_state) {
            case State.Navigate:
                DoNavigate();
                break;
            case State.Patrol:
                DoPatrol();
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

    void DoPatrol() {
        _agent.speed = normalSpeed;

        // Nếu đã đến đích (hoặc gần đến) -> bắt đầu đếm ngược thời gian chờ
        if (!_agent.hasPath || _agent.remainingDistance < 0.5f) {
            _patrolTimer -= Time.deltaTime;

            // Hết thời gian chờ -> Tìm điểm mới ngẫu nhiên trên NavMesh
            if (_patrolTimer <= 0f) {
                Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
                randomDir += transform.position;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDir, out hit, patrolRadius, NavMesh.AllAreas)) {
                    _agent.SetDestination(hit.position);
                }

                _patrolTimer = patrolWaitTime; // Reset timer
            }
        }
    }

    void DoFlee() {
        if (_nearestDrone == null) return;

        _agent.speed = fleeSpeed;
        _fleeUpdateTimer -= Time.deltaTime;

        if (_fleeUpdateTimer <= 0f) {
            _fleeUpdateTimer = FleeUpdateInterval;

            Vector3 fleeDir = (transform.position - _nearestDrone.position);
            fleeDir.y = 0f;
            fleeDir.Normalize();

            Vector3 randomOffset = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
            fleeDir = (fleeDir + randomOffset).normalized;

            _agent.SetDestination(transform.position + fleeDir * fleeDistance);
        }
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Helpers & Gizmos

    Transform FindNearestDroneTransform() {
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

    void OnDrawGizmosSelected() {
        if (!showDebugGizmos) return;

        Gizmos.color = _state == State.Flee
            ? new Color(1f, 0.2f, 0.2f, 0.2f)
            : new Color(0.2f, 1f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, fleeDetectionRadius);

        if (_state == State.Navigate && exitTarget != null) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, exitTarget.position);
        }
    }
    #endregion
}