using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MapManager : MonoBehaviour {
    [Header("Cấu hình Bản đồ Hình chữ nhật")]
    [Tooltip("X: Chiều Rộng (Width), Y: Chiều Dài (Length/Trục Z trong 3D)")]
    public Vector2 mapSize = new Vector2(200f, 100f);

    [Tooltip("Số ô chia theo chiều rộng X")]
    public int gridResolutionX = 40;
    [Tooltip("Số ô chia theo chiều dài Z")]
    public int gridResolutionZ = 20;

    private float[,] heatmap;
    private float cellWidth;
    private float cellLength;

    [Header("Quản lý Object")]
    public List<AdvancedSeekerDrone> drones;
    public List<GameObject> players;
    public List<Transform> obstacles;

    [Header("Episode Settings")]
    [Tooltip("Thời gian tối đa mỗi episode (giây). Map 200x100 cần ít nhất 120s")]
    public float stageTimeLimit = 120f;
    public float timeoutPenalty = -1f;

    [Header("Bounds Settings (Bảo vệ an toàn)")]
    public float minAllowedLocalY = -2f;  // Chống rớt xuyên sàn
    public float maxAllowedLocalY = 20f;  // Chống bay lọt qua trần nhà
    public float droneSpawnPadding = 5f;  // Cách tường ít nhất 5m lúc spawn
    public float droneSpawnLocalY = 1.5f; // Độ cao thả Drone lúc đầu

    private int playersFoundThisStage = 0;
    // [HideInInspector] public để Drone đọc được trong OnEpisodeBegin, tránh double-reset
    [HideInInspector] public bool isResetting = false;
    private float stageStartTime = 0f;

    [Header("Obstacle Settings")]
    [Tooltip("Khoảng cách tối thiểu giữa các obstacle khi spawn (meter)")]
    public float minObstacleSpacing = 5f;

    [Header("Stuck Detection")]
    [Tooltip("Tốc độ ngang tối thiểu để không bị tính là kẹt (m/s)")]
    public float stuckSpeedThreshold = 0.05f;
    [Tooltip("Bao nhiêu giây đứng im thì bị tính là kẹt")]
    public float stuckTimeLimit = 10f;
    [Tooltip("Số giây miễn dịch sau khi spawn - tránh reset sớm khi drone chưa kịp tăng tốc")]
    public float stuckGracePeriod = 8f;

    // Cache Rigidbody + stuck timer + thời điểm bắt đầu episode của từng drone
    private Rigidbody[] droneRigidbodies;
    private float[] droneStuckTimers;
    private float[] droneEpisodeStartTimes;

    void Awake() {
        // Tính toán kích thước mỗi ô lưới
        cellWidth = mapSize.x / gridResolutionX;
        cellLength = mapSize.y / gridResolutionZ;
        heatmap = new float[gridResolutionX, gridResolutionZ];
    }

    void Start() {
        // Cache Rigidbody của từng drone + khởi tạo mảng timer
        droneRigidbodies      = new Rigidbody[drones.Count];
        droneStuckTimers      = new float[drones.Count];
        droneEpisodeStartTimes = new float[drones.Count];
        for (int i = 0; i < drones.Count; i++) {
            if (drones[i] != null)
                droneRigidbodies[i] = drones[i].GetComponent<Rigidbody>();
            droneEpisodeStartTimes[i] = Time.time;
        }
        ResetMap();
    }

    void Update() {
        if (isResetting) return;

        // 1. Kiểm tra hết thời gian (Timeout)
        if (Time.time - stageStartTime >= stageTimeLimit) {
            EndStageAndReset(false);
            return;
        }

        for (int i = 0; i < drones.Count; i++) {
            var drone = drones[i];
            if (drone == null) continue;

            // 2. Kiểm tra lỗi rớt trục Y
            float localY = transform.InverseTransformPoint(drone.transform.position).y;
            if (localY < minAllowedLocalY || localY > maxAllowedLocalY) {
                drone.AddReward(timeoutPenalty);
                drone.EndEpisode();
                continue;
            }

            // 3. Stuck Detection
            if (droneRigidbodies != null && i < droneRigidbodies.Length && droneRigidbodies[i] != null) {
                // BUG FIX: Grace period - không check stuck trong N giây đầu episode
                // Lý do: drone mới spawn velocity=0, network chưa output action → timer đếm sai
                float timeSinceEpisodeStart = (i < droneEpisodeStartTimes.Length)
                    ? Time.time - droneEpisodeStartTimes[i]
                    : float.MaxValue;

                if (timeSinceEpisodeStart < stuckGracePeriod) continue; // Chưa qua grace period, bỏ qua

                Rigidbody rb = droneRigidbodies[i];
                float flatSpeed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;

                if (flatSpeed < stuckSpeedThreshold) {
                    droneStuckTimers[i] += Time.deltaTime;
                    if (droneStuckTimers[i] >= stuckTimeLimit) {
                        drone.AddReward(-0.3f);
                        droneStuckTimers[i] = 0f;
                        drone.EndEpisode();
                    }
                } else {
                    droneStuckTimers[i] = 0f;
                }
            }
        }
    }

    // Chuyển tọa độ thế giới sang tọa độ lưới 2D hình chữ nhật
    public Vector2Int WorldToGrid(Vector3 worldPosition) {
        Vector3 local = transform.InverseTransformPoint(worldPosition);

        int cellX = Mathf.FloorToInt((local.x + (mapSize.x / 2f)) / cellWidth);
        int cellZ = Mathf.FloorToInt((local.z + (mapSize.y / 2f)) / cellLength);

        // Đảm bảo không bị văng lỗi Out of Index
        cellX = Mathf.Clamp(cellX, 0, gridResolutionX - 1);
        cellZ = Mathf.Clamp(cellZ, 0, gridResolutionZ - 1);

        return new Vector2Int(cellX, cellZ);
    }

    // Drone gọi hàm này để đọc giá trị độ "Nóng" của ô lưới
    public float GetHeat(int x, int z) {
        if (x < 0 || x >= gridResolutionX || z < 0 || z >= gridResolutionZ) {
            return Time.time; // Nếu ngửi ra ngoài map thì coi như cấm đi
        }
        return heatmap[x, z];
    }

    // Drone gọi hàm này để cộng điểm khám phá map
    public void ProcessHeatmapReward(AdvancedSeekerDrone drone) {
        Vector2Int cell = WorldToGrid(drone.transform.position);
        float timeSinceLastVisit = Time.time - heatmap[cell.x, cell.y];

        // Nếu ô này đã trên 3 giây chưa ai đến -> Có thưởng
        // FIX: Cap reward tối đa 0.15f để tránh spike reward lúc đầu episode
        if (timeSinceLastVisit > 3f) {
            float explorationReward = Mathf.Min(timeSinceLastVisit * 0.01f, 0.15f);
            drone.AddReward(explorationReward);
            heatmap[cell.x, cell.y] = Time.time; // Làm nóng ô này lên
        }
    }

    // Drone gọi hàm này khi bắt được Player
    public void OnPlayerCaught(GameObject caughtPlayer, AdvancedSeekerDrone catcherDrone) {
        if (!caughtPlayer.activeSelf) return;

        caughtPlayer.SetActive(false);
        catcherDrone.AddReward(5f); // Thưởng lớn
        playersFoundThisStage++;

        // Nếu bắt hết Player thì thắng
        if (playersFoundThisStage >= players.Count) {
            EndStageAndReset(true);
        }
    }

    // Kết thúc màn chơi và cộng/trừ điểm tổng kết
    private void EndStageAndReset(bool success) {
        if (isResetting) return;
        // FIX: Set cờ TRƯỚC khi gọi EndEpisode để tránh double-reset
        // -> OnEpisodeBegin của Drone sẽ check isResetting và bỏ qua ResetSingleDronePosition
        // -> ResetMap() phía dưới sẽ là nơi duy nhất đặt lại vị trí
        isResetting = true;

        foreach (var d in drones) {
            if (d == null) continue;

            if (success) {
                d.AddReward(2f); // Thắng thì cả team được cộng điểm
            }
            else {
                d.AddReward(timeoutPenalty); // Thua thì cả team bị trừ
            }
            d.EndEpisode();
        }

        ResetMap();
    }

    // Reset toàn bộ môi trường
    public void ResetMap() {
        // FIX: Nếu đã được set từ EndStageAndReset thì bỏ qua guard, chỉ block re-entry thực sự
        if (!isResetting) isResetting = true;

        playersFoundThisStage = 0;
        stageStartTime = Time.time;

        // 1. Làm nguội toàn bộ Heatmap
        // FIX QUAN TRỌNG: Trước đây = Time.time → tất cả ô "nóng" → drone phải đợi 3s mới có reward
        // Giờ = Time.time - 5f → tất cả ô đã "nguội" 5 giây → drone được thưởng ngay khi di chuyển
        for (int i = 0; i < gridResolutionX; i++) {
            for (int j = 0; j < gridResolutionZ; j++) {
                heatmap[i, j] = Time.time - 5f;
            }
        }

        // 2. Obstacle: GIỬ NGUYÊN VỊ TRÍ BẢI ĐẶT TRong Scene, không random lại
        // (Random obstacle khiến drone bị kẹp và NavMesh Player bị block)

        // 3. Hồi sinh và random vị trí Player trên NavMesh
        foreach (var p in players) {
            p.SetActive(true);
            Vector3 safePos = GetRandomNavMeshPosition();
            p.GetComponent<NavMeshAgent>().Warp(safePos);
        }

        // 4. Reset vị trí Drone
        foreach (var d in drones) {
            if (d != null) {
                ResetSingleDronePosition(d);
            }
        }

        isResetting = false;
    }

    // Reset vị trí 1 con Drone độc lập (Drone tự gọi ở hàm OnEpisodeBegin)
    public void ResetSingleDronePosition(AdvancedSeekerDrone drone) {
        float halfX = Mathf.Max(1f, (mapSize.x / 2f) - droneSpawnPadding);
        float halfZ = Mathf.Max(1f, (mapSize.y / 2f) - droneSpawnPadding);

        Vector3 localXZ = new Vector3(Random.Range(-halfX, halfX), 0f, Random.Range(-halfZ, halfZ));
        Vector3 worldXZ  = transform.TransformPoint(localXZ);
        Vector3 finalSpawn = new Vector3(worldXZ.x, transform.position.y + droneSpawnLocalY, worldXZ.z);

        Rigidbody droneRb = drone.GetComponent<Rigidbody>();
        if (droneRb != null) {
            droneRb.linearVelocity  = Vector3.zero;
            droneRb.angularVelocity = Vector3.zero;
            droneRb.position        = finalSpawn;
        } else {
            drone.transform.position = finalSpawn;
        }

        // BUG FIX: Reset stuck timer + ghi nhận thời điểm episode mới bắt đầu
        // Nếu không làm điều này, grace period sẽ tính sai từ thời điểm Start() thay vì lần spawn này
        int idx = drones.IndexOf(drone);
        if (idx >= 0 && droneStuckTimers != null && idx < droneStuckTimers.Length) {
            droneStuckTimers[idx]       = 0f;
            droneEpisodeStartTimes[idx] = Time.time;
        }
    }

    // Tìm một điểm an toàn trên NavMesh nằm trong khu vực Map chữ nhật
    // FIX: Thử 15 lần thay vì 1 lần → tránh Worker crash khi NavMesh bị block
    private Vector3 GetRandomNavMeshPosition() {
        for (int attempt = 0; attempt < 15; attempt++) {
            Vector3 randomLocal = new Vector3(
                Random.Range(-mapSize.x / 2f, mapSize.x / 2f),
                0f,
                Random.Range(-mapSize.y / 2f, mapSize.y / 2f)
            );

            Vector3 randomWorld = transform.TransformPoint(randomLocal);

            // Tăng bán kính tìm kiếm lên 15m để dễ hit NavMesh hơn
            if (NavMesh.SamplePosition(randomWorld, out NavMeshHit hit, 15f, NavMesh.AllAreas)) {
                return hit.position;
            }
        }

        // Sau 15 lần vẫn thất bại → log cảnh báo và trả về tâm map
        Debug.LogWarning("[MapManager] NavMesh sampling thất bại sau 15 lần, dùng tâm map.", this);
        return transform.position;
    }

    // Tìm Player active gần nhất từ một vị trí (Drone dùng để quan sát)
    public GameObject GetNearestActivePlayer(Vector3 fromPosition) {
        GameObject nearest = null;
        float nearestDistSq = float.MaxValue;

        foreach (var p in players) {
            if (p == null || !p.activeSelf) continue;
            float distSq = (fromPosition - p.transform.position).sqrMagnitude;
            if (distSq < nearestDistSq) {
                nearestDistSq = distSq;
                nearest = p;
            }
        }
        return nearest;
    }

    // [GỢI Ý] Vẽ khung hình chữ nhật trong Scene để dễ canh chỉnh tường
    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(mapSize.x, maxAllowedLocalY, mapSize.y));
    }
}