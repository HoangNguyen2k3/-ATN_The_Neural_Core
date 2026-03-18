using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class AdvancedSeekerDrone : Agent {
    [Header("Cài đặt")]
    public float moveSpeed = 10f;
    public float turnSpeed = 200f;

    [Header("Tham chiếu")]
    public MapManager mapManager; // Kéo object chứa MapManager vào đây

    private Rigidbody rb;

    public override void Initialize() {
        rb = GetComponent<Rigidbody>();
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
        int myCellX = Mathf.FloorToInt((transform.localPosition.x + (mapManager.mapSize / 2)) / (mapManager.mapSize / mapManager.gridResolution));
        int myCellZ = Mathf.FloorToInt((transform.localPosition.z + (mapManager.mapSize / 2)) / (mapManager.mapSize / mapManager.gridResolution));

        for (int x = -1; x <= 1; x++) {
            for (int z = -1; z <= 1; z++) {
                float cellLastVisitTime = mapManager.GetHeat(myCellX + x, myCellZ + z);
                float timeSinceVisit = Time.time - cellLastVisitTime;

                // Chuẩn hóa: Càng lâu chưa đến = giá trị càng gần 1 (ngon). Mới đến = 0 (dở)
                float normalizedHeat = Mathf.Clamp01(timeSinceVisit / 10f);
                sensor.AddObservation(normalizedHeat);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions) {
        float moveForward = actions.ContinuousActions[0];
        float turnDirection = actions.ContinuousActions[1];

        Vector3 moveDir = transform.forward * moveForward * moveSpeed;
        rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z); // Giữ nguyên Y để rớt vật lý tự nhiên
        transform.Rotate(Vector3.up, turnDirection * turnSpeed * Time.fixedDeltaTime);

        // NHỜ MANAGER CHECK HEATMAP
        mapManager.ProcessHeatmapReward(this);

        if (MaxStep > 0) {
            AddReward(-1f / MaxStep);
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