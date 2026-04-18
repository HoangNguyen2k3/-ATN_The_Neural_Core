using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AdvancedSeekerDrone : Agent {
    [Header("Cài đặt Di chuyển")]
    [Tooltip("Tốc độ di chuyển tối đa (m/s)")]
    public float moveSpeed = 15f;
    [Tooltip("Tốc độ xoay (độ/giây)")]
    public float turnSpeed = 250f;
    public bool allowBackward = false;

    [Header("Tham chiếu")]
    public MapManager mapManager;
    public Animator animator;
    public GameObject coneObject;

    private Rigidbody rb;
    private float lastWallHitTime = 0f;
    private float spawnY; // Khóa cứng độ cao bay

    // Lưu lệnh từ não AI (OnActionReceived) để FixedUpdate dùng liên tục
    private float currentMoveInput = 0f;
    private float currentTurnInput = 0f;

    // ════════════════════════════════════════════════════════════════
    #region ML-Agents Lifecycle

    public override void Initialize() {
        rb = GetComponent<Rigidbody>();

        if (mapManager == null) {
            mapManager = FindFirstObjectByType<MapManager>();
        }

        MaxStep = 0;

        // === CẤU HÌNH RIGIDBODY CHUẨN CHO DRONE BAY ===
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // Khóa xoay X/Z để không bị lật, KHÔNG khóa Y position trong constraints
        // vì ta sẽ khóa Y thủ công trong FixedUpdate (chính xác hơn)
        rb.constraints = RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;
        // Giảm Drag để AddForce không bị triệt tiêu
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;

        // === TẮT ROOT MOTION — tránh Animator giật Rigidbody ===
        if (animator != null) {
            animator.applyRootMotion = false;
        }

        // Ghi nhớ độ cao spawn ban đầu
        spawnY = transform.position.y;
    }

    public override void OnEpisodeBegin() {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentMoveInput = 0f;
        currentTurnInput = 0f;

        // Khi đang trong Intro cutscene: KHÔNG reset vị trí drone
        // (để drone giữ nguyên vị trí trong scene mà đạo diễn đã xếp)
        if (IslandGameManager.Instance != null && !IslandGameManager.Instance.IsPlaying) {
            spawnY = transform.position.y;
            return;
        }

        if (mapManager != null && !mapManager.isResetting) {
            mapManager.ResetSingleDronePosition(this);
        }

        // Cập nhật lại độ cao khóa sau khi spawn
        spawnY = transform.position.y;
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Vertical");
        continuousActionsOut[1] = Input.GetAxis("Horizontal");
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Observations (18 giá trị)

    public override void CollectObservations(VectorSensor sensor) {
        if (mapManager == null) return;

        // 1. Vận tốc cục bộ (3 giá trị)
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));

        // 2. Heatmap lưới 3x3 (9 giá trị)
        Vector2Int myCell = mapManager.WorldToGrid(transform.position);
        for (int x = -1; x <= 1; x++) {
            for (int z = -1; z <= 1; z++) {
                float cellLastVisitTime = mapManager.GetHeat(myCell.x + x, myCell.y + z);
                float timeSinceVisit = Time.time - cellLastVisitTime;
                sensor.AddObservation(Mathf.Clamp01(timeSinceVisit / 10f));
            }
        }

        // 3. Quan sát mục tiêu (6 giá trị)
        GameObject targetObj = mapManager.GetNearestActivePlayer(transform.position);

        Vector3 toTargetWorld = Vector3.zero;
        float normalizedDistance = 0f;
        float facingDot = 0f;

        if (targetObj != null) {
            toTargetWorld = targetObj.transform.position - transform.position;
            toTargetWorld.y = 0f;

            float distance = toTargetWorld.magnitude;
            float maxMapDimension = Mathf.Max(mapManager.mapSize.x, mapManager.mapSize.y);
            normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(1f, maxMapDimension));

            if (distance > 0.001f) {
                facingDot = Vector3.Dot(transform.forward, toTargetWorld.normalized);
            }
        }

        Vector3 localTargetDir = transform.InverseTransformDirection(toTargetWorld);
        if (localTargetDir.sqrMagnitude > 0.001f) {
            localTargetDir.Normalize();
        }

        float horizontalSpeedRatio = Mathf.Clamp01(
            new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude / Mathf.Max(0.01f, moveSpeed)
        );

        sensor.AddObservation(localTargetDir);        // 3 giá trị
        sensor.AddObservation(normalizedDistance);     // 1 giá trị
        sensor.AddObservation(facingDot);              // 1 giá trị
        sensor.AddObservation(horizontalSpeedRatio);   // 1 giá trị
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Actions & Physics

    public override void OnActionReceived(ActionBuffers actions) {
        if (mapManager == null || rb == null) return;

        // ═══ ĐÓNG BĂNG KHI ĐANG CUTSCENE INTRO ═══
        if (IslandGameManager.Instance != null && !IslandGameManager.Instance.IsPlaying) {
            currentMoveInput = 0f;
            currentTurnInput = 0f;
            return;
        }

        // Lưu lệnh từ não AI — FixedUpdate sẽ thực thi liên tục
        currentMoveInput = actions.ContinuousActions[0];
        currentTurnInput = actions.ContinuousActions[1];

        if (!allowBackward) {
            currentMoveInput = (currentMoveInput + 1f) * 0.5f; // Map [-1,1] → [0,1]
        }

        // Xử lý phần thưởng (chỉ gọi mỗi Decision, không cần mỗi FixedUpdate)
        mapManager.ProcessHeatmapReward(this);

        // --- REWARD SHAPING ---
        GameObject targetObj = mapManager.GetNearestActivePlayer(transform.position);
        if (targetObj != null) {
            Vector3 toTarget = targetObj.transform.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 1f) {
                Vector3 dirToTarget = toTarget.normalized;
                float lookDot = Vector3.Dot(transform.forward, dirToTarget);

                // Thưởng nhẹ nếu quay mặt về hướng mục tiêu
                if (lookDot > 0.7f) {
                    AddReward(0.001f);
                }

                // Thưởng đậm hơn nếu đang thực sự di chuyển về hướng đó
                Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (flatVel.magnitude > 1f) {
                    float moveDot = Vector3.Dot(flatVel.normalized, dirToTarget);
                    if (moveDot > 0.8f) {
                        AddReward(0.002f);
                    }
                }
            }
        }
    }

    private void FixedUpdate() {
        if (rb == null) return;

        // ═══ ĐÓNG BĂNG KHI ĐANG CUTSCENE INTRO ═══
        if (IslandGameManager.Instance != null && !IslandGameManager.Instance.IsPlaying) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        // ═══ XOAY — liên tục mỗi physics frame ═══
        transform.Rotate(Vector3.up, currentTurnInput * turnSpeed * Time.fixedDeltaTime);

        // ═══ DI CHUYỂN — Sử dụng Lực kéo (AddForce) thay vì ghi đè Velocity ═══
        // Tại sao? Ghi đè Velocity làm AI đâm vào chướng ngại vật vẫn tưởng mình đang đi với 15m/s
        Vector3 targetVelocity = transform.forward * currentMoveInput * moveSpeed;
        Vector3 currentFlatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // Tính lực cần thiết để đạt tới targetVelocity 
        Vector3 velocityChange = targetVelocity - currentFlatVel;

        // Đẩy 1 lực gia tốc Acceleration gấp 10 lần để nó vọt đi nhanh nhưng vẫn bị cản bởi tường
        rb.AddForce(velocityChange * 10f, ForceMode.Acceleration);

        // ═══ KHÓA ĐỘ CAO Y — chống trôi lên/xuống ═══
        Vector3 pos = rb.position;
        pos.y = spawnY;
        rb.position = pos;
        if (Mathf.Abs(rb.linearVelocity.y) > 0.01f) {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        // ═══ ANIMATOR ═══
        if (animator != null) {
            if (HasParameter(animator, "Speed")) {
                float flatSpeed = currentFlatVel.magnitude;
                animator.SetFloat("Speed", flatSpeed);
            }
        }
    }
    private bool HasParameter(Animator anim, string paramName) {
        foreach (AnimatorControllerParameter param in anim.parameters) {
            if (param.name == paramName) return true;
        }
        return false;
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Collision & Trigger

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            mapManager.OnPlayerCaught(other.gameObject, this);
        }
    }

    private void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle")) {
            if (Time.time - lastWallHitTime > 1.0f) {
                AddReward(-0.01f);
                lastWallHitTime = Time.time;
            }
        }
    }

    private void OnCollisionStay(Collision collision) {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle")) {
            // Phạt liên tục nếu cố tình cọ xát/mài mặt vào tường (1 giây phạt 1 lần)
            if (Time.time - lastWallHitTime > 1.0f) {
                AddReward(-0.01f);
                lastWallHitTime = Time.time;
            }
        }
    }
    #endregion
    public void ActiveAll() {
        if (coneObject != null && coneObject.activeSelf == false)
            coneObject.SetActive(true);
    }
}