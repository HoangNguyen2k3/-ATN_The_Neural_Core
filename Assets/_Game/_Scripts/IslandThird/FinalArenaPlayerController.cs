using UnityEngine;

/// <summary>
/// Player controller dành riêng cho scene FinalArena.
/// Chức năng: di chuyển, nhảy, bắn (raycast tâm màn hình).
/// Gắn lên Player cùng với BossHealthSystem và CharacterController.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(BossHealthSystem))]
public class FinalArenaPlayerController : MonoBehaviour {

    // ─── Movement ─────────────────────────────────────────────────
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    public float turnSmoothTime = 0.08f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundRadius = 0.3f;
    public LayerMask groundMask;

    // ─── Shoot ────────────────────────────────────────────────────
    [Header("Shoot (Raycast tâm màn hình)")]
    public float shootDamage = 15f;
    public float shootRange = 30f;
    public float shootCooldown = 0.35f;
    public LayerMask shootMask;
    public Camera mainCamera;
    [Tooltip("Particle nổ tại điểm bắn trúng (tùy chọn)")]
    public GameObject hitExplosionVFX;

    // ─── UI ───────────────────────────────────────────────────────
    [Header("UI")]
    [Tooltip("Image dấu + ở tâm màn hình")]
    public GameObject crosshairUI;

    // ─── Events ───────────────────────────────────────────────────
    public event System.Action OnPlayerDead;

    // ─── Public state ─────────────────────────────────────────────
    /// <summary> FinalBossArenaManager set false để khoá input (cutscene, popup, v.v.) </summary>
    public bool CanControl { get; set; } = false;

    // ─── Private ──────────────────────────────────────────────────
    private CharacterController _cc;
    private BossHealthSystem _health;
    private Animator _animator;
    private float _shootTimer;
    private Vector3 _velocity;
    private bool _isGrounded;
    private float _turnSmoothVelocity;

    // ══════════════════════════════════════════════════════════════
    void Start() {
        _cc = GetComponent<CharacterController>();
        _health = GetComponent<BossHealthSystem>();
        _animator = GetComponent<Animator>();

        if (mainCamera == null) mainCamera = Camera.main;
        _health.OnDeath += HandleDeath;

        // Ẩn crosshair cho đến khi combat bắt đầu
        if (crosshairUI != null) crosshairUI.SetActive(false);
    }

    void OnDestroy() {
        if (_health != null) _health.OnDeath -= HandleDeath;
    }

    void Update() {
        if (!CanControl) {
            // Gravity vẫn áp dụng dù không điều khiển được
            ApplyGravity();
            return;
        }

        CheckGround();
        HandleMovement();
        HandleJump();
        HandleShoot();
        ApplyGravity();
    }

    // ─── Ground ───────────────────────────────────────────────────

    void CheckGround() {
        _isGrounded = groundCheck != null
            ? Physics.CheckSphere(groundCheck.position, groundRadius, groundMask)
            : _cc.isGrounded;

        if (_animator != null) _animator.SetBool("IsGrounded", _isGrounded);

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    // ─── Movement ─────────────────────────────────────────────────

    void HandleMovement() {
        Vector2 rawInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 moveInput = MobileInputBridge.HasMoveInput ? MobileInputBridge.MoveInput : rawInput;
        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (dir.magnitude >= 0.1f) {
            float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg
                                + mainCamera.transform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle,
                                                 ref _turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            _cc.Move(moveDir.normalized * moveSpeed * Time.deltaTime);

            if (_animator != null) _animator.SetBool("IsMoving", true);
        } else {
            if (_animator != null) _animator.SetBool("IsMoving", false);
        }
    }

    // ─── Jump ─────────────────────────────────────────────────────

    void HandleJump() {
        bool jumped = MobileInputBridge.JumpDown || Input.GetButtonDown("Jump");
        if (jumped && _isGrounded) {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (_animator != null) _animator.SetTrigger("Jump");
        }
    }

    // ─── Shoot ────────────────────────────────────────────────────

    void HandleShoot() {
        _shootTimer -= Time.deltaTime;

        bool shoot = MobileInputBridge.RangedDown || Input.GetMouseButtonDown(0);
        if (!shoot || _shootTimer > 0f) return;

        _shootTimer = shootCooldown;

        // Quay người về hướng camera khi bắn
        Vector3 camFwd = mainCamera.transform.forward;
        camFwd.y = 0f;
        if (camFwd.sqrMagnitude > 0.01f)
            transform.forward = camFwd.normalized;

        // Raycast từ chính xác tâm viewport
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, shootRange);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 endPoint = ray.origin + ray.direction * shootRange;

        foreach (var h in hits) {
            // Bỏ qua bản thân và layer IgnoreRaycast
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.gameObject.layer == 2) continue;

            endPoint = h.point;

            // VFX tại điểm chạm
            if (hitExplosionVFX != null) {
                var vfx = Instantiate(hitExplosionVFX, endPoint, Quaternion.identity);
                Destroy(vfx, 1.5f);
            }

            // Gây damage nếu đúng layer enemy
            if (((1 << h.collider.gameObject.layer) & shootMask) != 0) {
                var hp = h.collider.GetComponent<BossHealthSystem>();
                if (hp != null) {
                    hp.TakeDamage(shootDamage);
                    CombatJuice.TriggerHitStop();
                    CombatJuice.ShakeLight();
                }
            }
            break; // chỉ xử lý vật đầu tiên chạm
        }
    }

    // ─── Gravity ──────────────────────────────────────────────────

    void ApplyGravity() {
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    // ─── Death ────────────────────────────────────────────────────

    void HandleDeath() {
        CanControl = false;
        if (crosshairUI != null) crosshairUI.SetActive(false);
        OnPlayerDead?.Invoke();
    }

    // ─── Public API (gọi từ FinalBossArenaManager) ────────────────

    public void EnableControl() {
        CanControl = true;
        if (crosshairUI != null) crosshairUI.SetActive(true);

        // Khóa và ẩn cursor khi combat
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void DisableControl() {
        CanControl = false;
        if (crosshairUI != null) crosshairUI.SetActive(false);
    }
}
