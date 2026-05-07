using System.Collections;
using UnityEngine;

/// <summary>
/// CombatJuice — Phase 2 (Hit-Stop, Screen Shake, Dynamic FOV) + Phase 3 (Lock-on).
/// Gắn script này lên Main Camera GameObject.
/// Gọi các static methods từ PlayerCombatController để kích hoạt hiệu ứng.
/// </summary>
public class CombatJuice : MonoBehaviour
{
    public static CombatJuice Instance { get; private set; }

    // ─── Hit-Stop ─────────────────────────────────────────────────
    [Header("=== Hit-Stop ===")] 
    [Tooltip("Khựng thời gian bao lâu (giây) khi chém/bắn trúng")]
    public float hitStopDuration = 0.06f;

    // ─── Screen Shake ─────────────────────────────────────────────
    [Header("=== Screen Shake ===")]
    [Tooltip("Biên độ rung nhẹ — khi bắn Ranged")]
    public float shakeLight = 0.06f;
    [Tooltip("Biên độ rung vừa — khi Melee chém trúng")]
    public float shakeMedium = 0.15f;
    [Tooltip("Biên độ rung mạnh — khi Player trúng đòn Boss")]
    public float shakeHeavy = 0.35f;
    [Tooltip("Thời gian rung cơ bản (giây)")]
    public float shakeBaseDuration = 0.15f;

    // ─── Dynamic FOV ──────────────────────────────────────────────
    [Header("=== Dynamic FOV ===")]
    [Tooltip("FOV bình thường — đọc tự động từ Camera khi Start")]
    public float normalFOV = 60f;
    [Tooltip("FOV giãn rộng khi đang Dash (tạo cảm giác tốc độ)")]
    public float dashFOV = 72f;
    [Tooltip("Tốc độ chuyển FOV — cao = chuyển nhanh")]
    public float fovLerpSpeed = 10f;

    // ─── Lock-on ──────────────────────────────────────────────────
    [Header("=== Lock-on Target ===")]
    [Tooltip("Transform của Boss. Tự tìm nếu để trống.")]
    public Transform lockOnTarget;
    [Tooltip("Phím bật/tắt Lock-on")]
    public KeyCode lockOnKey = KeyCode.Tab;
    [Tooltip("Tốc độ Camera xoay khi đang Lock-on")]
    public float lockOnRotSpeed = 8f;
    [Tooltip("Nhìn vào phần thân Boss (offset chiều cao)")]
    public float lockOnHeightOffset = 1f;

    // ─── Private ──────────────────────────────────────────────────
    private Camera _cam;
    private CameraDragRotate _camCtrl;

    private bool _isShaking;
    private bool _isHitStopped;
    private bool _isDashing;

    public bool IsLockedOn { get; private set; }

    // =============================================================
    #region Unity Lifecycle

    void Awake()
    {
        Instance = this;
        _cam     = GetComponent<Camera>();
        _camCtrl = GetComponent<CameraDragRotate>();
    }

    void Start()
    {
        // Đọc FOV gốc từ Camera thay vì Inspector
        if (_cam != null) normalFOV = _cam.fieldOfView;

        // Tự tìm Boss nếu chưa gán
        if (lockOnTarget == null)
        {
            var b = FindFirstObjectByType<AdaptiveBossAgent>();
            if (b != null) lockOnTarget = b.transform;
        }
    }

    void Update()
    {
        ProcessLockOnInput();
        UpdateFOV();

        if (IsLockedOn && lockOnTarget != null)
            UpdateLockOnCamera();
    }

    #endregion

    // =============================================================
    #region PUBLIC API — Gọi từ bên ngoài

    /// <summary>Rung nhẹ — dùng khi bắn Ranged</summary>
    public static void ShakeLight()
    {
        if (Instance != null)
            Instance.StartShake(Instance.shakeLight, Instance.shakeBaseDuration * 0.6f);
    }

    /// <summary>Rung vừa — dùng khi Melee chém trúng Boss</summary>
    public static void ShakeMedium()
    {
        if (Instance != null)
            Instance.StartShake(Instance.shakeMedium, Instance.shakeBaseDuration);
    }

    /// <summary>Rung mạnh — dùng khi Player bị Boss đánh trúng</summary>
    public static void ShakeHeavy()
    {
        if (Instance != null)
            Instance.StartShake(Instance.shakeHeavy, Instance.shakeBaseDuration * 1.5f);
    }

    /// <summary>Kích hoạt Hit-Stop khi tấn công trúng Boss</summary>
    public static void TriggerHitStop()
    {
        if (Instance != null)
            Instance.StartCoroutine(Instance.HitStopRoutine());
    }

    /// <summary>Gọi từ PlayerCombatController khi bắt đầu Dash</summary>
    public static void NotifyDashStart()
    {
        if (Instance != null) Instance._isDashing = true;
    }

    /// <summary>Gọi từ PlayerCombatController khi kết thúc Dash</summary>
    public static void NotifyDashEnd()
    {
        if (Instance != null) Instance._isDashing = false;
    }

    #endregion

    // =============================================================
    #region Hit-Stop

    IEnumerator HitStopRoutine()
    {
        if (_isHitStopped) yield break;
        _isHitStopped = true;
        Time.timeScale = 0.05f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        _isHitStopped = false;
    }

    #endregion

    // =============================================================
    #region Screen Shake

    void StartShake(float amount, float duration)
    {
        if (_isShaking) StopCoroutine(nameof(ShakeRoutine));
        StartCoroutine(ShakeRoutine(amount, duration));
    }

    IEnumerator ShakeRoutine(float amount, float duration)
    {
        _isShaking = true;
        Vector3 localOrigin = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float damping  = Mathf.Lerp(1f, 0f, progress);
            Vector2 rand   = Random.insideUnitCircle;
            transform.localPosition = localOrigin + new Vector3(rand.x, rand.y, 0f) * amount * damping;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localPosition = localOrigin;
        _isShaking = false;
    }

    #endregion

    // =============================================================
    #region Dynamic FOV

    void UpdateFOV()
    {
        if (_cam == null) return;
        float target = _isDashing ? dashFOV : normalFOV;
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, target, Time.deltaTime * fovLerpSpeed);
    }

    #endregion

    // =============================================================
    #region Lock-on

    void ProcessLockOnInput()
    {
        // Tab để bật/tắt
        if (Input.GetKeyDown(lockOnKey))
            SetLockOn(!IsLockedOn);

        // Escape tắt lock-on
        if (Input.GetKeyDown(KeyCode.Escape) && IsLockedOn)
            SetLockOn(false);

        // Tự tắt nếu Boss mất đi
        if (IsLockedOn && (lockOnTarget == null || !lockOnTarget.gameObject.activeInHierarchy))
            SetLockOn(false);
    }

    void SetLockOn(bool active)
    {
        IsLockedOn = active;

        // Tắt CameraDragRotate khi lock-on để tránh xung đột
        if (_camCtrl != null) _camCtrl.enabled = !IsLockedOn;
    }

    void UpdateLockOnCamera()
    {
        Vector3 aimPoint = lockOnTarget.position + Vector3.up * lockOnHeightOffset;
        Vector3 dir      = aimPoint - transform.position;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion goal = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, goal, Time.deltaTime * lockOnRotSpeed);
    }

    #endregion

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!IsLockedOn || lockOnTarget == null) return;
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawWireDisc(
            lockOnTarget.position + Vector3.up * lockOnHeightOffset,
            Vector3.up, 0.8f);
    }
#endif
}
