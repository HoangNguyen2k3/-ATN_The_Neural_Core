using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// ML-Agent Boss cho Đảo 3.
/// Dùng PPO + Dynamic Player Profiling (1 model duy nhất biết vạn chiêu).
/// 
/// Observations (15 giá trị):
///   1-3:  Vận tốc cục bộ Boss
///   4-6:  Hướng tới Player (local, normalized)
///   7:    Khoảng cách tới Player (normalized)
///   8:    HP Boss (normalized)
///   9:    HP Player (normalized)
///   10:   Phase Boss (0.0 / 0.5 / 1.0)
///   11:   Cooldown trung bình (0-1)
///   12:   Facing dot (-1 → 1)
///   13:   aggressionScore  ← TỪ PLAYER ANALYZER
///   14:   agilityScore     ← TỪ PLAYER ANALYZER
///   15:   preferredRange   ← TỪ PLAYER ANALYZER
/// 
/// Actions:
///   Continuous (2): Di chuyển tiến/lùi + Xoay trái/phải
///   Discrete (1 branch, 6 options): Chọn kỹ năng
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AdaptiveBossAgent : Agent {
    // ─── Cài đặt di chuyển ──────────────────────────────────────
    [Header("🚀 Di chuyển")]
    public float moveSpeed = 8f;
    public float turnSpeed = 200f;

    // ─── Tham chiếu ─────────────────────────────────────────────
    [Header("🔗 Tham chiếu")]
    public BossSkillExecutor skillExecutor;
    public BossHealthSystem myHealth;           // HP của Boss
    public BossHealthSystem playerHealth;       // HP của Player
    public PlayerCombatAnalyzer playerAnalyzer; // Bộ đo thói quen Player
    public Transform playerTransform;           // Transform của Player
    public BossArenaManager arenaManager;
    public Animator animator;

    [Header("⚙️ Cấu hình Training")]
    [Tooltip("Bán kính đấu trường để chuẩn hóa khoảng cách")]
    public float arenaRadius = 20f;

    // ─── Private ────────────────────────────────────────────────
    private Rigidbody rb;
    private float currentMoveInput;
    private float currentTurnInput;

    // ════════════════════════════════════════════════════════════════
    #region ML-Agents Lifecycle

    public override void Initialize() {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ
                       | RigidbodyConstraints.FreezePositionY;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;

        if (animator != null) {
            animator.applyRootMotion = false;
        }

        // Auto-find references locally inside the same Arena
        if (arenaManager == null) arenaManager = transform.parent.GetComponentInChildren<BossArenaManager>();
        if (playerAnalyzer == null) playerAnalyzer = transform.parent.GetComponentInChildren<PlayerCombatAnalyzer>();
        if (skillExecutor == null) skillExecutor = GetComponent<BossSkillExecutor>();
        if (myHealth == null) myHealth = GetComponent<BossHealthSystem>();
    }

    public override void OnEpisodeBegin() {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentMoveInput = 0f;
        currentTurnInput = 0f;

        // Reset HP + Cooldowns
        myHealth?.ResetHP();
        skillExecutor?.ResetAllCooldowns();

        // Reset Arena (nếu đang Training)
        arenaManager?.ResetArena();
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        // Test thủ công bằng bàn phím
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxis("Vertical");
        continuous[1] = Input.GetAxis("Horizontal");

        var discrete = actionsOut.DiscreteActions;
        discrete[0] = 0; // Mặc định: Idle

        if (Input.GetKey(KeyCode.Alpha1)) discrete[0] = 1; // Laser
        if (Input.GetKey(KeyCode.Alpha2)) discrete[0] = 2; // Dash
        if (Input.GetKey(KeyCode.Alpha3)) discrete[0] = 3; // AoE
        if (Input.GetKey(KeyCode.Alpha4)) discrete[0] = 4; // Shield
        if (Input.GetKey(KeyCode.Alpha5)) discrete[0] = 5; // Melee
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Observations (15 giá trị)

    public override void CollectObservations(VectorSensor sensor) {
        // ═══ 1. Vận tốc cục bộ Boss (3) ═══
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));

        // ═══ 2-7. Thông tin về Player ═══
        Vector3 toPlayer = Vector3.zero;
        float normalizedDist = 0f;
        float facingDot = 0f;

        if (playerTransform != null) {
            toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;

            float dist = toPlayer.magnitude;
            normalizedDist = Mathf.Clamp01(dist / Mathf.Max(1f, arenaRadius));

            if (dist > 0.001f) {
                facingDot = Vector3.Dot(transform.forward, toPlayer.normalized);
            }
        }

        Vector3 localDir = transform.InverseTransformDirection(toPlayer);
        if (localDir.sqrMagnitude > 0.001f) localDir.Normalize();

        sensor.AddObservation(localDir);            // 3 giá trị (4-6)
        sensor.AddObservation(normalizedDist);      // 1 giá trị (7)

        // ═══ 8-9. HP ═══
        float bossHPRatio = myHealth != null ? myHealth.HPRatio : 1f;
        float playerHPRatio = playerHealth != null ? playerHealth.HPRatio : 1f;
        sensor.AddObservation(bossHPRatio);         // 1 giá trị (8)
        sensor.AddObservation(playerHPRatio);       // 1 giá trị (9)

        // ═══ 10. Phase Boss ═══
        float phaseNorm = 0f;
        if (myHealth != null) {
            phaseNorm = (myHealth.CurrentPhase - 1) / 2f; // Phase 1=0.0, 2=0.5, 3=1.0
        }
        sensor.AddObservation(phaseNorm);           // 1 giá trị (10)

        // ═══ 11. Cooldown trung bình ═══
        float cooldownRatio = skillExecutor != null ? skillExecutor.AverageCooldownRatio : 0f;
        sensor.AddObservation(cooldownRatio);       // 1 giá trị (11)

        // ═══ 12. Facing Dot ═══
        sensor.AddObservation(facingDot);           // 1 giá trị (12)

        // ═══ 13-15. 🔑 PLAYER COMBAT PROFILE (Điểm thần kỳ!) ═══
        float aggression = playerAnalyzer != null ? playerAnalyzer.aggressionScore : 0.5f;
        float agility = playerAnalyzer != null ? playerAnalyzer.agilityScore : 0.5f;
        float range = playerAnalyzer != null ? playerAnalyzer.preferredRange : 0.5f;

        sensor.AddObservation(aggression);          // 1 giá trị (13)
        sensor.AddObservation(agility);             // 1 giá trị (14)
        sensor.AddObservation(range);               // 1 giá trị (15)
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Actions

    public override void OnActionReceived(ActionBuffers actions) {
        if (rb == null) return;

        // Đóng băng khi không đang Fighting
        if (arenaManager != null && !arenaManager.IsFighting) {
            currentMoveInput = 0f;
            currentTurnInput = 0f;
            return;
        }

        // ═══ CONTINUOUS: Di chuyển + Xoay ═══
        currentMoveInput = actions.ContinuousActions[0];
        currentTurnInput = actions.ContinuousActions[1];

        // ═══ DISCRETE: Chọn Skill ═══
        int skillChoice = actions.DiscreteActions[0];
        if (skillExecutor != null && skillChoice > 0) {
            bool executed = skillExecutor.ExecuteSkill(skillChoice);

            if (executed) {
                // Thưởng nếu skill dính, phạt nếu miss
                if (skillExecutor.LastSkillHit) {
                    AddReward(0.5f);
                } else {
                    AddReward(-0.1f);
                }
            }
        }

        // ═══ Time Penalty ═══
        if (MaxStep > 0) {
            AddReward(-1f / MaxStep);
        }
    }

    void FixedUpdate() {
        if (rb == null) return;

        // Đóng băng
        if (arenaManager != null && !arenaManager.IsFighting) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        // ═══ XOAY ═══
        transform.Rotate(Vector3.up, currentTurnInput * turnSpeed * Time.fixedDeltaTime);

        // ═══ DI CHUYỂN (AddForce, giống Drone Đảo 2) ═══
        Vector3 targetVelocity = transform.forward * currentMoveInput * moveSpeed;
        Vector3 currentFlatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 velocityChange = targetVelocity - currentFlatVel;
        rb.AddForce(velocityChange * 10f, ForceMode.Acceleration);

        // ═══ ANIMATOR ═══
        if (animator != null) {
            animator.SetFloat("Speed", currentFlatVel.magnitude);
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Collision

    void OnCollisionStay(Collision collision) {
        if (collision.gameObject.CompareTag("Wall")) {
            AddReward(-0.01f);
        }
    }

    #endregion
}
