using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SPEED BUFF PICKUP — Buff tăng tốc gấp đôi trong 5 giây.
///
/// SETUP TRONG SCENE:
/// 1. Tạo GameObject hình viên đá/orb, thêm Collider Trigger (Sphere).
/// 2. Gắn script này vào.
/// 3. (Tuỳ chọn) Thêm vào BuffIcon Image trên HUD để hiển thị thời gian còn lại.
/// 4. Đặt nhiều bản sao ở các vị trí khác nhau trên map.
/// 
/// RESPAWN: Buff sẽ tự hồi sinh sau respawnDelay giây.
/// </summary>
public class SpeedBuffPickup : MonoBehaviour
{
    [Header("Buff Config")]
    [Tooltip("Thời gian buff tồn tại (giây)")]
    public float buffDuration = 5f;
    [Tooltip("Hệ số nhân tốc độ (2 = gấp đôi)")]
    public float speedMultiplier = 2f;
    [Tooltip("Thời gian chờ để buff hồi sinh sau khi bị nhặt (giây). 0 = không hồi sinh)")]
    public float respawnDelay = 15f;

    [Header("Visual")]
    [Tooltip("Mesh/Sprite hiển thị viên buff — ẩn đi khi bị nhặt")]
    public GameObject visualObject;
    [Tooltip("ParticleSystem hiệu ứng khi nhặt")]
    public ParticleSystem pickupVFX;
    [Tooltip("ParticleSystem hiệu ứng idle (quay quay)")]
    public ParticleSystem idleVFX;

    [Header("HUD (tuỳ chọn)")]
    [Tooltip("Image radial fill trên HUD hiển thị countdown buff")]
    public Image hudBuffIcon;

    [Header("Bob & Rotate Animation")]
    public bool enableBob = true;
    public float bobSpeed    = 2f;
    public float bobHeight   = 0.3f;
    public bool enableRotate = true;
    public float rotateSpeed = 90f;   // Degrees/second

    // ─── Runtime ──────────────────────────────────────────────────
    private bool _isAvailable = true;
    private Vector3 _startPos;

    // Buff đang active trên player (instance fields, không static)
    private HumanPlayerController _buffedPlayer;
    private Coroutine             _activeBuff;
    private static SpeedBuffPickup _activePickup; // chỉ track instance đang active

    // ════════════════════════════════════════════════════════════════
    private void Start()
    {
        _startPos = transform.position;
        SetVisual(true);
    }

    private void Update()
    {
        if (!_isAvailable) return;

        // Hiệu ứng bob lên xuống
        if (enableBob)
        {
            float newY = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        // Xoay
        if (enableRotate)
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
        }
    }

    // ════════════════════════════════════════════════════════════════
    #region Trigger

    private void OnTriggerEnter(Collider other)
    {
        if (!_isAvailable) return;

        var player = other.GetComponent<HumanPlayerController>();
        if (player == null) return;

        PickUp(player);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Pickup & Buff Logic

    private void PickUp(HumanPlayerController player)
    {
        _isAvailable = false;
        SetVisual(false);

        // Nếu đang có buff khác → hủy cũ, apply mới (reset timer)
        if (_activePickup != null && _activePickup != this)
            _activePickup.CancelBuff();

        // Hiệu ứng nhặt
        if (pickupVFX != null)
        {
            pickupVFX.transform.position = transform.position;
            pickupVFX.Play();
        }

        // Apply buff
        _buffedPlayer = player;
        _activePickup = this;
        _activeBuff   = StartCoroutine(BuffRoutine(player));

        // Respawn sau delay
        if (respawnDelay > 0f)
            StartCoroutine(RespawnRoutine());

        Debug.Log($"[SpeedBuff] Player nhặt buff tốc độ x{speedMultiplier} trong {buffDuration}s!");
    }

    private IEnumerator BuffRoutine(HumanPlayerController player)
    {
        // Apply
        float originalWalk   = player.walkSpeed;
        float originalSprint = player.sprintSpeed;
        player.walkSpeed     = originalWalk   * speedMultiplier;
        player.sprintSpeed   = originalSprint * speedMultiplier;

        // Countdown HUD
        float remaining = buffDuration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            if (hudBuffIcon != null)
                hudBuffIcon.fillAmount = remaining / buffDuration;
            yield return null;
        }

        // Remove
        RemoveBuff();

        // Ẩn HUD icon
        if (hudBuffIcon != null)
            hudBuffIcon.fillAmount = 0f;

        _activeBuff   = null;
        _activePickup = null;
        _buffedPlayer = null;
    }

    public void CancelBuff()
    {
        if (_activeBuff != null) StopCoroutine(_activeBuff);
        RemoveBuff();
        if (hudBuffIcon != null) hudBuffIcon.fillAmount = 0f;
        _activeBuff = null;
        if (_activePickup == this) _activePickup = null;
        _buffedPlayer = null;
    }

    private void RemoveBuff()
    {
        if (_buffedPlayer == null) return;
        _buffedPlayer.walkSpeed   /= speedMultiplier;
        _buffedPlayer.sprintSpeed /= speedMultiplier;
        Debug.Log("[SpeedBuff] Buff hết hạn — trở về tốc độ bình thường.");
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        _isAvailable = true;
        SetVisual(true);
        Debug.Log("[SpeedBuff] Buff hồi sinh!");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Visual Helpers

    private void SetVisual(bool visible)
    {
        if (visualObject != null)
            visualObject.SetActive(visible);

        if (idleVFX != null)
        {
            if (visible) idleVFX.Play();
            else         idleVFX.Stop();
        }

        // Tắt/bật collider
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = visible;
    }

    #endregion
}
