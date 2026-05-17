using System.Collections;
using UnityEngine;

/// <summary>
/// LIGHTNING TRAP ZONE — Vùng sét giật làm chậm người chơi.
///
/// SETUP TRONG SCENE:
/// 1. Tạo GameObject, thêm Collider (Trigger = true) — BoxCollider hoặc SphereCollider.
/// 2. Gắn script này vào GameObject đó.
/// 3. Thêm hiệu ứng VFX (ParticleSystem sét) làm con.
/// 4. Nếu muốn sét giật ngắt quãng: bật cycleActive, chỉnh activeTime/inactiveTime.
/// </summary>
public class LightningTrapZone : MonoBehaviour
{
    [Header("Slow Effect")]
    [Tooltip("Hệ số nhân tốc độ khi bị sét (0.4 = còn 40% tốc độ)")]
    [Range(0.1f, 0.9f)]
    public float slowMultiplier = 0.4f;

    [Header("Cycle On/Off (tuỳ chọn)")]
    [Tooltip("Bật để sét bật/tắt theo chu kỳ")]
    public bool cycleActive = true;
    [Tooltip("Thời gian SÉT BẬT (giây)")]
    public float activeTime = 2.5f;
    [Tooltip("Thời gian SÉT TẮT (giây) — người chơi an toàn")]
    public float inactiveTime = 1.5f;

    [Header("Visual")]
    [Tooltip("ParticleSystem hiệu ứng sét — gắn vào đây")]
    public ParticleSystem lightningVFX;
    [Tooltip("Renderer để nhấp nháy màu cảnh báo khi sắp bật")]
    public Renderer warningRenderer;
    public Color dangerColor  = new Color(0.9f, 0.8f, 0f);   // Vàng
    public Color safeColor    = new Color(0.2f, 0.2f, 0.2f); // Xám
    public Color warningColor = new Color(1f, 0.4f, 0f);     // Cam cảnh báo

    // ─── Runtime ──────────────────────────────────────────────────
    private bool _isActive = true;
    private HumanPlayerController _playerInside;
    private float _originalWalkSpeed;
    private float _originalSprintSpeed;

    // ════════════════════════════════════════════════════════════════
    private void Start()
    {
        if (cycleActive)
            StartCoroutine(CycleRoutine());
        else
            SetTrapState(true);
    }

    // ════════════════════════════════════════════════════════════════
    #region Trigger

    private void OnTriggerEnter(Collider other)
    {
        if (!_isActive) return;
        var player = other.GetComponent<HumanPlayerController>();
        if (player == null) return;

        _playerInside = player;
        ApplySlow(player);
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<HumanPlayerController>();
        if (player == null || player != _playerInside) return;

        RemoveSlow(player);
        _playerInside = null;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Slow Logic

    private void ApplySlow(HumanPlayerController player)
    {
        _originalWalkSpeed   = player.walkSpeed;
        _originalSprintSpeed = player.sprintSpeed;
        player.walkSpeed     = _originalWalkSpeed   * slowMultiplier;
        player.sprintSpeed   = _originalSprintSpeed * slowMultiplier;
        Debug.Log("[LightningTrap] Player bị sét — chậm lại!");
    }

    private void RemoveSlow(HumanPlayerController player)
    {
        player.walkSpeed   = _originalWalkSpeed;
        player.sprintSpeed = _originalSprintSpeed;
        Debug.Log("[LightningTrap] Player thoát vùng sét — bình thường!");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Cycle On/Off

    private IEnumerator CycleRoutine()
    {
        while (true)
        {
            // --- BẬT sét ---
            SetTrapState(true);
            yield return new WaitForSeconds(activeTime);

            // --- TẮT sét + cảnh báo 0.5s trước khi bật lại ---
            SetTrapState(false);
            float warningStart = inactiveTime - 0.5f;
            yield return new WaitForSeconds(warningStart > 0 ? warningStart : 0f);

            // Nhấp nháy cảnh báo
            if (warningRenderer != null)
                StartCoroutine(BlinkWarning(0.5f));
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void SetTrapState(bool active)
    {
        _isActive = active;

        // VFX
        if (lightningVFX != null)
        {
            if (active) lightningVFX.Play();
            else        lightningVFX.Stop();
        }

        // Màu indicator
        if (warningRenderer != null)
        {
            warningRenderer.material.color = active ? dangerColor : safeColor;
        }

        // Nếu tắt trong khi player đang bên trong → remove slow
        if (!active && _playerInside != null)
        {
            RemoveSlow(_playerInside);
        }

        // Nếu bật lại trong khi player vẫn bên trong → re-apply slow
        if (active && _playerInside != null)
        {
            ApplySlow(_playerInside);
        }
    }

    private IEnumerator BlinkWarning(float duration)
    {
        if (warningRenderer == null) yield break;
        float t = 0f;
        bool toggle = false;
        while (t < duration)
        {
            warningRenderer.material.color = toggle ? warningColor : safeColor;
            toggle = !toggle;
            yield return new WaitForSeconds(0.1f);
            t += 0.1f;
        }
    }

    #endregion
}
