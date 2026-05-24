using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BossHealthSystem))]
public class SimpleFinalBoss : MonoBehaviour {
    public enum BossType { Melee, Ranged }

    [Header("Settings")]
    public BossType bossType = BossType.Melee;
    public float speed = 4f;
    public float attackDamage = 12f;

    [Header("Melee")]
    public float meleeAttackRange = 2.5f;
    public float meleeAttackCooldown = 1.5f;

    [Header("Ranged")]
    public float rangedKeepDistance = 10f;
    public float rangedFleeDistance = 5f;
    public float rangedFireCooldown = 2.5f;
    public float rangedProjectileSpeed = 12f;
    public GameObject projectilePrefab;
    public Transform projectileOrigin;

    [Header("Enrage VFX")]
    public ParticleSystem enrageParticle;

    // Set by FinalBossArenaManager
    [HideInInspector] public Transform playerTransform;
    [HideInInspector] public BossHealthSystem playerHealth;

    public event Action OnDied;

    [Header("Death")]
    [Tooltip("Thời gian chờ trước khi tắt boss sau khi chết (dành cho animation chết)")]
    public float deathHideDelay = 1.2f;

    private BossHealthSystem _hp;
    private float _attackTimer;
    private bool _isDead;
    private bool _isActivated; // Boss đứng yên cho đến khi Activate() được gọi

    private NavMeshAgent _agent;
    public GameObject particleDead;
    void Awake() {
        _hp = GetComponent<BossHealthSystem>();
        if (_hp != null) _hp.OnDeath += HandleDeath;

        _agent = GetComponent<NavMeshAgent>();

        // Tắt agent ngay từ đầu, Activate() sẽ bật lại
        if (_agent != null) {
            _agent.enabled = false;
            _agent.updateRotation = false; // Tắt tự động xoay của Agent để script tự xử lý xoay mượt
        }
    }

    void OnDestroy() {
        if (_hp != null) _hp.OnDeath -= HandleDeath;
    }

    /// <summary>
    /// FinalBossArenaManager gọi sau khi warp boss về spawn point.
    /// </summary>
    public void Activate() {
        _isActivated = true;
        if (_agent != null) {
            _agent.enabled = true;

            // QUAN TRỌNG: Warp agent vào vị trí hiện tại để đảm bảo bám NavMesh sau khi bị tele
            _agent.Warp(transform.position);

            _agent.autoBraking = false;  // Tắt auto-brake → boss không dừng sờm trước khi đến nơi
            _agent.speed = speed;
            _agent.stoppingDistance = (bossType == BossType.Melee)
                ? meleeAttackRange - 0.3f
                : 0f; // Với Ranged, ta tự xử lý khoảng cách nên set stopping distance về 0 để tránh giật

            Debug.Log($"[Boss:{name}] Activate() → agent enabled, isOnNavMesh={_agent.isOnNavMesh}");
        }
        else {
            Debug.LogWarning($"[Boss:{name}] Activate() → không có NavMeshAgent! Sẽ dùng fallback transform.");
        }
    }

    void Update() {
        if (_isDead || !_isActivated || playerTransform == null) return;

        _attackTimer -= Time.deltaTime;

        HandleRotation(); // Xử lý việc xoay mặt về phía player

        if (bossType == BossType.Melee) UpdateMelee();
        else UpdateRanged();
    }

    // Hàm xoay mặt mượt mà về hướng player
    void HandleRotation() {
        Vector3 dirToPlayer = playerTransform.position - transform.position;
        dirToPlayer.y = 0f;

        if (dirToPlayer.sqrMagnitude > 0.01f) {
            Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
        }
    }

    void UpdateMelee() {
        float dist = Vector3.Distance(transform.position, playerTransform.position);

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh) {
            _agent.SetDestination(playerTransform.position);
        }
        else {
            // Fallback nếu không có NavMesh
            if (dist > meleeAttackRange) {
                Vector3 dir = (playerTransform.position - transform.position).normalized;
                dir.y = 0;
                transform.position += dir * speed * Time.deltaTime;
            }
        }

        // Tấn công
        if (dist <= meleeAttackRange && _attackTimer <= 0f) {
            _attackTimer = meleeAttackCooldown;
            playerHealth?.TakeDamage(attackDamage);

            // Xử lý giật màn hình an toàn
            // CombatJuice.ShakeMedium(); // Gọi nếu hệ thống Juice của bạn đã được khởi tạo
        }
    }

    void UpdateRanged() {
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
        dirToPlayer.y = 0f;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh) {
            Vector3 targetPos = transform.position;

            if (dist < rangedFleeDistance) {
                // Bị áp sát quá -> lùi lại
                targetPos = transform.position - dirToPlayer * 3f;
            }
            else if (dist > rangedKeepDistance) {
                // Xa quá -> Tiến lại gần player
                targetPos = playerTransform.position;
            }
            else {
                // Ở cự ly đẹp -> Strafe (Di chuyển ngang)
                Vector3 strafeDir = Vector3.Cross(Vector3.up, dirToPlayer).normalized;
                // Có thể random đổi hướng strafe trái/phải nếu muốn phức tạp hơn
                targetPos = transform.position + strafeDir * 2f;
            }

            _agent.SetDestination(targetPos);
        }
        else {
            // Fallback Transform
            if (dist < rangedFleeDistance)
                transform.position -= dirToPlayer * speed * Time.deltaTime;
            else if (dist > rangedKeepDistance)
                transform.position += dirToPlayer * speed * Time.deltaTime;
            else {
                Vector3 strafe = Vector3.Cross(Vector3.up, dirToPlayer).normalized;
                transform.position += strafe * speed * 0.5f * Time.deltaTime;
            }
        }

        // Bắn đạn
        if (_attackTimer <= 0f) {
            _attackTimer = rangedFireCooldown;
            FireProjectile();
        }
    }

    void FireProjectile() {
        if (projectilePrefab == null || playerHealth == null) return;

        Vector3 origin = projectileOrigin != null
            ? projectileOrigin.position
            : transform.position + Vector3.up * 1.2f;

        // Canh tọa độ bắn vào tâm của Player
        Vector3 targetPoint = playerTransform.position + Vector3.up * 1f;
        Vector3 dir = (targetPoint - origin).normalized;

        GameObject proj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
        var script = proj.GetComponent<FinalBossProjectile>();
        if (script != null) script.Init(playerHealth, attackDamage);
    }

    public void SetEnrage(bool enraged) {
        if (_agent != null) _agent.speed = speed;

        if (enrageParticle != null) {
            if (enraged && !enrageParticle.isPlaying) enrageParticle.Play();
            else if (!enraged && enrageParticle.isPlaying) enrageParticle.Stop();
        }
    }

    void HandleDeath() {
        if (_isDead) return; // Tránh gọi nhiều lần
        _isDead = true;
        Debug.Log($"[Boss:{name}] HandleDeath() — bắt đầu ẩn sau {deathHideDelay}s");

        if (_agent != null && _agent.isOnNavMesh) {
            _agent.isStopped = true;
            _agent.enabled = false;
        }

        // Báo cho Arena Manager trước
        OnDied?.Invoke();

        // Ẩn boss sau một khoảng trễ (cho animation chết chạy xong)
        StartCoroutine(HideAfterDelay());
    }

    System.Collections.IEnumerator HideAfterDelay() {
        // Vô hiệu hóa collider ngay lập tức (không bị bắn thêm)
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        yield return new UnityEngine.WaitForSeconds(deathHideDelay);
        Instantiate(particleDead, transform.position, Quaternion.identity);
        gameObject.SetActive(false);
    }
}