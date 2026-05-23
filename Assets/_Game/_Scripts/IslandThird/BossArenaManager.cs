using System;
using System.Collections;
using System.Collections.Generic;
using Aircraft;
using Lean.Pool;
using TMPro;
using Unity.MLAgents.Policies;
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
    public string mainMenuSceneName = "Island3_MenuBoard";

    // ─── AI Model Injection (Gameplay) ──────────────────────────
    [Header("🧠 AI Models (Chỉ dùng khi Gameplay)")]
    [Tooltip("Danh sách model ONNX theo mức độ khó. Gán trong Inspector.")]
    public List<DifficultyModel> difficultyModels;

    [Serializable]
    public struct DifficultyModel {
        public GameDifficulty difficulty;
        public Unity.InferenceEngine.ModelAsset model;
    }

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
    [Header("=============Dead Anim============")]
    public GameObject prefab_explosionDeadBoss;

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
        if (dummyPlayerBot == null && isTrainingMode) dummyPlayerBot = transform.parent.GetComponentInChildren<DummyPlayerBot>();

        // Lưu vị trí ban đầu
        if (bossAgent != null) {
            _defaultBossPos = bossAgent.transform.position;
            _defaultBossRot = bossAgent.transform.rotation;
        }
        if (dummyPlayerBot != null) {
            _defaultPlayerPos = dummyPlayerBot.transform.position;
            _defaultPlayerRot = dummyPlayerBot.transform.rotation;
        }

        // ═══ GAMEPLAY MODE: Inject AI Model + tắt DummyBot ═══
        if (!isTrainingMode) {
            // Tắt DummyPlayerBot (gameplay dùng người chơi thật)
            if (dummyPlayerBot != null) {
                dummyPlayerBot.enabled = false;
            }

            // Inject ONNX model theo độ khó đã chọn
            if (GameManager.Instance != null && difficultyModels != null && difficultyModels.Count > 0) {
                var chosenDifficulty = GameManager.Instance.GameDifficultyIsland3;
                var difficultyData = difficultyModels.Find(x => x.difficulty == chosenDifficulty);

                if (difficultyData.model != null && bossAgent != null) {
                    var bp = bossAgent.GetComponent<BehaviorParameters>();
                    if (bp != null) {
                        bp.Model = difficultyData.model;
                    }
                    Debug.Log($"[BossArenaManager] Đã cấy bộ não AI Boss mức: {chosenDifficulty}");
                }
                else {
                    Debug.LogWarning($"[BossArenaManager] Không tìm thấy ONNX Model cho mức {GameManager.Instance.GameDifficultyIsland3}!");
                }
            }

            // Auto-start trận đấu (tạm thời không có cutscene)
            OnIntroFinished();
        }

        // ═══ TRAINING MODE: Auto-start luôn ═══
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
        if (bossHealth != null) {
            bossHealth.OnDeath -= OnBossDefeated;
            bossHealth.OnDamaged -= OnBossDamaged;
            bossHealth.OnPhaseChanged -= OnBossPhaseChanged;
        }
        if (playerHealth != null) {
            playerHealth.OnDeath -= OnPlayerDead;
            playerHealth.OnDamaged -= OnPlayerDamaged;
        }
        if (Instance == this) Instance = null;
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

    void OnBossDefeated() {
        if (_state != ArenaState.Fighting) return;
        _state = ArenaState.BossDefeated;
        Debug.Log("[BossArenaManager] Boss bị hạ — Chiến thắng!");

        // Hiện anim dead
        if (bossAgent != null) {
            Animator bossAnim = bossAgent.GetComponentInChildren<Animator>();
            if (bossAnim != null) {
                StartCoroutine(TripleExplosionSequence((bossAgent.transform.position + new Vector3(0, 3, 0)), 2f));
                bossAnim.SetTrigger("IsDead");
                Debug.LogWarning("[BossArenaManager] Explosion Done!");
            }
            else {
                Debug.LogWarning("[BossArenaManager] Không tìm thấy Animator để chạy anim chết!");
            }

            // Thưởng cho Boss Agent (AI học rằng thua = xấu)
            bossAgent.AddReward(-5f);

            if (isTrainingMode) {
                // Training: Kết thúc episode ngay lập tức để học vòng mới
                bossAgent.EndEpisode();
            }
            else {
                // Gameplay: Tắt AI để Boss ngừng chạy
                bossAgent.enabled = false;

                // Dọn dẹp đệ tử
                BossMinionSpawner spawner = bossAgent.GetComponent<BossMinionSpawner>();
                if (spawner != null) spawner.ClearAllMinions();

                // Dừng hẳn vật lý nhưng KHÔNG bật isKinematic để tránh block Root Motion
                var rb = bossAgent.GetComponent<Rigidbody>();
                if (rb != null) {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        if (!isTrainingMode) {
            // Lưu tiến trình viên đá Island 3 (stoneIndex = 3)
            if (DataManager.Ins != null && DataManager.Ins.isLoaded && GameManager.Instance != null)
                DataManager.Ins.AddStoneProgress(3, GameManager.Instance.DifficultyCountIsland3);

            AudioManager.Instance?.PlayVictory();
            StartCoroutine(ShowWinScreenDelay());
        }
    }
    private IEnumerator TripleExplosionSequence(Vector3 centerPos, float times) {
        for (int i = 0; i < 3; i++) {
            // Tạo độ lệch (offset) ngẫu nhiên để các vụ nổ không đè lên nhau
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-1.0f, 1.0f) * times, // Lệch sang trái/phải
                UnityEngine.Random.Range(-0.5f, 0.5f) * times, // Lệch lên/xuống
                UnityEngine.Random.Range(-1.0f, 1.0f) * times  // Lệch trước/sau
            );

            // Spawn vụ nổ
            LeanPool.Spawn(prefab_explosionDeadBoss, centerPos + randomOffset, Quaternion.identity);

            // Chờ 0.3 giây rồi mới nổ phát tiếp theo
            yield return new WaitForSeconds(0.6f);
        }
    }
    private System.Collections.IEnumerator ShowWinScreenDelay() {
        yield return new WaitForSeconds(2.5f); // Đợi 2.5s để xem Boss gục ngã

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        hudPanel?.SetActive(false);
        winPanel?.SetActive(true);
    }

    /// <summary> Player chết hoặc hết giờ → Game Over </summary>
    void OnPlayerDead() {
        if (_state != ArenaState.Fighting) return;
        _state = ArenaState.PlayerDead;

        Debug.Log("[BossArenaManager] Player bị hạ — Game Over!");

        if (bossAgent != null) {
            BossMinionSpawner spawner = bossAgent.GetComponent<BossMinionSpawner>();
            if (spawner != null) spawner.ClearAllMinions();
        }

        // Thưởng cho Boss Agent (AI học rằng thắng = tốt)
        if (bossAgent != null) {
            bossAgent.AddReward(5f);
            bossAgent.EndEpisode();
        }

        if (!isTrainingMode) {
            AudioManager.Instance?.PlayDefeat();
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
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
        if (GameManager.Instance != null)
            GameManager.Instance.GoToScene(SceneManager.GetActiveScene().name);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu() {
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.GoToScene(mainMenuSceneName);
        else
            SceneManager.LoadScene(mainMenuSceneName);
    }

    #endregion
}
