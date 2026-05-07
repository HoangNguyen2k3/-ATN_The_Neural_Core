using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Quản lý việc đẻ đệ tử của Boss (Minions) theo thời gian.
/// Gắn script này trực tiếp lên Boss GameObject.
/// </summary>
public class BossMinionSpawner : MonoBehaviour {
    [Header("🥚 Cài đặt Prefab")]
    public GameObject kamikazePrefab;
    public GameObject rangedPrefab;

    [Header("🚪 Cài đặt Cổng (Gate)")]
    [Tooltip("Prefab hiển thị cảnh báo trước khi quái xuất hiện")]
    public GameObject gatePrefab;
    [Tooltip("Thời gian cảnh báo của cổng trước khi biến mất và sinh quái (giây)")]
    public float gateWarningTime = 2f;

    [Header("⏱️ Thông số Triệu hồi")]
    [Tooltip("Thời gian giữa các đợt spawn (giây)")]
    public float spawnInterval = 15f;
    [Tooltip("Số lượng đệ tử tối đa tồn tại cùng lúc")]
    public int maxMinionsAlive = 6;
    [Tooltip("Bán kính tìm điểm spawn quanh Boss")]
    public float spawnRadius = 15f;
    [Tooltip("Khoảng cách tối thiểu so với người chơi để đảm bảo an toàn lúc đẻ trứng")]
    public float minDistanceToPlayer = 8f;

    [Header("⚔️ Tùy chọn Phase")]
    [Tooltip("Nếu true, chỉ bắt đầu đẻ đệ khi Boss dưới ngưỡng máu nhất định (Phase 2)")]
    public bool spawnOnlyInPhase2 = true;

    private BossArenaManager _arenaManager;
    private BossHealthSystem _bossHealth;
    private float _spawnTimer;
    private Transform _playerTransform;

    // Danh sách theo dõi đệ tử còn sống
    private List<GameObject> _aliveMinions = new List<GameObject>();
    private int _pendingSpawns = 0; // Đếm số quái đang trong quá trình chờ từ Cổng

    [Header("==========Spawn minions=========")]
    public int minionsPerWave = 3;
    public float delayBetweenGates = 0.5f;
    void Start() {
        _arenaManager = FindFirstObjectByType<BossArenaManager>();
        _bossHealth = GetComponent<BossHealthSystem>();

        var player = FindFirstObjectByType<PlayerCombatController>();
        if (player != null) {
            _playerTransform = player.transform;
        }

        _spawnTimer = spawnInterval; // Bắt đầu đếm ngược ngay
    }

    void Update() {
        // Chỉ chạy trong Gameplay (không phải lúc Train) và trận đấu đang diễn ra
        if (_arenaManager == null || _arenaManager.isTrainingMode || !_arenaManager.IsFighting) return;

        // Nếu yêu cầu Phase 2 mà máu boss vẫn đầy (Phase 1) thì chưa spawn
        if (spawnOnlyInPhase2 && _bossHealth != null && _bossHealth.CurrentPhase == 1) return;

        // Nếu Boss đã chết thì dừng
        if (_bossHealth != null && _bossHealth.IsDead) return;

        // Dọn dẹp danh sách (loại bỏ các đệ tử đã chết/bị destroy)
        _aliveMinions.RemoveAll(m => m == null);

        // Đếm ngược
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f) {
            _spawnTimer = spawnInterval;

            // Triệu hồi nếu chưa đạt giới hạn (tính cả những con đang chờ gate)
            if (_aliveMinions.Count + _pendingSpawns < maxMinionsAlive) {
                StartCoroutine(SpawnWaveRoutine());
            }
        }
    }
    IEnumerator SpawnWaveRoutine() {
        for (int i = 0; i < minionsPerWave; i++) {
            // Mỗi lần lặp phải kiểm tra lại xem đã đầy slot chưa (tránh đẻ quá max)
            if (_aliveMinions.Count + _pendingSpawns < maxMinionsAlive) {
                TrySpawnMinion();
            }
            else {
                break; // Đã đạt giới hạn tối đa, không đẻ thêm trong đợt này nữa
            }

            // Chờ một chút trước khi tạo cổng tiếp theo trong cùng đợt cho đẹp
            yield return new WaitForSeconds(delayBetweenGates);
        }
    }
    void TrySpawnMinion() {
        // Chọn ngẫu nhiên loại đệ tử
        GameObject prefabToSpawn = (Random.value > 0.5f) ? kamikazePrefab : rangedPrefab;
        if (prefabToSpawn == null) return;

        Vector3 spawnPos = Vector3.zero;
        bool foundPos = false;

        // Thử tìm vị trí hợp lệ nhiều lần
        for (int i = 0; i < 10; i++) {
            Vector3 randomDir = Random.insideUnitSphere * spawnRadius;
            randomDir.y = 0;
            Vector3 randomPos = transform.position + randomDir;

            if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 5f, NavMesh.AllAreas)) {
                // Kểm tra khoảng cách với player
                if (_playerTransform != null) {
                    float distToPlayer = Vector3.Distance(hit.position, _playerTransform.position);
                    if (distToPlayer < minDistanceToPlayer) {
                        continue; // Quá gần player, thử lại
                    }
                }

                spawnPos = hit.position;
                foundPos = true;
                break;
            }
        }

        if (foundPos) {
            StartCoroutine(SpawnWithGateSequence(prefabToSpawn, spawnPos));
        }
        else {
            Debug.LogWarning("[BossMinionSpawner] Không tìm thấy vị trí phù hợp để spawn quái!");
        }
    }

    IEnumerator SpawnWithGateSequence(GameObject prefabToSpawn, Vector3 spawnPos) {
        _pendingSpawns++;
        GameObject gateObj = null;

        // 1. Hiện Gate cảnh báo
        if (gatePrefab != null) {
            gateObj = Instantiate(gatePrefab, spawnPos + new Vector3(0, 1.5f, 0), Quaternion.identity);
        }

        // 2. Chờ 2 giây
        yield return new WaitForSeconds(gateWarningTime);

        // 3. Ẩn Gate (Xóa)
        if (gateObj != null) {
            Destroy(gateObj);
        }

        // Đảm bảo trận đấu chưa kết thúc và boss chưa chết trong lúc chờ
        if (_arenaManager != null && _arenaManager.IsFighting && _bossHealth != null && !_bossHealth.IsDead) {
            // 4. Sinh quái
            GameObject newMinion = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            _aliveMinions.Add(newMinion);
            Debug.Log($"[BossMinionSpawner] Đã triệu hồi {newMinion.name} tại {spawnPos}");
        }

        _pendingSpawns--;
    }

    // Xóa sạch đệ tử khi Boss chết hoặc Game Over
    public void ClearAllMinions() {
        foreach (var m in _aliveMinions) {
            if (m != null) Destroy(m);
        }
        _aliveMinions.Clear();
    }
}
