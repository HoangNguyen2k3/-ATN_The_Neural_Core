using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Minion Type 1: Lao vào người chơi và tự bạo (Kamikaze).
/// Yêu cầu có: NavMeshAgent, BossHealthSystem, và Collider.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BossHealthSystem))]
public class MinionKamikaze : MonoBehaviour {
    [Header("💥 Thông số Tự bạo")]
    public float explosionDamage = 20f;
    public float explosionRadius = 3f;
    public float explosionDelay = 0.5f;
    public LayerMask targetLayer;

    [Header("🎨 Hiệu ứng")]
    public GameObject explosionVFX;
    public Material flashMaterial;

    private NavMeshAgent _agent;
    private BossHealthSystem _health;
    private Transform _targetPlayer;
    private Renderer _meshRenderer;
    private Material _originalMaterial;

    private bool _isExploding = false;

    void Awake() {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<BossHealthSystem>();
        _meshRenderer = GetComponentInChildren<Renderer>();
        if (_meshRenderer != null) _originalMaterial = _meshRenderer.material;
    }

    void Start() {
        // Tìm Player làm mục tiêu
        var player = FindFirstObjectByType<PlayerCombatController>();
        if (player != null) {
            _targetPlayer = player.transform;
        }

        // Đăng ký sự kiện chết (bị bắn chết trước khi nổ)
        _health.OnDeath += OnKilledByPlayer;
    }

    void Update() {
        if (_isExploding || _health.IsDead || _targetPlayer == null) return;

        // Đuổi theo Player
        _agent.SetDestination(_targetPlayer.position);

        // Kiểm tra khoảng cách
        if (Vector3.Distance(transform.position, _targetPlayer.position) <= explosionRadius) {
            StartCoroutine(ExplodeSequence());
        }
    }

    IEnumerator ExplodeSequence() {
        _isExploding = true;
        _agent.isStopped = true;

        // Hiệu ứng nhấp nháy cảnh báo
        if (_meshRenderer != null && flashMaterial != null) {
            _meshRenderer.material = flashMaterial;
        }

        yield return new WaitForSeconds(explosionDelay);

        // Nổ sát thương
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, targetLayer);
        foreach (var hit in hits) {
            BossHealthSystem targetHealth = hit.GetComponent<BossHealthSystem>();
            if (targetHealth != null && targetHealth != _health) {
                targetHealth.TakeDamage(explosionDamage);
            }
        }

        // Hiệu ứng nổ và tự hủy
        if (explosionVFX != null) {
            Instantiate(explosionVFX, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
        }

        Destroy(gameObject);
    }

    void OnKilledByPlayer() {
        if (_isExploding) return; // Đang nổ thì thôi

        // Chết chay (không gây sát thương)
        if (explosionVFX != null) {
            Instantiate(explosionVFX, transform.position + new Vector3(0, 1.5f, 0), Quaternion.identity);
        }
        Destroy(gameObject);
    }

    void OnDestroy() {
        if (_health != null) {
            _health.OnDeath -= OnKilledByPlayer;
        }
    }
}
