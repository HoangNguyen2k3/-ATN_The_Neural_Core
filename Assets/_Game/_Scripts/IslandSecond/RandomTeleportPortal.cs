using System.Collections;
using UnityEngine;

/// <summary>
/// RANDOM TELEPORT PORTAL — Cổng dịch chuyển xuất hiện ngẫu nhiên.
/// Nếu player không thoát trong 2 giây → dịch chuyển về điểm bắt đầu.
///
/// SETUP TRONG SCENE:
/// 1. Tạo GameObject "TeleportPortal" với Collider Trigger (Sphere/Cylinder).
/// 2. Gắn script này vào.
/// 3. Kéo spawnPoints (các Transform vị trí portal có thể xuất hiện) vào Inspector.
/// 4. Kéo playerSpawnPoint (điểm bắt đầu của player) vào Inspector.
/// 5. Portal sẽ ẩn lúc đầu, tự spawn ngẫu nhiên theo spawnInterval.
/// </summary>
public class RandomTeleportPortal : MonoBehaviour
{
    [Header("Spawn Config")]
    [Tooltip("Danh sách vị trí ngẫu nhiên portal có thể xuất hiện")]
    public Transform[] spawnPoints;
    [Tooltip("Thời gian giữa mỗi lần portal xuất hiện (giây)")]
    public float spawnInterval = 10f;
    [Tooltip("Thời gian portal tồn tại tối đa trước khi biến mất (giây)")]
    public float portalLifetime = 8f;

    [Header("Teleport Config")]
    [Tooltip("Thời gian người chơi phải thoát ra (giây) trước khi bị dịch chuyển")]
    public float warningDuration = 2f;
    [Tooltip("Transform điểm bắt đầu — player bị dịch chuyển về đây")]
    public Transform playerSpawnPoint;

    [Header("Visual")]
    public ParticleSystem portalVFX;
    [Tooltip("GameObject hiển thị countdown UI (TMP_Text)")]
    public TMPro.TMP_Text countdownText;
    [Tooltip("Renderer vòng xoáy portal — đổi màu khi cảnh báo")]
    public Renderer portalRenderer;
    public Color normalColor  = new Color(0.3f, 0f, 1f);   // Tím
    public Color warningColor = new Color(1f, 0.2f, 0f);   // Đỏ cam

    // ─── Runtime ──────────────────────────────────────────────────
    private bool _playerInside  = false;
    private bool _isWarning     = false;
    private Coroutine _warningCo;
    private Coroutine _lifetimeCo;
    private HumanPlayerController _player;

    // ════════════════════════════════════════════════════════════════
    private void Start()
    {
        gameObject.SetActive(false); // Ẩn lúc đầu
        StartCoroutine(SpawnCycle());
    }

    // ════════════════════════════════════════════════════════════════
    #region Spawn Cycle

    private IEnumerator SpawnCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // Chọn vị trí ngẫu nhiên
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                int idx = Random.Range(0, spawnPoints.Length);
                transform.position = spawnPoints[idx].position;
                transform.rotation = spawnPoints[idx].rotation;
            }

            Appear();

            // Chờ hết lifetime rồi ẩn (nếu chưa bị kích hoạt)
            _lifetimeCo = StartCoroutine(AutoHideAfter(portalLifetime));
        }
    }

    private void Appear()
    {
        gameObject.SetActive(true);
        _playerInside = false;
        _isWarning    = false;

        if (portalRenderer != null)
            portalRenderer.material.color = normalColor;

        if (portalVFX != null)
            portalVFX.Play();

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        Debug.Log("[TeleportPortal] Portal xuất hiện!");
    }

    private IEnumerator AutoHideAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_playerInside)
            Hide();
    }

    private void Hide()
    {
        _playerInside = false;
        _isWarning    = false;

        if (_warningCo != null)
        {
            StopCoroutine(_warningCo);
            _warningCo = null;
        }

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        if (portalVFX != null)
            portalVFX.Stop();

        gameObject.SetActive(false);
        Debug.Log("[TeleportPortal] Portal biến mất.");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Trigger

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<HumanPlayerController>();
        if (player == null) return;

        _player      = player;
        _playerInside = true;

        // Hủy auto-hide vì player đang ở trong
        if (_lifetimeCo != null)
        {
            StopCoroutine(_lifetimeCo);
            _lifetimeCo = null;
        }

        // Bắt đầu đếm ngược
        _warningCo = StartCoroutine(TeleportWarning());
        Debug.Log("[TeleportPortal] Player vào cổng — bắt đầu đếm ngược!");
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<HumanPlayerController>();
        if (player == null || player != _player) return;

        _playerInside = false;
        _isWarning    = false;

        // Hủy đếm ngược
        if (_warningCo != null)
        {
            StopCoroutine(_warningCo);
            _warningCo = null;
        }

        // Reset màu về bình thường
        if (portalRenderer != null)
            portalRenderer.material.color = normalColor;

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        // Tiếp tục tính lifetime còn lại rồi ẩn
        _lifetimeCo = StartCoroutine(AutoHideAfter(3f));
        Debug.Log("[TeleportPortal] Player thoát cổng kịp thời!");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Teleport Warning & Execute

    private IEnumerator TeleportWarning()
    {
        _isWarning = true;

        if (portalRenderer != null)
            portalRenderer.material.color = warningColor;

        if (countdownText != null)
            countdownText.gameObject.SetActive(true);

        float remaining = warningDuration;
        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.SetText("{0:F1}", remaining);

            remaining -= Time.deltaTime;
            yield return null;

            // Player đã thoát → coroutine bị cancel từ OnTriggerExit
        }

        // Hết giờ → teleport
        if (_playerInside && _player != null)
        {
            ExecuteTeleport();
        }
    }

    private void ExecuteTeleport()
    {
        if (_player == null || playerSpawnPoint == null) return;

        Debug.Log("[TeleportPortal] Player bị dịch chuyển về điểm bắt đầu!");

        // Tắt CharacterController trước khi dịch chuyển (bắt buộc với CC)
        var cc = _player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        _player.transform.position = playerSpawnPoint.position;
        _player.transform.rotation = playerSpawnPoint.rotation;

        if (cc != null) cc.enabled = true;

        Hide();
    }

    #endregion
}
