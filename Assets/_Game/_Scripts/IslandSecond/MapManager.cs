using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MapManager : MonoBehaviour {
    [Header("Bản đồ & Heatmap")]
    public float mapSize = 30f;
    public int gridResolution = 15;
    private float[,] heatmap;
    private float cellSize;

    [Header("Quản lý Object")]
    public List<AdvancedSeekerDrone> drones;
    public List<GameObject> players;
    public List<Transform> obstacles; // Kéo các bức tường/vật cản vào đây để random

    private int playersFoundThisStage = 0;
    private bool isResetting = false;

    void Awake() {
        cellSize = mapSize / gridResolution;
        heatmap = new float[gridResolution, gridResolution];
    }

    // Hàm này được gọi khi bắt đầu Stage mới, hoặc khi Drone bị Timeout
    public void ResetMap() {
        if (isResetting) return;
        isResetting = true;

        playersFoundThisStage = 0;

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
            Vector3 randomPos = new Vector3(Random.Range(-12f, 12f), 1f, Random.Range(-12f, 12f));
            d.transform.position = transform.position + randomPos;
        }

        isResetting = false;
    }

    // Drone gọi hàm này để check Heatmap và nhận thưởng
    public void ProcessHeatmapReward(AdvancedSeekerDrone drone) {
        int cellX = Mathf.FloorToInt((drone.transform.localPosition.x + (mapSize / 2)) / cellSize);
        int cellZ = Mathf.FloorToInt((drone.transform.localPosition.z + (mapSize / 2)) / cellSize);

        cellX = Mathf.Clamp(cellX, 0, gridResolution - 1);
        cellZ = Mathf.Clamp(cellZ, 0, gridResolution - 1);

        float timeSinceLastVisit = Time.time - heatmap[cellX, cellZ];

        if (timeSinceLastVisit > 3f) {
            drone.AddReward(timeSinceLastVisit * 0.01f);
            heatmap[cellX, cellZ] = Time.time; // Cập nhật lại độ nóng cho TOÀN BỘ team
        }
    }

    // Drone gọi hàm này khi bắt được Player
    public void OnPlayerCaught(GameObject caughtPlayer, AdvancedSeekerDrone catcherDrone) {
        if (!caughtPlayer.activeSelf) return; // Tránh việc 2 drone cùng báo bắt 1 lúc

        caughtPlayer.SetActive(false);
        catcherDrone.AddReward(5f); // Thưởng đậm cho cá nhân bắt được

        playersFoundThisStage++;

        // NẾU TÌM THẤY HẾT PLAYER -> CHUYỂN STAGE / RESET
        if (playersFoundThisStage >= players.Count) {
            // Thưởng thêm tinh thần đồng đội cho tất cả các con Drone
            foreach (var d in drones) {
                d.AddReward(2f);
                d.EndEpisode(); // Reset bộ não ML-Agents
            }
            ResetMap(); // Setup lại map
        }
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