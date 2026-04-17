using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MapManager : MonoBehaviour {
    [Header("Mode")]
    [Tooltip("Bật True khi chơi thật. False khi Train AI.")]
    public bool isGameplayMode = false;

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
    [Tooltip("Thời gian tối đa mỗi episode (giây)")]
    public float stageTimeLimit = 120f;
    public float timeoutPenalty = -1f;

    [Header("Bounds Settings (Bảo vệ an toàn)")]
    public float minAllowedLocalY = -2f;  // Chống rớt xuyên sàn
    public float maxAllowedLocalY = 20f;  // Chống bay lọt qua trần nhà
    public float droneSpawnPadding = 5f;  // Cách tường ít nhất 5m lúc spawn
    public float droneSpawnLocalY = 1.5f; // Độ cao thả Drone lúc đầu

    [Header("Stuck Detection (Chống kẹt)")]
    [Tooltip("Tốc độ ngang tối thiểu để không bị tính là kẹt (m/s)")]
    public float stuckSpeedThreshold = 0.05f;
    [Tooltip("Bao nhiêu giây đứng im thì bị tính là kẹt")]
    public float stuckTimeLimit = 10f;
    [Tooltip("Số giây miễn dịch sau khi spawn - tránh phạt oan lúc AI chưa kịp tăng tốc")]
    public float stuckGracePeriod = 8f;

    [HideInInspector] public bool isResetting = false;

    private int playersFoundThisStage = 0;
    private float stageStartTime = 0f;

    // Cache Rigidbody + timer
    private Rigidbody[] droneRigidbodies;
    private float[] droneStuckTimers;
    private Vector3[] droneStuckStartPositions;
    private float[] droneEpisodeStartTimes;

    void Awake() {
        cellWidth = mapSize.x / gridResolutionX;
        cellLength = mapSize.y / gridResolutionZ;
        heatmap = new float[gridResolutionX, gridResolutionZ];
    }

    void Start() {
        droneRigidbodies = new Rigidbody[drones.Count];
        droneStuckTimers = new float[drones.Count];
        droneStuckStartPositions = new Vector3[drones.Count];
        droneEpisodeStartTimes = new float[drones.Count];

        for (int i = 0; i < drones.Count; i++) {
            if (drones[i] != null)
                droneRigidbodies[i] = drones[i].GetComponent<Rigidbody>();
            droneEpisodeStartTimes[i] = Time.time;
        }

        if (!isGameplayMode) {
            ResetMap();
        }
    }

    void Update() {
        if (isResetting || isGameplayMode) return;

        // 1. Kiểm tra hết thời gian (Timeout)
        if (Time.time - stageStartTime >= stageTimeLimit) {
            EndStageAndReset(false);
            return;
        }

        for (int i = 0; i < drones.Count; i++) {
            var drone = drones[i];
            if (drone == null) continue;

            // 2. Kiểm tra lỗi rớt trục Y (Văng ra khỏi map)
            float localY = transform.InverseTransformPoint(drone.transform.position).y;
            if (localY < minAllowedLocalY || localY > maxAllowedLocalY) {
                drone.AddReward(timeoutPenalty);
                drone.EndEpisode(); // Tự reset nó thông qua OnEpisodeBegin
                continue;
            }

            // 3. Cảm biến kẹt bằng khoảng cách (Stuck Detection - Positional)
            if (droneRigidbodies != null && i < droneRigidbodies.Length && droneRigidbodies[i] != null) {
                float timeSinceEpisodeStart = Time.time - droneEpisodeStartTimes[i];

                if (timeSinceEpisodeStart < stuckGracePeriod) {
                    droneStuckStartPositions[i] = drone.transform.position;
                    droneStuckTimers[i] = 0f;
                    continue; // Đang trong thời gian miễn nhiễm
                }

                droneStuckTimers[i] += Time.deltaTime;

                if (droneStuckTimers[i] >= stuckTimeLimit) { // Mỗi chu kỳ 10 giây
                    float distanceMoved = Vector3.Distance(drone.transform.position, droneStuckStartPositions[i]);
                    
                    if (distanceMoved < 3f) { // Nếu di chuyển chưa được 3 mét trong 10 giây => KẸT
                        drone.AddReward(-0.3f);
                        drone.EndEpisode();
                    } else {
                        // Nếu đi được xa hơn 3 mét, reset lại mốc để đo 10 giây tiếp theo
                        droneStuckStartPositions[i] = drone.transform.position;
                        droneStuckTimers[i] = 0f;
                    }
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    #region Quản lý Map & Episode

    public void ResetMap() {
        if (isGameplayMode) return;
        if (!isResetting) isResetting = true;

        playersFoundThisStage = 0;
        stageStartTime = Time.time;

        // 1. Làm nguội toàn bộ Heatmap (Để AI có động lực chạy ngay lúc đầu)
        for (int i = 0; i < gridResolutionX; i++) {
            for (int j = 0; j < gridResolutionZ; j++) {
                heatmap[i, j] = Time.time - 5f;
            }
        }

        // 2. Không reset vật cản (Để AI học ổn định)

        // 3. Hồi sinh và random vị trí Player trên NavMesh
        foreach (var p in players) {
            if (p == null) continue;
            p.SetActive(true);
            Vector3 safePos = GetRandomNavMeshPosition();
            var agent = p.GetComponent<NavMeshAgent>();
            if (agent != null) agent.Warp(safePos);
        }

        // 4. Reset vị trí Drone
        foreach (var d in drones) {
            if (d != null) {
                ResetSingleDronePosition(d);
            }
        }

        isResetting = false;
    }

    private void EndStageAndReset(bool success) {
        if (isResetting) return;
        isResetting = true;

        foreach (var d in drones) {
            if (d == null) continue;

            if (success) {
                d.AddReward(2f); // Thắng thì cả team cộng điểm
            }
            else {
                d.AddReward(timeoutPenalty); // Thua thì cả team bị trừ
            }
            d.EndEpisode();
        }

        ResetMap();
    }

    public void ResetSingleDronePosition(AdvancedSeekerDrone drone) {
        float halfX = Mathf.Max(1f, (mapSize.x / 2f) - droneSpawnPadding);
        float halfZ = Mathf.Max(1f, (mapSize.y / 2f) - droneSpawnPadding);

        Vector3 localXZ = new Vector3(Random.Range(-halfX, halfX), 0f, Random.Range(-halfZ, halfZ));
        Vector3 worldXZ = transform.TransformPoint(localXZ);
        Vector3 finalSpawn = new Vector3(worldXZ.x, transform.position.y + droneSpawnLocalY, worldXZ.z);

        Rigidbody droneRb = drone.GetComponent<Rigidbody>();
        if (droneRb != null) {
            droneRb.linearVelocity = Vector3.zero;
            droneRb.angularVelocity = Vector3.zero;
            droneRb.position = finalSpawn;
        }
        else {
            drone.transform.position = finalSpawn;
        }

        // Reset bộ đếm chống kẹt
        int idx = drones.IndexOf(drone);
        if (idx >= 0 && droneStuckTimers != null && idx < droneStuckTimers.Length) {
            droneStuckTimers[idx] = 0f;
            droneEpisodeStartTimes[idx] = Time.time;
        }
    }

    private Vector3 GetRandomNavMeshPosition() {
        for (int attempt = 0; attempt < 15; attempt++) {
            Vector3 randomLocal = new Vector3(
                Random.Range(-mapSize.x / 2f, mapSize.x / 2f),
                0f,
                Random.Range(-mapSize.y / 2f, mapSize.y / 2f)
            );

            Vector3 randomWorld = transform.TransformPoint(randomLocal);

            if (NavMesh.SamplePosition(randomWorld, out NavMeshHit hit, 15f, NavMesh.AllAreas)) {
                return hit.position;
            }
        }

        Debug.LogWarning("[MapManager] NavMesh sampling thất bại sau 15 lần, thả tạm ở tâm map.", this);
        return transform.position;
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Tương tác với Drone (Observations & Rewards)

    public Vector2Int WorldToGrid(Vector3 worldPosition) {
        Vector3 local = transform.InverseTransformPoint(worldPosition);

        int cellX = Mathf.FloorToInt((local.x + (mapSize.x / 2f)) / cellWidth);
        int cellZ = Mathf.FloorToInt((local.z + (mapSize.y / 2f)) / cellLength);

        cellX = Mathf.Clamp(cellX, 0, gridResolutionX - 1);
        cellZ = Mathf.Clamp(cellZ, 0, gridResolutionZ - 1);

        return new Vector2Int(cellX, cellZ);
    }

    public float GetHeat(int x, int z) {
        if (x < 0 || x >= gridResolutionX || z < 0 || z >= gridResolutionZ) {
            return Time.time;
        }
        return heatmap[x, z];
    }

    public void ProcessHeatmapReward(AdvancedSeekerDrone drone) {
        Vector2Int cell = WorldToGrid(drone.transform.position);
        float timeSinceLastVisit = Time.time - heatmap[cell.x, cell.y];

        if (timeSinceLastVisit > 3f) {
            float explorationReward = Mathf.Min(timeSinceLastVisit * 0.01f, 0.15f);
            drone.AddReward(explorationReward);
            heatmap[cell.x, cell.y] = Time.time;
        }
    }

    public void OnPlayerCaught(GameObject caughtPlayer, AdvancedSeekerDrone catcherDrone) {
        if (isGameplayMode) {
            if (caughtPlayer == null || !caughtPlayer.activeSelf) return;

            HumanPlayerController human = caughtPlayer.GetComponent<HumanPlayerController>();
            if (human != null) {
                IslandGameManager.Instance?.OnHumanPlayerCaught();
                return;
            }

            AICompanion companion = caughtPlayer.GetComponent<AICompanion>();
            if (companion != null) {
                caughtPlayer.SetActive(false);
                IslandGameManager.Instance?.OnCompanionCaught(caughtPlayer);
            }
            return;
        }

        // --- Logic khi đang Train AI ---
        if (!caughtPlayer.activeSelf) return;

        caughtPlayer.SetActive(false);
        catcherDrone.AddReward(5f);
        playersFoundThisStage++;

        if (playersFoundThisStage >= players.Count) {
            EndStageAndReset(true);
        }
    }

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
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Debug Gizmos
    private void OnDrawGizmos() {
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        float yCenter = (maxAllowedLocalY + minAllowedLocalY) / 2f;
        float ySize = maxAllowedLocalY - minAllowedLocalY;
        Vector3 centerOffset = new Vector3(0f, yCenter, 0f);

        // 1. Map Size
        Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
        Vector3 mapBoxSize = new Vector3(mapSize.x, ySize, mapSize.y);
        Gizmos.DrawWireCube(centerOffset, mapBoxSize);

        // 2. Vùng Spawn Drone
        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        float spawnBoxSizeX = Mathf.Max(1f, mapSize.x - droneSpawnPadding * 2f);
        float spawnBoxSizeZ = Mathf.Max(1f, mapSize.y - droneSpawnPadding * 2f);
        Vector3 spawnCenter = new Vector3(0f, droneSpawnLocalY, 0f);
        Vector3 spawnSize = new Vector3(spawnBoxSizeX, 0.1f, spawnBoxSizeZ);
        Gizmos.DrawWireCube(spawnCenter, spawnSize);

        // 3. Lưới Heatmap (Chỉ hiện khi đang chạy game để đỡ rối)
        if (Application.isPlaying && cellWidth > 0 && cellLength > 0) {
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            float startX = -mapSize.x / 2f;
            float startZ = -mapSize.y / 2f;

            for (int x = 0; x <= gridResolutionX; x++) {
                float xPos = startX + x * cellWidth;
                Gizmos.DrawLine(new Vector3(xPos, 0, startZ), new Vector3(xPos, 0, startZ + mapSize.y));
            }
            for (int z = 0; z <= gridResolutionZ; z++) {
                float zPos = startZ + z * cellLength;
                Gizmos.DrawLine(new Vector3(startX, 0, zPos), new Vector3(startX + mapSize.x, 0, zPos));
            }
        }

        Gizmos.matrix = oldMatrix;
    }
    #endregion
}