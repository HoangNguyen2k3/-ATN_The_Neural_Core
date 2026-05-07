using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Minion Type 2: Đứng từ xa bắn laser (Ranged).
/// Yêu cầu có: NavMeshAgent, BossHealthSystem, và Collider.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BossHealthSystem))]
public class MinionRanged : MonoBehaviour {
    [Header("🔫 Thông số Bắn xa")]
    public float attackDamage = 10f;
    public float attackRange = 20f;
    public float fireRate = 3f;
    [Tooltip("Khoảng cách giữ an toàn với người chơi")]
    public float stoppingDistance = 12f;
    
    [Header("⚡ Cơ chế Vận công (Charge)")]
    [Tooltip("Thời gian đứng yên tụ năng lượng trước khi bắn")]
    public float chargeTime = 1.5f;
    public LayerMask hitMask;

    [Header("🎨 Hiệu ứng")]
    public Transform firePoint;
    public GameObject laserVFXPrefab;
    public GameObject deathVFX;

    private NavMeshAgent _agent;
    private BossHealthSystem _health;
    private Transform _targetPlayer;
    private float _fireTimer;
    private bool _isCharging = false;

    void Awake() {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<BossHealthSystem>();

        // Cài đặt agent
        _agent.stoppingDistance = stoppingDistance;
    }

    void Start() {
        // Tìm Player
        var player = FindFirstObjectByType<PlayerCombatController>();
        if (player != null) {
            _targetPlayer = player.transform;
        }

        _health.OnDeath += OnKilled;
        _fireTimer = fireRate; // Bắn phát đầu tiên chậm một chút
    }

    void Update() {
        if (_health.IsDead || _targetPlayer == null) return;

        // Bỏ qua Update di chuyển nếu đang gồng (để đứng yên hoàn toàn)
        if (_isCharging) return;

        float distance = Vector3.Distance(transform.position, _targetPlayer.position);

        // Di chuyển giữ khoảng cách
        if (distance > stoppingDistance) {
            _agent.isStopped = false;
            _agent.SetDestination(_targetPlayer.position);
        }
        else {
            _agent.isStopped = true;
            // Xoay mặt về phía người chơi
            Vector3 dir = (_targetPlayer.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero) {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
            }
        }

        // Kiểm tra bắn Laser
        _fireTimer -= Time.deltaTime;
        if (_fireTimer <= 0f && distance <= attackRange) {
            StartCoroutine(ChargeAndFireSequence());
        }
    }

    IEnumerator ChargeAndFireSequence() {
        _isCharging = true;
        if (_agent.isOnNavMesh) {
            _agent.isStopped = true;
        }

        // 1. Giai đoạn ngắm: Vẫn liên tục xoay theo player trong nửa đầu thời gian charge
        float aimTime = chargeTime * 0.7f;
        float elapsed = 0f;
        while (elapsed < aimTime) {
            if (_targetPlayer == null || _health.IsDead) yield break;

            Vector3 dir = (_targetPlayer.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero) {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2. Giai đoạn khóa mục tiêu: Chốt vị trí của Player ngay lúc này
        if (_targetPlayer == null || _health.IsDead) yield break;
        
        // Đây là điểm mà đệ tử chốt hạ để bắn (tạo cơ hội cho Player né)
        Vector3 lockedTargetPos = _targetPlayer.position + Vector3.up * 1f; 

        // 3. Chờ thêm chút xíu (khoảng khắc tụ xong chuẩn bị xả)
        yield return new WaitForSeconds(chargeTime - aimTime);

        // 4. Bắn Laser vào đúng vị trí đã khóa
        if (!_health.IsDead) {
            FireLaserAt(lockedTargetPos);
        }

        _fireTimer = fireRate;
        _isCharging = false;
    }

    void FireLaserAt(Vector3 targetPosition) {
        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1f;
        Vector3 direction = (targetPosition - origin).normalized;

        Vector3 hitPoint = origin + direction * attackRange;

        // Dùng SphereCast thay vì Raycast để dễ trúng Player hơn chút
        if (Physics.SphereCast(origin, 0.5f, direction, out RaycastHit hit, attackRange, hitMask)) {
            hitPoint = hit.point;

            // Gây sát thương nếu trúng player
            var targetHealth = hit.collider.GetComponent<BossHealthSystem>();
            if (targetHealth != null && targetHealth != _health) {
                targetHealth.TakeDamage(attackDamage);
            }
        }

        // Vẽ tia laser
        if (laserVFXPrefab != null) {
            GameObject vfx = Instantiate(laserVFXPrefab, origin, Quaternion.identity);
            LineRenderer lr = vfx.GetComponent<LineRenderer>();
            if (lr != null) {
                lr.SetPosition(0, origin);
                lr.SetPosition(1, hitPoint);
            }
            Destroy(vfx, 0.2f);
        }
    }

    void OnKilled() {
        if (deathVFX != null) {
            Instantiate(deathVFX, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
        }
        Destroy(gameObject);
    }

    void OnDestroy() {
        if (_health != null) {
            _health.OnDeath -= OnKilled;
        }
    }
}
