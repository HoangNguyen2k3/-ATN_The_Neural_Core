using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrator cho chuỗi kết thúc game (scene FinalArena):
///   Cutscene → Fighting → [Win/Lose popup] → StoneAppears → EndingChoice → EndingA/B
/// </summary>
public class FinalBossArenaManager : MonoBehaviour {
    public enum ArenaState { Idle, Cutscene, Fighting, WinPending, StoneAppears, EndingChoice, Done }

    // ─── Player ───────────────────────────────────────────────────
    [Header("Player")]
    public FinalArenaPlayerController playerController;

    // ─── Bosses ───────────────────────────────────────────────────
    [Header("Bosses")]
    public SimpleFinalBoss meleeBoss;
    public SimpleFinalBoss rangedBoss;
    public Transform meleeBossSpawn;
    public Transform rangedBossSpawn;

    // ─── Cutscene ─────────────────────────────────────────────────
    [Header("Cutscene")]
    [Tooltip("Camera phụ nhìn xuống arena, bắt đầu inactive")]
    public Camera cinemaCamera;
    public GameObject subtitlePanel;
    public TextMeshProUGUI subtitleText;
    public float subtitleDuration = 2.5f;
    public float startDelay = 0.5f;

    // ─── Final Stone ──────────────────────────────────────────────
    [Header("Final Stone")]
    public GameObject finalStoneObject;
    public FinalEndingSequence endingSequence;
    public float stoneActivationRadius = 3f;
    public ParticleSystem stoneGlowParticle;

    // ─── Win Popup ────────────────────────────────────────────────
    [Header("Win Popup")]
    [Tooltip("Panel hiện khi cả 2 boss chết")]
    public GameObject winPopup;
    [Tooltip("Nút 'Tiếp tục' trên win popup")]
    public UnityEngine.UI.Button winContinueButton;

    // ─── Lose Popup ───────────────────────────────────────────────
    [Header("Lose Popup")]
    [Tooltip("Panel hiện khi player chết")]
    public GameObject losePopup;
    public UnityEngine.UI.Button loseRetryButton;
    public UnityEngine.UI.Button loseMenuButton;

    // ─── State ────────────────────────────────────────────────────
    public ArenaState State { get; private set; } = ArenaState.Idle;

    private bool _meleeDead;
    private bool _rangedDead;
    private bool _winShown;
    private Camera _mainCam;

    // ══════════════════════════════════════════════════════════════
    void Start() {
        _mainCam = Camera.main;

        // Ẩn tất cả ban đầu
        if (finalStoneObject != null) finalStoneObject.SetActive(false);
        if (cinemaCamera != null) cinemaCamera.gameObject.SetActive(false);
        if (subtitlePanel != null) subtitlePanel.SetActive(false);
        if (winPopup != null) winPopup.SetActive(false);
        if (losePopup != null) losePopup.SetActive(false);

        // Boss bắt đầu inactive
        if (meleeBoss != null) {
            meleeBoss.OnDied += OnMeleeDead;
            meleeBoss.gameObject.SetActive(false);
            Debug.Log($"[Arena] meleeBoss gán OK: '{meleeBoss.name}' (instanceID={meleeBoss.GetInstanceID()})");
        }
        else {
            Debug.LogError("[Arena] meleeBoss chưa được gán trong Inspector!");
        }

        if (rangedBoss != null) {
            rangedBoss.OnDied += OnRangedDead;
            rangedBoss.gameObject.SetActive(false);
            Debug.Log($"[Arena] rangedBoss gán OK: '{rangedBoss.name}' (instanceID={rangedBoss.GetInstanceID()})");
        }
        else {
            Debug.LogError("[Arena] rangedBoss chưa được gán trong Inspector!");
        }

        // Lắng nghe player chết
        if (playerController != null)
            playerController.OnPlayerDead += OnPlayerDead;

        // Gán button callbacks
        if (winContinueButton != null) winContinueButton.onClick.AddListener(OnWinContinue);
        if (loseRetryButton != null) loseRetryButton.onClick.AddListener(RetryScene);
        if (loseMenuButton != null) loseMenuButton.onClick.AddListener(GoToMenu);

        if (meleeBoss != null && rangedBoss != null && meleeBoss == rangedBoss)
            Debug.LogError("[FinalBossArenaManager] meleeBoss và rangedBoss đang trỏ vào CÙNG 1 GameObject! Win sẽ trigger ngay khi boss đó chết.");

        // --- Guard chống 2 FinalBossArenaManager chạy song song ---
        var allManagers = FindObjectsByType<FinalBossArenaManager>(FindObjectsSortMode.None);
        if (allManagers.Length > 1) {
            // Chỉ giữ manager có thiết lập đầy đủ (có meleeBoss), hủy bản sao thừa
            if (meleeBoss == null) {
                Debug.LogWarning($"[FinalBossArenaManager] Phát hiện bản sao trùng lặp trên '{gameObject.name}' (không có boss) — đang tự hủy.");
                Destroy(gameObject);
                return;
            }
        }

        StartCoroutine(DelayedStart());
    }

    void OnDestroy() {
        if (meleeBoss != null) meleeBoss.OnDied -= OnMeleeDead;
        if (rangedBoss != null) rangedBoss.OnDied -= OnRangedDead;
        if (playerController != null) playerController.OnPlayerDead -= OnPlayerDead;
    }

    void Update() {
        // Proximity check đã bỏ — stone hiện ra và phải BỮN LASER để phá hủy nó
    }

    // ─── Startup ─────────────────────────────────────────────────

    IEnumerator DelayedStart() {
        yield return new WaitForSeconds(startDelay);
        State = ArenaState.Cutscene;
        StartCoroutine(CutsceneRoutine());
    }

    // ─── Cutscene ─────────────────────────────────────────────────

    IEnumerator CutsceneRoutine() {
        playerController?.DisableControl();

        if (cinemaCamera != null) {
            cinemaCamera.gameObject.SetActive(true);
            if (_mainCam != null) _mainCam.gameObject.SetActive(false);
        }

        yield return ShowSubtitle("The Neural Core... I can feel it pulsing.");

        SpawnBoss(meleeBoss, meleeBossSpawn);
        SpawnBoss(rangedBoss, rangedBossSpawn);
        CombatJuice.ShakeMedium();
        yield return new WaitForSeconds(0.8f);

        yield return ShowSubtitle("Destroy them. Then finish what you started.");

        if (cinemaCamera != null) cinemaCamera.gameObject.SetActive(false);
        if (_mainCam != null) _mainCam.gameObject.SetActive(true);
        if (subtitlePanel != null) subtitlePanel.SetActive(false);

        playerController?.EnableControl();
        State = ArenaState.Fighting;
    }
    public void ChangeCamcine() {
        if (cinemaCamera != null) {
            cinemaCamera.gameObject.SetActive(true);
            if (_mainCam != null) _mainCam.gameObject.SetActive(false);
        }
    }
    IEnumerator ShowSubtitle(string msg) {
        if (subtitlePanel != null) subtitlePanel.SetActive(true);
        if (subtitleText != null) subtitleText.text = msg;
        yield return new WaitForSeconds(subtitleDuration);
        if (subtitlePanel != null) subtitlePanel.SetActive(false);
        yield return new WaitForSeconds(0.4f);
    }

    void SpawnBoss(SimpleFinalBoss boss, Transform spawnPoint) {
        if (boss == null) return;

        if (spawnPoint != null) {
            // Tắt agent trước khi teleport (NavMeshAgent cấm dời position trực tiếp khi enabled)
            var agent = boss.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            boss.transform.position = spawnPoint.position;
            boss.transform.rotation = spawnPoint.rotation;
            Debug.Log($"[Arena] SpawnBoss '{boss.name}' → vị trí spawn: {spawnPoint.position}");
        }
        else {
            Debug.LogWarning($"[Arena] ⚠ SpawnPoint cho '{boss.name}' chưa gán! Boss spawn tại vị trí hiện tại: {boss.transform.position}");
        }

        boss.gameObject.SetActive(true);
        boss.playerTransform = playerController != null ? playerController.transform : null;
        boss.playerHealth = playerController?.GetComponent<BossHealthSystem>();

        if (boss.playerTransform == null)
            Debug.LogError($"[Arena] ❌ Boss '{boss.name}' không tìm được playerTransform! PlayerController = {playerController}");

        Debug.Log($"[Arena] Boss '{boss.name}' Activate() được gọi");
        // Khởi động AI — chỉ sau khi đã warp xong
        boss.Activate();
    }

    // ─── Boss Deaths ──────────────────────────────────────────────

    void OnMeleeDead() {
        Debug.Log($"[Arena] 🟡 OnMeleeDead() gọi! _meleeDead trước={_meleeDead}, _rangedDead={_rangedDead}, State={State}");
        _meleeDead = true;
        HandleCombatResult();
    }
    void OnRangedDead() {
        Debug.Log($"[Arena] 🟡 OnRangedDead() gọi! _rangedDead trước={_rangedDead}, _meleeDead={_meleeDead}, State={State}");
        _rangedDead = true;
        HandleCombatResult();
    }

    void HandleCombatResult() {
        Debug.Log($"[Arena] HandleCombatResult: _melee={_meleeDead}, _ranged={_rangedDead}, _winShown={_winShown}, State={State}");

        if (_meleeDead && _rangedDead && !_winShown) {
            _winShown = true;
            State = ArenaState.StoneAppears;
            CombatJuice.ShakeHeavy();
            // Bỏ qua Win Popup — vào thẳng phase phá đá
            StartCoroutine(BossClearedRoutine());
            return;
        }

        // Enrage boss còn lại
        var rangedHp = rangedBoss != null ? rangedBoss.GetComponent<BossHealthSystem>() : null;
        if (_meleeDead && rangedHp != null && !rangedHp.IsDead) {
            rangedBoss.speed *= 1.5f;
            rangedBoss.attackDamage *= 1.3f;
            rangedBoss.rangedFireCooldown *= 0.65f;
            rangedBoss.SetEnrage(true);
        }

        var meleeHp = meleeBoss != null ? meleeBoss.GetComponent<BossHealthSystem>() : null;
        if (_rangedDead && meleeHp != null && !meleeHp.IsDead) {
            meleeBoss.speed *= 1.5f;
            meleeBoss.attackDamage *= 1.3f;
            meleeBoss.SetEnrage(true);
        }
    }

    // ─── Boss Cleared → Stone Spawn ──────────────────────────────────

    IEnumerator BossClearedRoutine() {
        // Chờ để camera rung xong
        yield return new UnityEngine.WaitForSeconds(1.5f);

        // Hiện viên đá
        if (finalStoneObject != null) {
            finalStoneObject.SetActive(true);
            Debug.Log("[Arena] Viên đá hiện ra. Bắn vào đá để phá hủy!");

            // Subscribe sự kiện chết của viên đá
            var stoneHp = finalStoneObject.GetComponent<BossHealthSystem>();
            if (stoneHp != null) {
                stoneHp.OnDeath += OnStoneDestroyed;
                Debug.Log($"[Arena] Stone HP = {stoneHp.maxHP}. Bắn vào đá!");
            }
            else {
                Debug.LogError("[Arena] ❌ finalStoneObject không có BossHealthSystem! Thêm BossHealthSystem vào viên đá trong Inspector.");
            }
        }
        else {
            Debug.LogError("[Arena] ❌ finalStoneObject chưa được gán!");
        }

        if (stoneGlowParticle != null) stoneGlowParticle.Play();
        CombatJuice.ShakeMedium();

        // Player tiếp tục được điều khiển để bắn viên đá
        playerController?.EnableControl();
    }

    void OnStoneDestroyed() {
        if (State == ArenaState.Done) return;
        State = ArenaState.Done;
        Debug.Log("[Arena] Viên đá bị phá hủy! Bắt đầu diễn cảnh kết thúc.");

        playerController?.DisableControl();

        if (endingSequence != null) {
            endingSequence.TriggerLiberation();
        }
        else {
            Debug.LogError("[Arena] ❌ endingSequence chưa gán! Không thể chạy diễn cảnh.");
        }
    }

    // ─── Win Popup (giữ lại để tương thích, không dùng trong flow chính) ────────

    [System.Obsolete("Không dùng nữa — flow đã đổi sang BossClearedRoutine")]
    void ShowWinPopup() {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (winPopup != null) winPopup.SetActive(true);
    }

    [System.Obsolete("Không dùng nữa — flow đã đổi sang BossClearedRoutine")]
    public void OnWinContinue() { /* Giữ để không vỡ reference trong Editor */ }

    // ─── Lose Popup ───────────────────────────────────────────────

    void OnPlayerDead() {
        if (State == ArenaState.Done) return;
        State = ArenaState.Done;

        // Dừng boss di chuyển
        if (meleeBoss != null) meleeBoss.enabled = false;
        if (rangedBoss != null) rangedBoss.enabled = false;

        StartCoroutine(ShowLoseAfterDelay(1.2f));
    }

    IEnumerator ShowLoseAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (losePopup != null) losePopup.SetActive(true);
    }

    // ─── Lose Buttons ─────────────────────────────────────────────

    void RetryScene() {
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoToMenu() {
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene("StartScene");
    }
}
