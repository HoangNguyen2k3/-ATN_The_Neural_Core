using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Quản lý trận đấu Boss 1v1 trong Đảo 3 (The Neural Colosseum).
/// Pattern tham chiếu từ IslandGameManager.cs của Đảo 2.
/// </summary>
public class BossArenaManager : MonoBehaviour {
    public static BossArenaManager Instance { get; private set; }

    public enum ArenaState { Intro, Fighting, BossDefeated, PlayerDead }

    // ─── Settings ───────────────────────────────────────────────
    [Header("⚙️ Settings")]
    [Tooltip("Thời gian giới hạn trận đấu (giây). 0 = không giới hạn")]
    public float matchTimeLimit = 300f; // 5 phút
    [Tooltip("Bật khi đang train AI. Tắt khi chơi thật.")]
    public bool isTrainingMode = true;

    [Header("🔗 Scene Settings")]
    public string mainMenuSceneName = "MainMenu";

    // ─── References ─────────────────────────────────────────────
    [Header("🔗 References")]
    public BossHealthSystem bossHealth;
    public BossHealthSystem playerHealth;
    public AdaptiveBossAgent bossAgent;
    public PlayerCombatAnalyzer playerAnalyzer;
    public Transform playerSpawnPoint;
    public Transform bossSpawnPoint;
    public DummyPlayerBot dummyPlayerBot;

    // ─── UI ─────────────────────────────────────────────────────
    [Header("🖥️ UI — Panels")]
    public GameObject hudPanel;
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("🖥️ UI — HUD")]
    public TextMeshProUGUI timerText;
    public Image bossHPBar;         // Thanh máu Boss (dùng Image.fillAmount)
    public Image playerHPBar;       // Thanh máu Player
    public TextMeshProUGUI phaseText; // Hiển thị "Phase 1" / "Phase 2" / "Phase 3"

    [Header("🖥️ UI — Buttons")]
    public Button winRetryButton;
    public Button winMenuButton;
    public Button loseRetryButton;
    public Button loseMenuButton;

    // ─── Runtime ────────────────────────────────────────────────
    private ArenaState _state = ArenaState.Intro;
    private float _matchTimer;

    // Lưu vị trí ban đầu để reset mỗi Episode
    private Vector3 _defaultBossPos;
    private Quaternion _defaultBossRot;
    private Vector3 _defaultPlayerPos;
    private Quaternion _defaultPlayerRot;

    public bool IsFighting => _state == ArenaState.Fighting;

    // ════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    void Awake() {
        // Khi Training: cho phép nhiều ArenaManager tồn tại song song
        // Khi Gameplay: chỉ giữ 1 Instance (Singleton)
        if (!isTrainingMode) {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
        }
        Instance = this;

        // Khởi tạo Academy ML-Agents
        var forceInit = Unity.MLAgents.Academy.Instance;
    }

    void Start() {
        _matchTimer = matchTimeLimit;
        _state = ArenaState.Intro;

        // Đăng ký events HP
        if (bossHealth != null) {
            bossHealth.OnDeath += OnBossDefeated;
            bossHealth.OnDamaged += OnBossDamaged;
            bossHealth.OnPhaseChanged += OnBossPhaseChanged;
        }
        if (playerHealth != null) {
            playerHealth.OnDeath += OnPlayerDead;
            playerHealth.OnDamaged += OnPlayerDamaged;
        }

        // Gắn button events
        winRetryButton?.onClick.AddListener(RestartLevel);
        winMenuButton?.onClick.AddListener(GoToMainMenu);
        loseRetryButton?.onClick.AddListener(RestartLevel);
        loseMenuButton?.onClick.AddListener(GoToMainMenu);

        // Ẩn panels
        if (hudPanel != null) {
            hudPanel?.SetActive(false);
            winPanel?.SetActive(false);
            losePanel?.SetActive(false);
        }
        Time.timeScale = 1f;

        // Auto-find DummyPlayerBot locally inside the same Arena
        if (dummyPlayerBot == null) dummyPlayerBot = transform.parent.GetComponentInChildren<DummyPlayerBot>();

        // Lưu vị trí ban đầu
        if (bossAgent != null) {
            _defaultBossPos = bossAgent.transform.position;
            _defaultBossRot = bossAgent.transform.rotation;
        }
        if (dummyPlayerBot != null) {
            _defaultPlayerPos = dummyPlayerBot.transform.position;
            _defaultPlayerRot = dummyPlayerBot.transform.rotation;
        }

        // Auto-start khi Training
        if (isTrainingMode) {
            OnIntroFinished();
        }
    }

    void Update() {
        if (_state != ArenaState.Fighting) return;

        // Đếm ngược
        if (matchTimeLimit > 0f) {
            _matchTimer -= Time.deltaTime;
            UpdateTimerDisplay();

            // Hết giờ → Player thua (Boss vẫn sống = Player thua)
            if (_matchTimer <= 0f) {
                OnPlayerDead();
            }
        }

        // Cập nhật thanh máu UI
        UpdateHPBars();
    }

    void OnDestroy() {
        // Hủy đăng ký events
        if (bossHealth != null) {
            bossHealth.OnDeath -= OnBossDefeated;
            bossHealth.OnDamaged -= OnBossDamaged;
            bossHealth.OnPhaseChanged -= OnBossPhaseChanged;
        }
        if (playerHealth != null) {
            playerHealth.OnDeath -= OnPlayerDead;
            playerHealth.OnDamaged -= OnPlayerDamaged;
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Game State Transitions

    /// <summary> Gọi khi Intro cutscene kết thúc. Bắt đầu trận đấu. </summary>
    public void OnIntroFinished() {
        if (_state != ArenaState.Intro) return;

        _state = ArenaState.Fighting;
        _matchTimer = matchTimeLimit;

        // Reset HP
        bossHealth?.ResetHP();
        playerHealth?.ResetHP();

        // Hiện HUD
        hudPanel?.SetActive(true);
        Cursor.lockState = CursorLockMode.Locked;

        Debug.Log("[BossArenaManager] Trận đấu bắt đầu!");
    }

    /// <summary> Boss bị hạ → Người chơi chiến thắng </summary>
    void OnBossDefeated() {
        if (_state != ArenaState.Fighting) return;
        _state = ArenaState.BossDefeated;

        Debug.Log("[BossArenaManager] Boss bị hạ — Chiến thắng!");

        // Thưởng cho Boss Agent (AI học rằng thua = xấu)
        if (bossAgent != null) {
            bossAgent.AddReward(-5f);
            bossAgent.EndEpisode();
        }

        if (!isTrainingMode) {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            hudPanel?.SetActive(false);
            winPanel?.SetActive(true);
        }
    }

    /// <summary> Player chết hoặc hết giờ → Game Over </summary>
    void OnPlayerDead() {
        if (_state != ArenaState.Fighting) return;
        _state = ArenaState.PlayerDead;

        Debug.Log("[BossArenaManager] Player bị hạ — Game Over!");

        // Thưởng cho Boss Agent (AI học rằng thắng = tốt)
        if (bossAgent != null) {
            bossAgent.AddReward(5f);
            bossAgent.EndEpisode();
        }

        if (!isTrainingMode) {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            hudPanel?.SetActive(false);
            losePanel?.SetActive(true);
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Events Callbacks

    void OnBossDamaged(float damage, float currentHP, float maxHP) {
        // Boss AI nhận phạt khi bị dính đòn
        if (bossAgent != null) {
            bossAgent.AddReward(-0.5f);
        }
    }

    void OnPlayerDamaged(float damage, float currentHP, float maxHP) {
        // Boss AI nhận thưởng khi gây sát thương
        if (bossAgent != null) {
            bossAgent.AddReward(1.0f);
        }
    }

    void OnBossPhaseChanged(int newPhase) {
        if (phaseText != null) {
            phaseText.text = $"PHASE {newPhase}";
        }
        Debug.Log($"[BossArenaManager] Boss chuyển Phase {newPhase}!");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region UI Helpers

    void UpdateTimerDisplay() {
        if (timerText == null) return;
        float safeTime = Mathf.Max(0f, _matchTimer);
        int minutes = Mathf.FloorToInt(safeTime / 60f);
        int seconds = Mathf.FloorToInt(safeTime % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";

        timerText.color = safeTime < 30f
            ? Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.time * 3f, 1f))
            : Color.white;
    }

    void UpdateHPBars() {
        if (bossHPBar != null && bossHealth != null) {
            bossHPBar.fillAmount = bossHealth.HPRatio;
        }
        if (playerHPBar != null && playerHealth != null) {
            playerHPBar.fillAmount = playerHealth.HPRatio;
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Training Support

    /// <summary>
    /// Reset toàn bộ trận đấu (dùng cho Training Episode).
    /// </summary>
    public void ResetArena() {
        _state = ArenaState.Fighting;
        _matchTimer = matchTimeLimit;

        bossHealth?.ResetHP();
        playerHealth?.ResetHP();

        // Reset vị trí Boss
        if (bossAgent != null) {
            Vector3 bossPos = bossSpawnPoint != null ? bossSpawnPoint.position : _defaultBossPos;
            Quaternion bossRot = bossSpawnPoint != null ? bossSpawnPoint.rotation : _defaultBossRot;
            bossAgent.transform.position = bossPos;
            bossAgent.transform.rotation = bossRot;
        }

        // Reset vị trí + phong cách DummyPlayer
        if (dummyPlayerBot != null) {
            Vector3 playerPos = playerSpawnPoint != null ? playerSpawnPoint.position : _defaultPlayerPos;
            dummyPlayerBot.ResetBot(playerPos);
            dummyPlayerBot.transform.rotation = playerSpawnPoint != null ? playerSpawnPoint.rotation : _defaultPlayerRot;
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
