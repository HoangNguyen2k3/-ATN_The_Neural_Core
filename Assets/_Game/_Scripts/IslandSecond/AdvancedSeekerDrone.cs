using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class AdvancedSeekerDrone : Agent {
    [Header("Cài đặt")]
    public float moveSpeed = 15f; // Tăng nhẹ vì dùng AddForce
    public float maxSpeed = 10f;  // Giới hạn vận tốc tối đa
    public float turnSpeed = 200f;

    [Header("Tham chiếu")]
    public MapManager mapManager;

    private Rigidbody rb;

    public override void Initialize() {
        rb = GetComponent<Rigidbody>();
        if (mapManager == null) {
            mapManager = FindFirstObjectByType<MapManager>();
        }

        if (rb == null || mapManager == null) {
            Debug.LogError("Thiếu tham chiếu Component hoặc MapManager!", this);
            enabled = false;
            return;
        }

        // Cho phép MaxStep = 0 để chạy vô hạn trong lúc test Gameplay
        // if (MaxStep <= 0) MaxStep = 1500;

        // Khóa xoay X, Z để không lật, và khóa luôn Vị trí Y để bay lơ lửng ngang tầm mắt
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
    }

    public override void OnEpisodeBegin() {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // FIX: Chỉ tự reset vị trí khi KHÔNG có màn reset toàn bộ đang diễn ra
        // (Nếu isResetting = true, ResetMap() đã lo việc này, tránh double-spawn)
        if (mapManager != null && !mapManager.isResetting) {
            mapManager.ResetSingleDronePosition(this);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Vertical");
        continuousActionsOut[1] = Input.GetAxis("Horizontal");
    }

    public override void CollectObservations(VectorSensor sensor) {
        // --- OBSERVATION SPACE: 18 giá trị ---

        // [1-3] Vận tốc hiện tại trong hệ tọa độ của Drone (3)
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));

        // [4-5] Vị trí chuẩn hóa trong map (2)
        // Drone biết mình đang ở góc nào của bản đồ (0.0 → 1.0)
        Vector2Int myCell = mapManager.WorldToGrid(transform.position);
        sensor.AddObservation((float)myCell.x / mapManager.gridResolutionX);
        sensor.AddObservation((float)myCell.y / mapManager.gridResolutionZ);

        // [6-14] 9 thông số Heatmap lân cận 3x3 (9)
        for (int x = -1; x <= 1; x++) {
            for (int z = -1; z <= 1; z++) {
                float cellLastVisitTime = mapManager.GetHeat(myCell.x + x, myCell.y + z);
                float timeSinceVisit = Time.time - cellLastVisitTime;
                float normalizedHeat = Mathf.Clamp01(timeSinceVisit / 10f);
                sensor.AddObservation(normalizedHeat);
            }
        }

        // [15-18] Cảm nhận Player gần nhất (4)
        // Drone biết hướng, khoảng cách, và có thấy không
        GameObject nearestPlayer = mapManager.GetNearestActivePlayer(transform.position);
        if (nearestPlayer != null) {
            Vector3 toPlayer = nearestPlayer.transform.position - transform.position;
            float distance = toPlayer.magnitude;
            float normalizedDist = Mathf.Clamp01(distance / 40f); // Phạm vi cảm nhận tối đa 40m

            // Kiểm tra line-of-sight bằng Raycast
            bool canSee = !Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                toPlayer.normalized,
                distance,
                LayerMask.GetMask("Wall", "Obstacle")
            );

            // Hướng đến player trong hệ tọa độ local (trái/phải, trước/sau)
            Vector3 localDir = transform.InverseTransformDirection(toPlayer.normalized);
            sensor.AddObservation(localDir.x);           // Hướng ngang
            sensor.AddObservation(localDir.z);           // Hướng dọc
            sensor.AddObservation(normalizedDist);       // Khoảng cách (0 = gần, 1 = xa)
            sensor.AddObservation(canSee ? 1f : 0f);    // Có nhìn thấy không
        } else {
            // Không có Player active -> giá trị mặc định trung lập
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(1f); // Coi như xa vô cực
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions) {
        if (mapManager == null || rb == null) return;

        // Clamp giá trị để đảm bảo an toàn
        float moveForward = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turnDirection = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        // 1. Xoay Drone
        transform.Rotate(Vector3.up, turnDirection * turnSpeed * Time.fixedDeltaTime);

        // 2. Di chuyển bằng Force (Giúp AI học vật lý tốt hơn và dội tường tự nhiên)
        Vector3 force = transform.forward * moveForward * moveSpeed;
        rb.AddForce(force, ForceMode.Acceleration);

        // Giới hạn tốc độ không cho bay nhanh vô hạn
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVelocity.magnitude > maxSpeed) {
            Vector3 limitedVel = flatVelocity.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
        }

        // 3. Đã khóa Position Y ở trên nên không cần AddForce chống trọng lực nữa

        // 4. Nhận thưởng từ Heatmap (khám phá ô mới)
        mapManager.ProcessHeatmapReward(this);

        // 5. Phạt thời gian tồn tại (Khuyến khích tìm Player nhanh)
        if (MaxStep > 0) {
            AddReward(-1f / MaxStep);
        }

        // 6. Thưởng nhỏ khi nhìn thấy Player (không bị tường che) -> kéo drone đến gần mục tiêu
        GameObject nearestPlayer = mapManager.GetNearestActivePlayer(transform.position);
        if (nearestPlayer != null) {
            Vector3 toPlayer = nearestPlayer.transform.position - transform.position;
            float dist = toPlayer.magnitude;
            bool canSee = !Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                toPlayer.normalized,
                dist,
                LayerMask.GetMask("Wall", "Obstacle")
            );
            if (canSee && dist < 20f) {
                // Thưởng tỷ lệ nghịch với khoảng cách: gần Player = thưởng nhiều hơn
                AddReward((1f - dist / 20f) * 0.001f);
            }
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            mapManager.OnPlayerCaught(other.gameObject, this);
        }
    }

    // Xử lý đâm tường tối ưu
    private void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle")) {
            AddReward(-0.02f); // Phạt một lần đủ đau để AI chừa
        }
    }

    private void OnCollisionStay(Collision collision) {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle")) {
            AddReward(-0.0005f); // Phạt cực nhẹ để AI không thích cọ xát vào tường
        }
    }
}