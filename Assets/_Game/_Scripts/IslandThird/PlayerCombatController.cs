using UnityEngine;

/// <summary>
/// Điều khiển chiến đấu của Human Player trong Đảo 3.
/// Gắn lên Player GameObject (cùng với BossHealthSystem + PlayerCombatAnalyzer).
///
/// Controls:
///   Click Trái  → Bắn xa (Raycast)
///   Click Phải  → Chém cận (OverlapSphere)
///   Space       → Dodge Roll (dash + invincibility)
///   Left Shift  → Block (giảm damage nhận vào)
///   WASD        → Di chuyển (xử lý bởi ThirdPersonController riêng)
/// </summary>
[RequireComponent(typeof(BossHealthSystem))]
[RequireComponent(typeof(PlayerCombatAnalyzer))]
public class PlayerCombatController : MonoBehaviour {
    // ─── Ranged Attack (Click Trái) ─────────────────────────────
    [Header("🔫 Ranged Attack (Click Trái)")]
    public float rangedDamage = 8f;
    public float rangedRange = 25f;
    public float rangedCooldown = 0.4f;
    public LayerMask attackHitMask;
    [Tooltip("Camera chính để Raycast từ tâm màn hình")]
    public Camera mainCamera;

    // ─── Melee Attack (Click Phải) ──────────────────────────────
    [Header("⚔️ Melee Attack (Click Phải)")]
    public float meleeDamage = 15f;
    public float meleeRange = 3f;
    public float meleeCooldown = 1.2f;

    // ─── Dodge Roll (Space) ─────────────────────────────────────
    [Header("💨 Dodge Roll (Space)")]
    public float dodgeDistance = 5f;
    public float dodgeSpeed = 20f;
    public float dodgeCooldown = 1.5f;
    public float dodgeInvincibilityDuration = 0.25f;

    // ─── Block (Left Shift) ─────────────────────────────────────
    [Header("🛡️ Block (Left Shift)")]
    [Range(0f, 0.9f)]
    public float blockDamageReduction = 0.6f;
    [Tooltip("Hệ số giảm tốc di chuyển khi Block (0.5 = chậm 50%)")]
    public float blockSpeedMultiplier = 0.5f;

    // ─── VFX References (Tùy chọn — gán sau) ────────────────────
    [Header("✨ VFX (Tùy chọn)")]
    public Transform rangedOrigin;
    [Tooltip("Particle hoặc Line bắn ra khi Ranged Attack")]
    public GameObject rangedVFX;
    public GameObject rangedExplosionVFX;
    [Tooltip("Particle khi Melee chém")]
    public GameObject meleeVFX;
    [Tooltip("Particle trail khi Dodge")]
    public GameObject dodgeVFX;
    [Tooltip("Shield visual khi Block")]
    public GameObject blockVFX;

    // ─── References ──────────────────────────────────────────────
    [Header("🔗 References")]
    public Transform bossTransform;
    public BossHealthSystem bossHealth;
    public Animator animator;

    // ─── Private ─────────────────────────────────────────────────
    private BossHealthSystem _myHealth;
    private PlayerCombatAnalyzer _analyzer;
    private CharacterController _charController;

    private float _rangedTimer;
    private float _meleeTimer;
    private float _dodgeTimer;

    private bool _isDodging;
    private Vector3 _dodgeDirection;
    private float _dodgeTraveled;

    private bool _isBlocking;

    // Properties cho ThirdPersonController đọc
    /// <summary> True khi đang Block → giảm tốc di chuyển </summary>
    public bool IsBlocking => _isBlocking;
    /// <summary> Hệ số tốc độ hiện tại (1.0 bình thường, 0.5 khi block) </summary>
    public float SpeedMultiplier => _isBlocking ? blockSpeedMultiplier : 1f;
    /// <summary> True khi đang Dodge → ThirdPersonController tạm ngưng xử lý input </summary>
    public bool IsDodging => _isDodging;

    // ════════════════════════════════════════════════════════════════
    void Start() {
        _myHealth = GetComponent<BossHealthSystem>();
        _analyzer = GetComponent<PlayerCombatAnalyzer>();
        _charController = GetComponent<CharacterController>();

        if (mainCamera == null) mainCamera = Camera.main;

        // Khi Player nhận sát thương → rung màn hình mạnh
        if (_myHealth != null) {
            _myHealth.OnDamaged += (dmg, hp, maxHp) => CombatJuice.ShakeHeavy();
        }

        // Auto-find Boss nếu chưa gán
        if (bossTransform == null) {
            var boss = transform.parent != null
                ? transform.parent.GetComponentInChildren<AdaptiveBossAgent>()
                : FindFirstObjectByType<AdaptiveBossAgent>();
            if (boss != null) {
                bossTransform = boss.transform;
                bossHealth = boss.GetComponent<BossHealthSystem>();
            }
        }
    }

    void Update() {
        // Không cho chiến đấu nếu đã chết
        if (_myHealth != null && _myHealth.IsDead) return;

        // Đếm ngược cooldowns
        _rangedTimer -= Time.deltaTime;
        _meleeTimer -= Time.deltaTime;
        _dodgeTimer -= Time.deltaTime;

        // Xử lý Dodge Roll đang chạy
        if (_isDodging) {
            ProcessDodge();
            return; // Khi dodge, bỏ qua input khác
        }

        // ═══ INPUT: Block (giữ Shift hoặc Block button) ═══
        HandleBlock();

        // ═══ INPUT: Dodge (Space hoặc Dodge button) ═══
        if ((MobileInputBridge.DodgeDown || Input.GetKeyDown(KeyCode.Space)) && _dodgeTimer <= 0f) {
            StartDodge();
        }

        // ═══ INPUT: Ranged Attack (Chuột Trái hoặc Ranged button) ═══
        if ((MobileInputBridge.RangedDown || Input.GetMouseButtonDown(0)) && _rangedTimer <= 0f && !_isBlocking) {
            DoRangedAttack();
        }

        // ═══ INPUT: Melee Attack (Chuột Phải hoặc Melee button) ═══
        if ((MobileInputBridge.MeleeDown || Input.GetMouseButtonDown(1)) && _meleeTimer <= 0f && !_isBlocking) {
            DoMeleeAttack();
        }
    }

    // ════════════════════════════════════════════════════════════════
    #region Ranged Attack

    void DoRangedAttack() {
        _rangedTimer = rangedCooldown;

        // Quay người về hướng camera khi tấn công
        if (mainCamera != null) {
            Vector3 camForward = mainCamera.transform.forward;
            camForward.y = 0;
            if (camForward.sqrMagnitude > 0.01f) {
                transform.forward = camForward.normalized;
            }
        }

        // Animation
        if (animator != null) animator.SetTrigger("RangedAttack");

        // Raycast từ tâm camera (giống FPS/TPS shooter)
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        bool didHit = false;
        Vector3 hitPoint = ray.origin + ray.direction * rangedRange;

        // RaycastAll: chạm mọi thứ, sau đó lọc kết quả
        RaycastHit[] hits = Physics.RaycastAll(ray, rangedRange);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? foundHit = null;
        foreach (RaycastHit h in hits) {
            // Bỏ qua chính bản thân Player và mọi child của Player
            if (h.collider.gameObject == gameObject) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            // Bỏ qua layer IgnoreRaycast (layer 2)
            if (h.collider.gameObject.layer == 2) continue;
            
            foundHit = h;
            break;
        }

        if (foundHit.HasValue) {
            hitPoint = foundHit.Value.point;
            
            // Luôn nổ Particle ở điểm chạm (dù là tường, đất hay Boss)
            if (rangedExplosionVFX != null) {
                GameObject explosion = Instantiate(rangedExplosionVFX, hitPoint, Quaternion.identity);
                Destroy(explosion, 1.5f);
            }

            // Chỉ gây sát thương nếu vật nằm trong attackHitMask
            int hitLayer = foundHit.Value.collider.gameObject.layer;
            if (((1 << hitLayer) & attackHitMask) != 0) {
                var hp = foundHit.Value.collider.GetComponent<BossHealthSystem>();
                if (hp != null && hp != _myHealth) {
                    hp.TakeDamage(rangedDamage);
                    didHit = true;
                    CombatJuice.TriggerHitStop();
                    CombatJuice.ShakeMedium();
                }
            } else {
                // Chạm vật khác (tường/đất) — rung nhẹ như recoil
                CombatJuice.ShakeLight();
            }
        }

        // Đăng ký hành vi cho Analyzer
        _analyzer?.RegisterAttack();

        // VFX
        if (rangedVFX != null) {
            rangedVFX.SetActive(true);
            LineRenderer lr = rangedVFX.GetComponent<LineRenderer>();
            if (lr != null) {
                Vector3 originPos = rangedOrigin != null ? rangedOrigin.position : transform.position + Vector3.up * 1.5f;
                lr.SetPosition(0, originPos);
                lr.SetPosition(1, hitPoint);
            }
            Invoke(nameof(HideRangedVFX), 0.15f);
        }

        Debug.Log($"[PlayerCombat] Bắn xa — {(didHit ? "TRÚNG!" : "MISS")}");
    }

    void HideRangedVFX() {
        if (rangedVFX != null) rangedVFX.SetActive(false);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Melee Attack

    void DoMeleeAttack() {
        _meleeTimer = meleeCooldown;

        // Quay người về hướng camera khi tấn công
        if (mainCamera != null) {
            Vector3 camForward = mainCamera.transform.forward;
            camForward.y = 0;
            if (camForward.sqrMagnitude > 0.01f) {
                transform.forward = camForward.normalized;
            }
        }

        // Animation
        if (animator != null) animator.SetTrigger("MeleeAttack");

        // OverlapSphere phía trước Player
        Vector3 attackCenter = transform.position + transform.forward * (meleeRange * 0.5f);
        Collider[] hits = Physics.OverlapSphere(attackCenter, meleeRange, attackHitMask);

        bool didHit = false;
        foreach (var col in hits) {
            if (col.transform == transform) continue;

            var hp = col.GetComponent<BossHealthSystem>();
            if (hp != null && hp != _myHealth) {
                hp.TakeDamage(meleeDamage);
                didHit = true;
                CombatJuice.TriggerHitStop();
                CombatJuice.ShakeMedium();
            }
        }

        // Đăng ký hành vi cho Analyzer
        _analyzer?.RegisterAttack();

        // VFX
        if (meleeVFX != null) {
            meleeVFX.SetActive(true);
            Invoke(nameof(HideMeleeVFX), 0.3f);
        }

        Debug.Log($"[PlayerCombat] Chém cận — {(didHit ? "TRÚNG!" : "MISS")}");
    }

    void HideMeleeVFX() {
        if (meleeVFX != null) meleeVFX.SetActive(false);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Dodge Roll

    void StartDodge() {
        _dodgeTimer = dodgeCooldown;
        _isDodging = true;
        _dodgeTraveled = 0f;

        // Hướng dodge = hướng input (joystick hoặc WASD), nếu không nhấn gì thì dodge ra sau
        Vector2 moveInput = MobileInputBridge.HasMoveInput
            ? MobileInputBridge.MoveInput
            : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        float h = moveInput.x;
        float v = moveInput.y;

        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f) {
            // Chuyển hướng input sang world space (tương đối với camera)
            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            _dodgeDirection = (camForward * v + camRight * h).normalized;
        } else {
            // Mặc định: dodge ra phía sau
            _dodgeDirection = -transform.forward;
        }
        _dodgeDirection.y = 0f;

        // Bật invincibility
        if (_myHealth != null) {
            _myHealth.invincibilityDuration = dodgeInvincibilityDuration;
            _myHealth.TriggerInvincibility();
        }

        // Animation
        if (animator != null) animator.SetTrigger("Dodge");

        // VFX
        if (dodgeVFX != null) {
            dodgeVFX.SetActive(true);
            TrailRenderer tr = dodgeVFX.GetComponent<TrailRenderer>();
            if (tr != null) tr.Clear();
        }

        // Đăng ký dodge cho Analyzer
        _analyzer?.RegisterDodge();

        // Thông báo cho CombatJuice mở rộng FOV
        CombatJuice.NotifyDashStart();

        Debug.Log("[PlayerCombat] Dodge Roll!");
    }

    void ProcessDodge() {
        float step = dodgeSpeed * Time.deltaTime;

        // Di chuyển bằng CharacterController (nếu có) hoặc transform trực tiếp
        if (_charController != null) {
            _charController.Move(_dodgeDirection * step);
        } else {
            transform.position += _dodgeDirection * step;
        }

        _dodgeTraveled += step;

        if (_dodgeTraveled >= dodgeDistance) {
            EndDodge();
        }
    }

    void EndDodge() {
        _isDodging = false;

        // Tắt VFX
        if (dodgeVFX != null) dodgeVFX.SetActive(false);

        // Báo cho CombatJuice thu FOV về bình thường
        CombatJuice.NotifyDashEnd();

        // Reset invincibility duration về mặc định
        if (_myHealth != null) {
            _myHealth.invincibilityDuration = 0.2f;
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Block

    void HandleBlock() {
        if (MobileInputBridge.BlockHeld || Input.GetKey(KeyCode.LeftShift)) {
            if (!_isBlocking) {
                _isBlocking = true;
                if (_myHealth != null) _myHealth.damageReduction = blockDamageReduction;
                if (blockVFX != null) blockVFX.SetActive(true);
                if (animator != null) animator.SetBool("IsBlocking", true);
            }
        } else {
            if (_isBlocking) {
                _isBlocking = false;
                if (_myHealth != null) _myHealth.damageReduction = 0f;
                if (blockVFX != null) blockVFX.SetActive(false);
                if (animator != null) animator.SetBool("IsBlocking", false);
            }
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Gizmos

    void OnDrawGizmosSelected() {
        // Vẽ melee range
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Vector3 meleeCenter = transform.position + transform.forward * (meleeRange * 0.5f);
        Gizmos.DrawWireSphere(meleeCenter, meleeRange);

        // Vẽ ranged range
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, rangedRange);
    }

    #endregion
}
