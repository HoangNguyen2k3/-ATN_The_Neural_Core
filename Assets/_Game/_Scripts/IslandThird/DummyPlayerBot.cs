using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bot giả lập người chơi để train Boss AI.
/// Có 3 phong cách chiến đấu: Rambo (cận chiến hổ báo), Sniper (bắn xa), Dodger (nhảy nhót).
/// Mỗi Episode sẽ random 1 phong cách, ép Boss AI phải học đối phó đủ loại.
/// 
/// QUAN TRỌNG: Script này có gắn PlayerCombatAnalyzer giả lập
/// để truyền chỉ số thói quen cho Boss đọc, giống y hệt người chơi thật.
/// </summary>
[RequireComponent(typeof(BossHealthSystem))]
public class DummyPlayerBot : MonoBehaviour {
    public enum BotStyle { Rambo, Sniper, Dodger, Random }

    // ─── Cấu hình ───────────────────────────────────────────────
    [Header("⚙️ Bot Style")]
    public BotStyle style = BotStyle.Random;

    [Header("🔗 References")]
    public Transform bossTransform;
    public BossHealthSystem bossHealth;
    public PlayerCombatAnalyzer combatAnalyzer; // Để fake chỉ số cho Boss đọc
    public BossHealthSystem myHealth;

    [Header("📏 Khoảng cách")]
    public float preferredDistanceRambo = 3f;
    public float preferredDistanceSniper = 15f;
    public float preferredDistanceDodger = 8f;

    [Header("⚔️ Chiến đấu")]
    public float attackDamage = 5f;
    public float attackRange = 18f;
    public float attackCooldown = 0.5f;
    public float moveSpeed = 6f;

    // ─── Runtime ────────────────────────────────────────────────
    private BotStyle _activeStyle;
    private float _attackTimer;
    private float _dodgeTimer;
    private float _actionTimer;
    private Vector3 _dodgeDirection;

    // ════════════════════════════════════════════════════════════════
    void Start() {
        if (myHealth == null) myHealth = GetComponent<BossHealthSystem>();
        if (combatAnalyzer == null) combatAnalyzer = GetComponent<PlayerCombatAnalyzer>();

        // Auto-find Boss
        if (bossTransform == null) {
            var boss = FindFirstObjectByType<AdaptiveBossAgent>();
            if (boss != null) {
                bossTransform = boss.transform;
                bossHealth = boss.GetComponent<BossHealthSystem>();
            }
        }

        PickStyle();
    }

    /// <summary> Chọn phong cách chiến đấu cho Episode này </summary>
    public void PickStyle() {
        if (style == BotStyle.Random) {
            int r = Random.Range(0, 3);
            _activeStyle = (BotStyle)r;
        } else {
            _activeStyle = style;
        }

        // Fake PlayerCombatAnalyzer cho Boss đọc đúng thói quen
        if (combatAnalyzer != null) {
            switch (_activeStyle) {
                case BotStyle.Rambo:
                    combatAnalyzer.aggressionScore = Random.Range(0.8f, 1.0f);
                    combatAnalyzer.agilityScore = Random.Range(0.1f, 0.3f);
                    combatAnalyzer.preferredRange = Random.Range(0.1f, 0.25f);
                    break;
                case BotStyle.Sniper:
                    combatAnalyzer.aggressionScore = Random.Range(0.3f, 0.5f);
                    combatAnalyzer.agilityScore = Random.Range(0.1f, 0.2f);
                    combatAnalyzer.preferredRange = Random.Range(0.7f, 0.95f);
                    break;
                case BotStyle.Dodger:
                    combatAnalyzer.aggressionScore = Random.Range(0.2f, 0.4f);
                    combatAnalyzer.agilityScore = Random.Range(0.7f, 0.95f);
                    combatAnalyzer.preferredRange = Random.Range(0.3f, 0.6f);
                    break;
            }
        }

        Debug.Log($"[DummyPlayerBot] Phong cách: {_activeStyle}");
    }

    void Update() {
        if (bossTransform == null || myHealth == null || myHealth.IsDead) return;

        // Cập nhật timer
        _attackTimer -= Time.deltaTime;
        _dodgeTimer -= Time.deltaTime;
        _actionTimer -= Time.deltaTime;

        // Thực thi hành vi theo style
        switch (_activeStyle) {
            case BotStyle.Rambo:  DoRambo();  break;
            case BotStyle.Sniper: DoSniper(); break;
            case BotStyle.Dodger: DoDodger(); break;
        }
    }

    // ════════════════════════════════════════════════════════════════
    #region Bot Behaviors

    /// <summary> RAMBO: Lao thẳng vào mặt Boss, spam tấn công cận chiến </summary>
    void DoRambo() {
        float dist = Vector3.Distance(transform.position, bossTransform.position);
        Vector3 dirToBoss = (bossTransform.position - transform.position).normalized;

        // Luôn tiến về phía Boss
        if (dist > preferredDistanceRambo) {
            MoveTowards(dirToBoss);
        }

        // Tấn công liên tục khi đủ gần
        if (dist < attackRange && _attackTimer <= 0f) {
            Attack();
            _attackTimer = attackCooldown * 0.5f; // Rambo bắn nhanh gấp đôi
        }

        // Cập nhật fake analyzer (real-time thay đổi nhẹ để tự nhiên hơn)
        if (combatAnalyzer != null && _actionTimer <= 0f) {
            combatAnalyzer.aggressionScore = Mathf.Clamp01(combatAnalyzer.aggressionScore + Random.Range(-0.05f, 0.05f));
            _actionTimer = 1f;
        }
    }

    /// <summary> SNIPER: Giữ khoảng cách xa, bắn tỉa chính xác </summary>
    void DoSniper() {
        float dist = Vector3.Distance(transform.position, bossTransform.position);
        Vector3 dirToBoss = (bossTransform.position - transform.position).normalized;
        Vector3 dirFromBoss = -dirToBoss;

        // Giữ khoảng cách
        if (dist < preferredDistanceSniper - 2f) {
            MoveTowards(dirFromBoss); // Lùi ra
        } else if (dist > preferredDistanceSniper + 2f) {
            MoveTowards(dirToBoss);   // Tiến vào
        }

        // Bắn khi nằm trong vùng ưa thích
        if (dist >= preferredDistanceSniper - 3f && dist <= attackRange && _attackTimer <= 0f) {
            Attack();
            _attackTimer = attackCooldown;
        }

        // Fake analyzer
        if (combatAnalyzer != null && _actionTimer <= 0f) {
            combatAnalyzer.preferredRange = Mathf.Clamp01(combatAnalyzer.preferredRange + Random.Range(-0.03f, 0.03f));
            _actionTimer = 1f;
        }
    }

    /// <summary> DODGER: Chạy vòng vòng, hay đổi hướng, tấn công hit-and-run </summary>
    void DoDodger() {
        float dist = Vector3.Distance(transform.position, bossTransform.position);

        // Dodge ngẫu nhiên mỗi 1-2 giây
        if (_dodgeTimer <= 0f) {
            _dodgeDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            _dodgeTimer = Random.Range(0.8f, 2f);

            // Nhớ báo cho analyzer
            combatAnalyzer?.RegisterDodge();
        }

        // Di chuyển theo hướng dodge + giữ khoảng cách vừa phải với Boss
        Vector3 dirToBoss = (bossTransform.position - transform.position).normalized;
        Vector3 moveDir;

        if (dist < preferredDistanceDodger - 2f) {
            moveDir = (_dodgeDirection - dirToBoss).normalized; // Chạy xa hơn + dodge
        } else if (dist > preferredDistanceDodger + 4f) {
            moveDir = (_dodgeDirection + dirToBoss).normalized; // Tiến lại + dodge
        } else {
            moveDir = _dodgeDirection; // Chạy ngang
        }

        MoveTowards(moveDir);

        // Hit-and-run: bắn rồi chạy
        if (dist < attackRange && _attackTimer <= 0f) {
            Attack();
            _attackTimer = attackCooldown * 1.5f; // Dodger bắn chậm hơn
        }

        // Fake analyzer
        if (combatAnalyzer != null && _actionTimer <= 0f) {
            combatAnalyzer.agilityScore = Mathf.Clamp01(combatAnalyzer.agilityScore + Random.Range(-0.05f, 0.05f));
            _actionTimer = 0.5f;
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Shared Actions

    void MoveTowards(Vector3 direction) {
        direction.y = 0f;
        transform.position += direction.normalized * moveSpeed * Time.deltaTime;

        // Quay mặt về hướng Boss
        if (bossTransform != null) {
            Vector3 lookDir = bossTransform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f) {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    Time.deltaTime * 5f
                );
            }
        }
    }

    void Attack() {
        if (bossHealth != null && !bossHealth.IsDead) {
            float dist = Vector3.Distance(transform.position, bossTransform.position);
            if (dist <= attackRange) {
                bossHealth.TakeDamage(attackDamage);
                combatAnalyzer?.RegisterAttack();
            }
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    /// <summary> Reset Bot cho Episode mới (Boss Agent gọi qua ArenaManager) </summary>
    public void ResetBot(Vector3 spawnPosition) {
        transform.position = spawnPosition;
        myHealth?.ResetHP();
        PickStyle(); // Random phong cách mới mỗi Episode
        _attackTimer = 0f;
        _dodgeTimer = 0f;
        _actionTimer = 0f;
    }
}
