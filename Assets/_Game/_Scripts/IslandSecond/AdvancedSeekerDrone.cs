using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class AdvancedSeekerDrone : Agent {
    [Header("Cài đặt")]
    public float moveSpeed = 10f;
    public float turnSpeed = 200f;
    public bool allowBackward = false;
    public float minimumForwardInput = 0.15f;
    public float idleSpeedPenalty = -0.002f;
    public float idleSpeedThreshold = 0.4f;

    [Header("Tham chiếu")]
    public MapManager mapManager; // Kéo object chứa MapManager vào đây

    private Rigidbody rb;
    public GameObject cone;
    public void ActiveAll() {
        cone.SetActive(true);
    }
    public override void Initialize() {
        rb = GetComponent<Rigidbody>();
        if (mapManager == null) {
            mapManager = FindFirstObjectByType<MapManager>();
        }

        if (rb == null) {
            Debug.LogError("AdvancedSeekerDrone requires a Rigidbody component.", this);
            enabled = false;
            return;
        }

        if (mapManager == null) {
            Debug.LogError("AdvancedSeekerDrone requires a MapManager reference.", this);
            enabled = false;
            return;
        }

        if (MaxStep <= 0) {
            MaxStep = 1500;
        }

        // Chỉ khóa xoay X và Z để không bị lật ngửa, cho phép rơi tự do theo trục Y
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnEpisodeBegin() {
        // Khi Drone bị timeout (hết MaxStep), ML-Agents sẽ gọi hàm này.
        // Báo MapManager reset lại toàn bộ map.
        //mapManager.ResetMap(); // Đã bật lại để Reset hoạt động
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // CHO PHÉP ĐIỀU KHIỂN BẰNG BÀN PHÍM ĐỂ TEST TRONG EDITOR
    public override void Heuristic(in ActionBuffers actionsOut) {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Vertical");   // Tiến/Lùi (W/S)
        continuousActionsOut[1] = Input.GetAxis("Horizontal"); // Xoay trái/phải (A/D)
    }

    public override void CollectObservations(VectorSensor sensor) {
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));

        // TÍNH TOÁN VÀ TRUYỀN 9 THÔNG SỐ HEATMAP (LƯỚI 3x3) CHO AI "NGỬI"
        Vector2Int myCell = mapManager.WorldToGrid(transform.position);
        int myCellX = myCell.x;
        int myCellZ = myCell.y;

        for (int x = -1; x <= 1; x++) {
            for (int z = -1; z <= 1; z++) {
                float cellLastVisitTime = mapManager.GetHeat(myCellX + x, myCellZ + z);
                float timeSinceVisit = Time.time - cellLastVisitTime;

                // Chuẩn hóa: Càng lâu chưa đến = giá trị càng gần 1 (ngon). Mới đến = 0 (dở)
                float normalizedHeat = Mathf.Clamp01(timeSinceVisit / 10f);
                sensor.AddObservation(normalizedHeat);
            }
        }

        // Bổ sung 6 quan sát ngữ cảnh mục tiêu để khớp vector size = 18.
        // 1) Hướng local đến mục tiêu gần nhất (3)
        // 2) Khoảng cách chuẩn hóa (1)
        // 3) Độ thẳng hướng nhìn tới mục tiêu (1)
        // 4) Tỉ lệ tốc độ ngang hiện tại (1)
        Transform target = mapManager.GetNearestTargetForObservations(transform.position);
        Vector3 toTargetWorld = Vector3.zero;
        float normalizedDistance = 0f;
        float facingDot = 0f;

        if (target != null) {
            toTargetWorld = target.position - transform.position;
            toTargetWorld.y = 0f;

            float distance = toTargetWorld.magnitude;
            normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(1f, mapManager.mapSize));

            if (distance > 0.001f) {
                Vector3 targetDir = toTargetWorld / distance;
                facingDot = Vector3.Dot(transform.forward, targetDir);
            }
        }

        Vector3 localTargetDir = transform.InverseTransformDirection(toTargetWorld);
        if (localTargetDir.sqrMagnitude > 0.0001f) {
            localTargetDir.Normalize();
        }

        float horizontalSpeedRatio = Mathf.Clamp01(
            new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude / Mathf.Max(0.01f, moveSpeed)
        );

        sensor.AddObservation(localTargetDir);
        sensor.AddObservation(normalizedDistance);
        sensor.AddObservation(facingDot);
        sensor.AddObservation(horizontalSpeedRatio);
    }

    public override void OnActionReceived(ActionBuffers actions) {
        if (mapManager == null || rb == null) return;

        float moveForward = actions.ContinuousActions[0];
        float turnDirection = actions.ContinuousActions[1];

        if (!allowBackward) {
            // Chuyển dải [-1, 1] thành [0, 1] để bot ưu tiên tiến về trước.
            moveForward = Mathf.Clamp01((moveForward + 1f) * 0.5f);
            if (moveForward > 0f && moveForward < minimumForwardInput) {
                moveForward = minimumForwardInput;
            }
        }
        else {
            if (Mathf.Abs(moveForward) > 0f && Mathf.Abs(moveForward) < minimumForwardInput) {
                moveForward = Mathf.Sign(moveForward) * minimumForwardInput;
            }
        }

        Vector3 moveDir = transform.forward * moveForward * moveSpeed;
        rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z); // Giữ nguyên Y để rớt vật lý tự nhiên
        transform.Rotate(Vector3.up, turnDirection * turnSpeed * Time.fixedDeltaTime);

        // NHỜ MANAGER CHECK HEATMAP
        mapManager.ProcessHeatmapReward(this);

        if (MaxStep > 0) {
            AddReward(-1f / MaxStep);
        }

        if (new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude < idleSpeedThreshold) {
            AddReward(idleSpeedPenalty);
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            // BÁO CÁO MANAGER ĐÃ BẮT ĐƯỢC
            mapManager.OnPlayerCaught(other.gameObject, this);
        }
    }

    // XỬ LÝ ĐÂM TƯỜNG (PHẠT NHẸ, KHÔNG END EPISODE ĐỂ KHÔNG HỎNG MULTI-AGENT)
    private void OnCollisionStay(Collision collision) {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle")) {
            AddReward(-0.01f); // Trừ điểm liên tục nếu cứ cạ vào tường
        }
    }
}