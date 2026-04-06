using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MapManager : MonoBehaviour {
    [Header("Mode")]
    public bool isGameplayMode = false;

    [Header("Bản đồ & Heatmap")]
    public float mapSize = 30f;
    public int gridResolution = 15;
    private float[,] heatmap;
    private float cellSize;

    [Header("Quản lý Object")]
    public List<AdvancedSeekerDrone> drones;
    public List<GameObject> players;
    public List<Transform> obstacles; // Kéo các bức tường/vật cản vào đây để random

    [Header("Episode Settings")]
    public float stageTimeLimit = 45f;
    public float timeoutPenalty = -1f;

    [Header("Drone Bounds")]
    public float outOfBoundsMargin = 1f;
    public float outOfBoundsPenalty = -0.5f;
    public float minAllowedLocalY = -2f;
    public float maxAllowedLocalY = 8f;
    public float droneSpawnPadding = 2f;
    public float droneSpawnLocalY = 1f;

    [Header("Anti-Stuck")]
    public float stuckSpeedThreshold = 0.25f;
    public float stuckDurationToReset = 2f;
    public float stuckPenalty = -0.25f;

    private int playersFoundThisStage = 0;
    private bool isResetting = false;
    private float stageStartTime = 0f;
    private readonly Dictionary<AdvancedSeekerDrone, float> stuckTimerByDrone = new();

    void Awake() {
        cellSize = mapSize / gridResolution;
        heatmap = new float[gridResolution, gridResolution];
    }

    void Start() {
        if (!isGameplayMode) {
            ResetMap();
        }
    }

    void Update() {
        if (isResetting) return;
        if (drones == null || drones.Count == 0) return;

        // Gameplay mode: không chạy logic reset/respawn của training.
        if (isGameplayMode) {
            return;
        }

        if (Time.time - stageStartTime >= stageTimeLimit) {
            EndStageAndReset(false);
        }

        foreach (var drone in drones) {
            if (drone == null) continue;

            if (IsDroneOutOfBounds(drone.transform.position)) {
                HandleDroneOutOfBounds(drone);
                continue;
            }

            HandleDroneStuckCheck(drone);
        }
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition) {
        Vector3 local = transform.InverseTransformPoint(worldPosition);

        int cellX = Mathf.FloorToInt((local.x + (mapSize / 2f)) / cellSize);
        int cellZ = Mathf.FloorToInt((local.z + (mapSize / 2f)) / cellSize);

        cellX = Mathf.Clamp(cellX, 0, gridResolution - 1);
        cellZ = Mathf.Clamp(cellZ, 0, gridResolution - 1);

        return new Vector2Int(cellX, cellZ);
    }

    // Hàm này được gọi khi bắt đầu Stage mới, hoặc khi Drone bị Timeout
    public void ResetMap() {
        if (isGameplayMode) return;
        if (isResetting) return;
        isResetting = true;

        playersFoundThisStage = 0;
        stageStartTime = Time.time;

        // 1. Reset Heatmap
        for (int i = 0; i < gridResolution; i++) {
            for (int j = 0; j < gridResolution; j++) {
                heatmap[i, j] = Time.time;
            }
        }

        // 2. Randomize Vật cản (Obstacles) - Thay đổi cấu trúc map
        foreach (var obs in obstacles) {
            obs.localPosition = new Vector3(Random.Range(-10f, 10f), 0.5f, Random.Range(-10f, 10f));
            // Lưu ý: Nếu thay đổi vật cản, bạn có thể cần dùng NavMeshSurface để Bake lại runtime, 
            // hoặc thiết kế các vật cản dùng NavMeshObstacle có Carve = true để không phải bake lại.
        }

        // 3. Spawn Players an toàn trên NavMesh
        foreach (var p in players) {
            p.SetActive(true);
            Vector3 safePos = GetRandomNavMeshPosition();
            p.GetComponent<NavMeshAgent>().Warp(safePos); // Bắt buộc dùng Warp thay vì transform.position
        }

        // 4. Báo cho các Drone reset vị trí (Sửa lỗi lơ lửng, tính theo Y cục bộ của MapManager)
        foreach (var d in drones) {
            if (d == null) continue;
            stuckTimerByDrone[d] = 0f;
            ResetSingleDronePosition(d);
        }

        isResetting = false;
    }

    // Drone gọi hàm này để check Heatmap và nhận thưởng
    public void ProcessHeatmapReward(AdvancedSeekerDrone drone) {
        Vector2Int cell = WorldToGrid(drone.transform.position);
        int cellX = cell.x;
        int cellZ = cell.y;

        float timeSinceLastVisit = Time.time - heatmap[cellX, cellZ];

        if (timeSinceLastVisit > 3f) {
            drone.AddReward(timeSinceLastVisit * 0.01f);
            heatmap[cellX, cellZ] = Time.time; // Cập nhật lại độ nóng cho TOÀN BỘ team
        }
    }

    // Drone gọi hàm này khi bắt được Player
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

        if (!caughtPlayer.activeSelf) return; // Tránh việc 2 drone cùng báo bắt 1 lúc

        caughtPlayer.SetActive(false);
        catcherDrone.AddReward(5f); // Thưởng đậm cho cá nhân bắt được

        playersFoundThisStage++;

        // NẾU TÌM THẤY HẾT PLAYER -> CHUYỂN STAGE / RESET
        if (playersFoundThisStage >= players.Count) {
            EndStageAndReset(true);
        }
    }

    public Transform GetNearestGameplayTarget(Vector3 fromPosition) {
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        Transform nearest = null;
        float nearestSqDist = float.MaxValue;

        foreach (GameObject playerObject in playerObjects) {
            if (playerObject == null || !playerObject.activeInHierarchy) continue;

            float sqDist = (playerObject.transform.position - fromPosition).sqrMagnitude;
            if (sqDist < nearestSqDist) {
                nearestSqDist = sqDist;
                nearest = playerObject.transform;
            }
        }

        return nearest;
    }

    public Transform GetNearestTargetForObservations(Vector3 fromPosition) {
        Transform nearest = null;
        float nearestSqDist = float.MaxValue;

        if (players != null && players.Count > 0) {
            foreach (GameObject playerObject in players) {
                if (playerObject == null || !playerObject.activeInHierarchy) continue;

                float sqDist = (playerObject.transform.position - fromPosition).sqrMagnitude;
                if (sqDist < nearestSqDist) {
                    nearestSqDist = sqDist;
                    nearest = playerObject.transform;
                }
            }
        }

        if (nearest != null) return nearest;

        // Fallback cho gameplay scene khi danh sách players chưa được gán.
        return GetNearestGameplayTarget(fromPosition);
    }

    private void EndStageAndReset(bool success) {
        if (isResetting) return;

        foreach (var d in drones) {
            if (d == null) continue;

            if (success) {
                d.AddReward(2f);
            }
            else {
                d.AddReward(timeoutPenalty);
            }

            d.EndEpisode();
        }

        ResetMap();
    }

    private bool IsDroneOutOfBounds(Vector3 worldPosition) {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float half = (mapSize / 2f) + outOfBoundsMargin;

        bool outsideHorizontal = Mathf.Abs(local.x) > half || Mathf.Abs(local.z) > half;
        bool outsideVertical = local.y < minAllowedLocalY || local.y > maxAllowedLocalY;

        return outsideHorizontal || outsideVertical;
    }

    private void HandleDroneOutOfBounds(AdvancedSeekerDrone drone) {
        drone.AddReward(outOfBoundsPenalty);
        ResetSingleDronePosition(drone);
        stuckTimerByDrone[drone] = 0f;
    }

    private void HandleDroneStuckCheck(AdvancedSeekerDrone drone) {
        Rigidbody droneRb = drone.GetComponent<Rigidbody>();
        if (droneRb == null) return;

        float horizontalSpeed = new Vector2(droneRb.linearVelocity.x, droneRb.linearVelocity.z).magnitude;

        if (!stuckTimerByDrone.ContainsKey(drone)) {
            stuckTimerByDrone[drone] = 0f;
        }

        if (horizontalSpeed < stuckSpeedThreshold) {
            stuckTimerByDrone[drone] += Time.deltaTime;

            if (stuckTimerByDrone[drone] >= stuckDurationToReset) {
                drone.AddReward(stuckPenalty);
                ResetSingleDronePosition(drone);
                stuckTimerByDrone[drone] = 0f;
            }
        }
        else {
            stuckTimerByDrone[drone] = 0f;
        }
    }

    private void ResetSingleDronePosition(AdvancedSeekerDrone drone) {
        drone.transform.position = GetRandomDroneSpawnPosition();
        drone.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Rigidbody droneRb = drone.GetComponent<Rigidbody>();
        if (droneRb != null) {
            droneRb.linearVelocity = Vector3.zero;
            droneRb.angularVelocity = Vector3.zero;
        }
    }

    private Vector3 GetRandomDroneSpawnPosition() {
        float half = Mathf.Max(1f, (mapSize / 2f) - droneSpawnPadding);
        Vector3 localSpawn = new Vector3(
            Random.Range(-half, half),
            droneSpawnLocalY,
            Random.Range(-half, half)
        );

        return transform.TransformPoint(localSpawn);
    }

    // Tiện ích: Tìm điểm hợp lệ trên NavMesh để không bị spawn chìm vào tường
    private Vector3 GetRandomNavMeshPosition() {
        Vector3 randomDirection = Random.insideUnitSphere * (mapSize / 2f);
        randomDirection += transform.position; // FIXED: Tính theo vị trí của Scene/Map hiện tại
        randomDirection.y = transform.position.y;
        NavMeshHit hit;
        // Tăng bán kính quét lên mapSize / 2f để luôn luôn tìm được đất NavMesh
        if (NavMesh.SamplePosition(randomDirection, out hit, mapSize / 2f, NavMesh.AllAreas)) {
            return hit.position;
        }
        return transform.position; // Fallback an toàn về tâm Map
    }

    // Tiện ích: Drone gọi hàm này để "Ngửi" ô lưới Heatmap (Hỗ trợ 3x3)
    public float GetHeat(int x, int z) {
        if (x < 0 || x >= gridResolution || z < 0 || z >= gridResolution) {
            return Time.time; // Ra ngoài map coi như đất cực Nóng (cấm đi)
        }
        return heatmap[x, z];
    }
}