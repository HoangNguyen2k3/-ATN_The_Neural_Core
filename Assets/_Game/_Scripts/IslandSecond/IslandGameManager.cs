using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Singleton quản lý toàn bộ game state cho Island 2 Gameplay Scene.
/// Xử lý Countdown, Win/Lose, companion tracking.
/// </summary>
public class IslandGameManager : MonoBehaviour {
    public static IslandGameManager Instance { get; private set; }

    public enum GameState { Playing, Won, Lost }

    // ─── Settings ───────────────────────────────────────────────────
    [Header("Settings")]
    [Tooltip("Thời gian tồn tại để thắng (giây). Mặc định 5 phút = 300s")]
    public float countdownDuration = 300f;

    [Header("Scene Settings")]
    [Tooltip("Tên scene Main Menu để về khi nhấn nút")]
    public string mainMenuSceneName = "MainMenu";

    // ─── References ─────────────────────────────────────────────────
    [Header("References")]
    public MapManager mapManager;

    // ─── UI References (gán trong Inspector hoặc tự tìm theo tên) ──
    [Header("UI — Panels")]
    public GameObject hudPanel;
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("UI — HUD Elements")]
    public TextMeshProUGUI timerText;
    [Tooltip("Container chứa icon trạng thái các companion (3 ô)")]
    public Transform companionStatusContainer;

    [Header("UI — Win Panel")]
    public Button winRetryButton;
    public Button winMenuButton;

    [Header("UI — Lose Panel")]
    public Button loseRetryButton;
    public Button loseMenuButton;

    // ─── Runtime ────────────────────────────────────────────────────
    private GameState _state = GameState.Playing;
    private float _countdown;
    private List<Image> _companionIcons = new List<Image>();
    private int _totalCompanions;
    private int _companionsCaught;

    public bool IsPlaying => _state == GameState.Playing;

    // ════════════════════════════════════════════════════════════════
    #region Unity Lifecycle
    void Awake() {
        // Singleton
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start() {
        _countdown = countdownDuration;
        _state = GameState.Playing;
        _companionsCaught = 0;

        // Bật gameplay mode cho MapManager
        if (mapManager != null) {
            mapManager.isGameplayMode = true;
        }

        // Đếm companion
        var companions = Object.FindObjectsByType<AICompanion>(FindObjectsSortMode.None);
        _totalCompanions = companions.Length;

        // Khởi tạo icon companion (màu xanh = sống, đỏ = bị bắt)
        SetupCompanionIcons();

        // Gắn button events
        winRetryButton?.onClick.AddListener(RestartLevel);
        winMenuButton?.onClick.AddListener(GoToMainMenu);
        loseRetryButton?.onClick.AddListener(RestartLevel);
        loseMenuButton?.onClick.AddListener(GoToMainMenu);

        // Hiện HUD, ẩn Win/Lose
        hudPanel?.SetActive(true);
        winPanel?.SetActive(false);
        losePanel?.SetActive(false);

        // Đảm bảo time không bị freeze từ lần trước
        Time.timeScale = 1f;
    }

    void Update() {
        if (_state != GameState.Playing) return;

        _countdown -= Time.deltaTime;
        UpdateTimerDisplay();

        // Hết 5 phút → player sống sót = thắng
        if (_countdown <= 0f) {
            OnPlayerReachedExit();
        }
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Game Events (gọi từ ExitZone, AdvancedSeekerDrone, MapManager)

    /// <summary> Gọi khi Human Player bị drone bắt → Game Over </summary>
    public void OnHumanPlayerCaught() {
        if (_state != GameState.Playing) return;
        _state = GameState.Lost;

        Debug.Log("[IslandGameManager] Human Player bị bắt — Game Over");
        Time.timeScale = 0f;
        // Ẩn con trỏ chuột
        Cursor.lockState = CursorLockMode.None;
        hudPanel?.SetActive(false);
        losePanel?.SetActive(true);
    }

    /// <summary> Gọi khi AI Companion bị bắt (game vẫn tiếp tục) </summary>
    public void OnCompanionCaught(GameObject companion) {
        _companionsCaught++;
        Debug.Log($"[IslandGameManager] Companion bị bắt ({_companionsCaught}/{_totalCompanions})");
        UpdateCompanionIcons();
    }

    /// <summary> Gọi khi Human Player vào Exit Zone hoặc hết giờ → Chiến thắng </summary>
    public void OnPlayerReachedExit() {
        if (_state != GameState.Playing) return;
        _state = GameState.Won;

        Debug.Log("[IslandGameManager] Thoát thành công — Chiến thắng!");
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        hudPanel?.SetActive(false);
        winPanel?.SetActive(true);
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region UI Helpers

    void UpdateTimerDisplay() {
        if (timerText == null) return;

        float safeTime = Mathf.Max(0f, _countdown);
        int minutes = Mathf.FloorToInt(safeTime / 60f);
        int seconds = Mathf.FloorToInt(safeTime % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";

        // Đổi màu đỏ khi còn dưới 30 giây
        timerText.color = safeTime < 30f
            ? Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.time * 3f, 1f)) // Nhấp nháy
            : Color.white;
    }

    void SetupCompanionIcons() {
        if (companionStatusContainer == null) return;

        // Tìm tất cả Image trong container
        _companionIcons.Clear();
        foreach (Transform child in companionStatusContainer) {
            var img = child.GetComponent<Image>();
            if (img != null) _companionIcons.Add(img);
        }

        // Đặt màu xanh ban đầu (sống)
        foreach (var icon in _companionIcons) {
            icon.color = new Color(0.2f, 0.9f, 0.3f); // Xanh lá
        }
    }

    void UpdateCompanionIcons() {
        // Các icon bị bắt → màu xám
        for (int i = 0; i < _companionIcons.Count; i++) {
            if (i < _companionsCaught) {
                _companionIcons[i].color = new Color(0.4f, 0.4f, 0.4f, 0.5f); // Xám mờ
            }
        }
    }
    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Scene Navigation

    public void RestartLevel() {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu() {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
    #endregion
}
