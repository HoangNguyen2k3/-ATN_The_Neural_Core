using System;
using UnityEngine;

/// <summary>
/// Hệ thống HP chung cho cả Boss và Player trong Đảo 3.
/// Gắn lên bất kỳ entity nào cần có thanh máu.
/// </summary>
public class BossHealthSystem : MonoBehaviour {
    [Header("❤️ Thông số HP")]
    public float maxHP = 100f;
    [SerializeField] private float currentHP;

    [Header("🛡️ Phòng thủ")]
    [Tooltip("Giảm sát thương nhận vào (0 = không giảm, 0.5 = giảm 50%)")]
    [Range(0f, 0.9f)] public float damageReduction = 0f;
    [Tooltip("Thời gian bất tử sau khi nhận sát thương (giây)")]
    public float invincibilityDuration = 0.2f;

    [Header("⚡ Phase Transition (Chỉ dùng cho Boss)")]
    [Tooltip("Ngưỡng HP để chuyển sang Phase 2 (0-1). Ví dụ: 0.6 = 60% HP")]
    public float phase2Threshold = 0.6f;
    [Tooltip("Ngưỡng HP để chuyển sang Phase 3 (0-1). Ví dụ: 0.3 = 30% HP")]
    public float phase3Threshold = 0.3f;

    // ─── Events (Các script khác đăng ký lắng nghe) ─────────────
    /// <summary> Phát ra khi nhận sát thương. Param: (damage, currentHP, maxHP) </summary>
    public event Action<float, float, float> OnDamaged;
    /// <summary> Phát ra khi HP = 0 </summary>
    public event Action OnDeath;
    /// <summary> Phát ra khi chuyển Phase. Param: (phaseNumber: 1, 2, hoặc 3) </summary>
    public event Action<int> OnPhaseChanged;

    // ─── Trạng thái Runtime ─────────────────────────────────────
    private int _currentPhase = 1;
    private float _lastDamageTime = -999f;
    private bool _isDead = false;
    public float CurrentHP => currentHP;
    public float HPRatio => currentHP / Mathf.Max(1f, maxHP); // 0.0 → 1.0
    public int CurrentPhase => _currentPhase;
    public bool IsDead => _isDead;
    public bool IsInvincible => (Time.time - _lastDamageTime) < invincibilityDuration;

    void Awake() {
        currentHP = maxHP;
    }

    public void ResetHP() {
        currentHP = maxHP;
        _currentPhase = 1;
        _isDead = false;
        _lastDamageTime = -999f;
        damageReduction = 0f;
    }

    public void TriggerInvincibility() {
        _lastDamageTime = Time.time;
    }

    public float TakeDamage(float rawDamage) {
        if (_isDead) return 0f;
        if (IsInvincible) return 0f;

        // Áp dụng giảm sát thương
        float actualDamage = rawDamage * (1f - damageReduction);
        currentHP -= actualDamage;
        currentHP = Mathf.Max(0f, currentHP);
        _lastDamageTime = Time.time;

        // Phát event
        OnDamaged?.Invoke(actualDamage, currentHP, maxHP);
        CheckPhaseTransition();
        if (currentHP <= 0f && !_isDead) {
            _isDead = true;
            OnDeath?.Invoke();
        }

        return actualDamage;
    }

    public void Heal(float amount) {
        if (_isDead) return;
        currentHP = Mathf.Min(currentHP + amount, maxHP);
    }

    // ════════════════════════════════════════════════════════════════
    void CheckPhaseTransition() {
        float ratio = HPRatio;
        int newPhase = 1;

        if (ratio <= phase3Threshold) {
            newPhase = 3;
        }
        else if (ratio <= phase2Threshold) {
            newPhase = 2;
        }

        if (newPhase != _currentPhase) {
            _currentPhase = newPhase;
            OnPhaseChanged?.Invoke(_currentPhase);
            Debug.Log($"[BossHealthSystem] {gameObject.name} chuyển sang Phase {_currentPhase}! (HP: {HPRatio:P0})");
        }
    }
}
