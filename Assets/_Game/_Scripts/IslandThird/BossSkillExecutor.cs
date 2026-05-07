using System.Collections;
using UnityEngine;

/// <summary>
/// Thực thi các kỹ năng của Boss.
/// Nhận lệnh từ AdaptiveBossAgent.OnActionReceived() → chạy animation + hitbox + cooldown.
/// </summary>
public class BossSkillExecutor : MonoBehaviour {
    // ─── Skill IDs (khớp với Discrete Action của ML-Agent) ──────
    public const int SKILL_IDLE = 0;
    public const int SKILL_LASER = 1;
    public const int SKILL_DASH = 2;
    public const int SKILL_AOE_SLAM = 3;
    public const int SKILL_SHIELD = 4;
    public const int SKILL_MELEE = 5;

    // ─── Cấu hình Skill ────────────────────────────────────────
    [Header("🔫 Laser (Skill 1)")]
    public float laserDamage = 8f;
    public float laserRange = 20f;
    public float laserCooldown = 1f;
    public Transform laserOrigin;       // Vị trí bắn ra (mũi súng Boss)
    public LayerMask laserHitMask;      // Layer để Raycast bắn trúng
    public GameObject laserVFX;         // Hiệu ứng tia Laser
    public GameObject laserExplosionVFX;// Hiệu ứng nổ khi tia Laser chạm mục tiêu

    [Header("💨 Dash (Skill 2)")]
    public float dashDistance = 8f;
    public float dashSpeed = 30f;
    public float dashCooldown = 3f;
    public GameObject dashVFX;          // Hiệu ứng lướt

    [Header("💥 AoE Slam (Skill 3)")]
    public float aoeDamage = 20f;
    public float aoeRadius = 5f;
    public float aoeCooldown = 5f;
    public GameObject aoeVFX;           // Hiệu ứng đập đất nổ tung

    [Header("🛡️ Shield (Skill 4)")]
    public float shieldDuration = 2f;
    public float shieldCooldown = 6f;
    public GameObject shieldVFX;        // Visual hiệu ứng khiên (bật/tắt)

    [Header("⚔️ Melee (Skill 5)")]
    public float meleeDamage = 15f;
    public float meleeRange = 3f;
    public float meleeCooldown = 2f;
    public GameObject meleeVFX;         // Hiệu ứng chém/đấm

    [Header("🔗 References")]
    public Animator animator;
    public BossHealthSystem myHealth;   // HP của chính Boss (để Shield tăng damageReduction)
    public Transform target;            // Player transform
    // ─── Cooldown Trackers ──────────────────────────────────────
    private float[] _cooldownTimers = new float[6]; // Index = Skill ID
    private bool _isDashing = false;
    private bool _isShielding = false;
    private bool _isCasting = false;    // Đang cast skill (không thể cast skill khác)

    // ─── Properties ─────────────────────────────────────────────
    /// <summary> Skill đang sẵn sàng chưa? </summary>
    public bool IsSkillReady(int skillId) {
        if (skillId < 0 || skillId >= _cooldownTimers.Length) return false;
        if (skillId == SKILL_IDLE) return true;
        return _cooldownTimers[skillId] <= 0f && !_isCasting;
    }

    /// <summary> Trả về tỉ lệ cooldown trung bình (0 = tất cả sẵn sàng, 1 = tất cả đang cooldown) </summary>
    public float AverageCooldownRatio {
        get {
            float sum = 0f;
            float[] maxCooldowns = { 0f, laserCooldown, dashCooldown, aoeCooldown, shieldCooldown, meleeCooldown };
            for (int i = 1; i < _cooldownTimers.Length; i++) {
                sum += Mathf.Clamp01(_cooldownTimers[i] / Mathf.Max(0.1f, maxCooldowns[i]));
            }
            return sum / 5f;
        }
    }

    /// <summary> Trả về true nếu hit trúng. Boss Agent sẽ dùng để thưởng/phạt. </summary>
    public bool LastSkillHit { get; private set; }

    // ════════════════════════════════════════════════════════════════
    void Update() {
        // Giảm cooldown mỗi frame
        for (int i = 0; i < _cooldownTimers.Length; i++) {
            if (_cooldownTimers[i] > 0f) {
                _cooldownTimers[i] -= Time.deltaTime;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    /// <summary>
    /// Boss Agent gọi hàm này mỗi Decision Step để thực thi kỹ năng.
    /// </summary>
    /// <param name="skillId">ID skill (0-5)</param>
    /// <returns>true nếu skill được thực thi thành công</returns>
    public bool ExecuteSkill(int skillId) {
        if (!IsSkillReady(skillId)) {
            LastSkillHit = false;
            return false;
        }

        switch (skillId) {
            case SKILL_IDLE:
                LastSkillHit = false;
                return true;

            case SKILL_LASER:
                return DoLaser();

            case SKILL_DASH:
                return DoDash();

            case SKILL_AOE_SLAM:
                return DoAoeSlam();

            case SKILL_SHIELD:
                return DoShield();

            case SKILL_MELEE:
                return DoMelee();

            default:
                return false;
        }
    }

    /// <summary> Reset tất cả cooldown (dùng khi Training reset Episode) </summary>
    public void ResetAllCooldowns() {
        for (int i = 0; i < _cooldownTimers.Length; i++) {
            _cooldownTimers[i] = 0f;
        }
        _isDashing = false;
        _isShielding = false;
        _isCasting = false;
        if (shieldVFX != null) shieldVFX.SetActive(false);
        if (myHealth != null) myHealth.damageReduction = 0f;
    }

    // ════════════════════════════════════════════════════════════════
    #region Skill Implementations

    bool DoLaser() {
        _cooldownTimers[SKILL_LASER] = laserCooldown;

        // Animation
        if (animator != null) animator.SetTrigger("Laser");

        // Raycast bắn thẳng về phía trước
        Transform origin = laserOrigin != null ? laserOrigin : transform;
        Vector3 dir = target != null
            ? (target.position + Vector3.up * 1f - origin.position).normalized
            : origin.forward;

        Vector3 hitPoint = origin.position + dir * laserRange;

        // RaycastAll: chạm mọi thứ, bỏ qua bản thân Boss
        RaycastHit[] hits = Physics.RaycastAll(origin.position, dir, laserRange);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? foundHit = null;
        foreach (RaycastHit h in hits) {
            if (h.collider.gameObject == gameObject) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.gameObject.layer == 2) continue;
            foundHit = h;
            break;
        }

        if (foundHit.HasValue) {
            hitPoint = foundHit.Value.point;

            // Luôn nổ Particle ở điểm chạm (đất, tường hay Player)
            if (laserExplosionVFX != null) {
                GameObject explosion = Instantiate(laserExplosionVFX, hitPoint, Quaternion.identity);
                Destroy(explosion, 1.5f);
            }

            // Chỉ gây sát thương nếu trúng đối tượng thuộc laserHitMask
            int hitLayer = foundHit.Value.collider.gameObject.layer;
            if (((1 << hitLayer) & laserHitMask) != 0) {
                var hp = foundHit.Value.collider.GetComponent<BossHealthSystem>();
                if (hp != null) {
                    hp.TakeDamage(laserDamage);
                    LastSkillHit = true;
                }
                else {
                    LastSkillHit = false;
                }
            }
            else {
                LastSkillHit = false;
            }
        }
        else {
            LastSkillHit = false;
        }

        // Bật VFX tạm thời
        if (laserVFX != null) {
            laserVFX.SetActive(true);
            LineRenderer lr = laserVFX.GetComponent<LineRenderer>();
            if (lr != null) {
                lr.SetPosition(0, origin.position);
                lr.SetPosition(1, hitPoint);
            }
            Invoke(nameof(HideLaserVFX), 0.3f);
        }

        return true; // Skill vẫn được xài
    }

    bool DoDash() {
        if (_isDashing) return false;
        _cooldownTimers[SKILL_DASH] = dashCooldown;

        if (animator != null) animator.SetTrigger("Dash");

        // Lướt nhanh về phía Player
        StartCoroutine(DashCoroutine());
        return true;
    }

    IEnumerator DashCoroutine() {
        _isDashing = true;
        _isCasting = true;

        if (dashVFX != null) dashVFX.SetActive(true);

        Vector3 dashDir = target != null
            ? (target.position - transform.position).normalized
            : transform.forward;
        dashDir.y = 0f;

        float traveled = 0f;
        while (traveled < dashDistance) {
            float step = dashSpeed * Time.deltaTime;
            transform.position += dashDir * step;
            traveled += step;
            yield return null;
        }

        _isDashing = false;
        _isCasting = false;
        LastSkillHit = false;

        if (dashVFX != null) dashVFX.SetActive(false);
    }

    bool DoAoeSlam() {
        _cooldownTimers[SKILL_AOE_SLAM] = aoeCooldown;

        if (animator != null) animator.SetTrigger("AoeSlam");

        // Bật VFX
        if (aoeVFX != null) {
            aoeVFX.SetActive(true);
            Invoke(nameof(HideAoeVFX), 0.5f);
        }

        // Kiểm tra mọi Collider trong bán kính
        Collider[] hits = Physics.OverlapSphere(transform.position, aoeRadius);
        bool hitSomething = false;

        foreach (var col in hits) {
            if (col.transform == transform) continue; // Bỏ qua chính mình

            var hp = col.GetComponent<BossHealthSystem>();
            if (hp != null && hp != myHealth) {
                hp.TakeDamage(aoeDamage);
                hitSomething = true;
            }
        }

        LastSkillHit = hitSomething;
        return true;
    }

    bool DoShield() {
        if (_isShielding) return false;
        _cooldownTimers[SKILL_SHIELD] = shieldCooldown;

        if (animator != null) animator.SetTrigger("Shield");

        StartCoroutine(ShieldCoroutine());
        return true;
    }

    IEnumerator ShieldCoroutine() {
        _isShielding = true;

        // Bật VFX + Tăng damage reduction
        if (shieldVFX != null) shieldVFX.SetActive(true);
        if (myHealth != null) myHealth.damageReduction = 0.8f; // Giảm 80% sát thương

        yield return new WaitForSeconds(shieldDuration);

        // Tắt
        if (shieldVFX != null) shieldVFX.SetActive(false);
        if (myHealth != null) myHealth.damageReduction = 0f;
        _isShielding = false;

        LastSkillHit = false;
    }

    bool DoMelee() {
        _cooldownTimers[SKILL_MELEE] = meleeCooldown;

        if (animator != null) animator.SetTrigger("Melee");

        // Bật VFX
        if (meleeVFX != null) {
            meleeVFX.SetActive(true);
            Invoke(nameof(HideMeleeVFX), 0.3f);
        }

        // Kiểm tra Player có trong tầm đánh không
        if (target != null) {
            float dist = Vector3.Distance(transform.position, target.position);
            if (dist <= meleeRange) {
                var hp = target.GetComponent<BossHealthSystem>();
                if (hp != null) {
                    hp.TakeDamage(meleeDamage);
                    LastSkillHit = true;
                    return true;
                }
            }
        }

        LastSkillHit = false;
        return true; // Skill được xài nhưng miss
    }

    #endregion

    // ─── Tắt VFX ──────────────────────────────────────────────────
    void HideLaserVFX() { if (laserVFX != null) laserVFX.SetActive(false); }
    void HideAoeVFX() { if (aoeVFX != null) aoeVFX.SetActive(false); }
    void HideMeleeVFX() { if (meleeVFX != null) meleeVFX.SetActive(false); }

    // ════════════════════════════════════════════════════════════════
    #region Debug

    void OnDrawGizmosSelected() {
        // Vẽ AoE radius
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, aoeRadius);

        // Vẽ Melee range
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        // Vẽ Laser range
        if (laserOrigin != null) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(laserOrigin.position, laserOrigin.forward * laserRange);
        }
    }

    #endregion
}
