using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Quản lý màn hình lựa chọn kết thúc + chuỗi visual của Ending A (Liberation) và Ending B (Corruption).
/// Gắn lên một GameObject trong scene (ví dụ: FinalArena_Root hoặc Canvas_FinalEnding).
/// </summary>
public class FinalEndingSequence : MonoBehaviour {
    // ─── Choice UI ───────────────────────────────────────────────
    [Header("Choice UI")]
    public GameObject choicePanel;
    public Button destroyButton;
    public Button absorbButton;

    // ─── Fade & Text ─────────────────────────────────────────────
    [Header("Fade & Text")]
    [Tooltip("Full-screen Image (default alpha=0)")]
    public Image fadePanel;
    public TextMeshProUGUI endingText;

    // ─── Ending A — Liberation ────────────────────────────────────
    [Header("Ending A — Liberation")]
    [Tooltip("Root Transform của toàn bộ địa hình đảo (sẽ được dịch xuống)")]
    public Transform islandRoot;
    public float islandFallSpeed = 8f;
    public float islandFallDuration = 5f;
    [Tooltip("Particle burst ánh sáng khi đảo rơi (tùy chọn)")]
    public ParticleSystem liberationParticle;

    // ─── Ending B — Corruption ────────────────────────────────────
    [Header("Ending B — Corruption")]
    [Tooltip("Màu overlay đỏ, alpha lerp 0→0.6")]
    public Image corruptionVignette;
    public float vignetteAlpha = 0.6f;

    // ─── Camera ──────────────────────────────────────────────────
    [Header("Camera Zoom (Ending A)")]
    public Camera mainCamera;
    public float zoomTargetFOV = 90f;
    public float zoomSpeed = 10f;
    public float cameraRiseHeight = 50f;

    // ─── Scene ───────────────────────────────────────────────────
    [Header("Scene")]
    public string menuSceneName = "StartScene";

    // ─── Internal ────────────────────────────────────────────────
    private bool _choiceMade;
    public FinalBossArenaManager temp;
    public GameObject stone;
    public GameObject _light;
    public GameObject obj_lighting;

    void Awake() {
        if (choicePanel != null) choicePanel.SetActive(false);
        if (fadePanel != null) SetAlpha(fadePanel, 0f);
        if (endingText != null) endingText.gameObject.SetActive(false);
        if (corruptionVignette != null) SetAlpha(corruptionVignette, 0f);

        if (destroyButton != null) destroyButton.onClick.AddListener(ChooseLiberation);
        if (absorbButton != null) absorbButton.onClick.AddListener(ChooseCorruption);
    }

    // ─── Called by FinalBossArenaManager ─────────────────────────

    public void ShowChoiceUI() {
        if (_choiceMade) return;
        if (choicePanel != null) choicePanel.SetActive(true);
    }

    public void HideChoiceUI() {
        if (choicePanel != null) choicePanel.SetActive(false);
    }

    /// <summary>
    /// Gọi từ FinalBossArenaManager khi viên đá bị phá hủy — bắt đầu diễn cảnh đảo rơi và hiện Win Screen.
    /// </summary>
    public void TriggerLiberation() {
        if (_choiceMade) return;
        _choiceMade = true;
        SaveChoice(1);
        StartCoroutine(PlayEndingA());
    }

    // ─── Button callbacks ─────────────────────────────────────────

    void ChooseLiberation() {
        if (_choiceMade) return;
        _choiceMade = true;
        HideChoiceUI();
        SaveChoice(1);
        StartCoroutine(PlayEndingA());
    }

    void ChooseCorruption() {
        if (_choiceMade) return;
        _choiceMade = true;
        HideChoiceUI();
        SaveChoice(2);
        StartCoroutine(PlayEndingB());
    }

    static void SaveChoice(int choice) {
        if (DataManager.Ins != null && DataManager.Ins.gameSave != null)
            DataManager.Ins.gameSave.endingChoice = choice;
        DataManager.Ins?.SaveData();
    }

    // ─── Ending A: Liberation ──────────────────────────────────────

    IEnumerator PlayEndingA() {
        // Stone explosion juice
        CombatJuice.ShakeHeavy();
        yield return new WaitForSeconds(1);
        if (liberationParticle != null) liberationParticle.Play();

        // 1. Chuyển qua góc cam kia và ẩn player
        if (temp != null) {
            temp.ChangeCamcine();
            if (temp.playerController != null) {
                temp.playerController.gameObject.SetActive(false);
            }
        }
        stone.SetActive(false);
        _light.SetActive(false);
        float elapsed = 0f;
        float startFOV = mainCamera != null ? mainCamera.fieldOfView : 60f;
        Vector3 startCamPos = mainCamera != null ? mainCamera.transform.position : Vector3.zero;

        // 2. Hành tinh rơi xuống
        while (elapsed < islandFallDuration) {
            elapsed += Time.deltaTime;
            float t = elapsed / islandFallDuration;

            if (islandRoot != null)
                islandRoot.position += Vector3.down * islandFallSpeed * Time.deltaTime;

            // Nếu main camera vẫn active thì có thể zoom, nhưng vì đã switch sang cinemaCamera nên đoạn này có thể bị lờ đi.
            // Giữ lại đề phòng trường hợp ChangeCamcine không hoạt động như mong đợi.
            if (mainCamera != null && mainCamera.gameObject.activeInHierarchy) {
                mainCamera.fieldOfView = Mathf.Lerp(startFOV, zoomTargetFOV, t);
                mainCamera.transform.position = Vector3.Lerp(startCamPos,
                    startCamPos + Vector3.up * cameraRiseHeight, t);
            }

            yield return null;
        }
        obj_lighting.SetActive(true);
        // 3. Đợi 10s sau khi đảo rơi
        yield return new WaitForSeconds(2f);
        // 4. Fade to white nhanh hơn và hiện text
        ShowEndingText("The Neural Core is destroyed.\nThe memories are free.", Color.white);
        yield return StartCoroutine(FadeTo(fadePanel, Color.white, 3f));

        yield return new WaitForSeconds(1f);

        SceneManager.LoadScene(menuSceneName);
    }

    // ─── Ending B: Corruption ─────────────────────────────────────

    IEnumerator PlayEndingB() {
        // Stone scale xuống 0
        // (Stone reference không cần; FinalBossArenaManager tắt stone object)

        // Vignette đỏ hiện dần
        yield return StartCoroutine(FadeImageTo(corruptionVignette, vignetteAlpha, 1.5f));

        // Rung nhẹ liên tục 3s
        float shakeEnd = Time.time + 3f;
        while (Time.time < shakeEnd) {
            CombatJuice.ShakeLight();
            yield return new WaitForSeconds(0.18f);
        }

        // Text 1
        ShowEndingText("The power is yours...", Color.white);
        yield return new WaitForSeconds(1.5f);

        // Text 2
        ShowEndingText("The power is yours...\n<size=80%>But the Core lives on... inside you.</size>", Color.white);
        yield return new WaitForSeconds(2f);

        // Fade to black
        yield return StartCoroutine(FadeTo(fadePanel, Color.black, 2f));

        // Dark credits
        if (endingText != null) endingText.gameObject.SetActive(false);
        ShowEndingText("— THE NEURAL CORE —\n\n<size=70%>A mind divided cannot stand.\nBut it can conquer.</size>", Color.red);
        yield return new WaitForSeconds(5f);

        SceneManager.LoadScene(menuSceneName);
    }

    // ─── Helpers ─────────────────────────────────────────────────

    void ShowEndingText(string msg, Color col) {
        if (endingText == null) return;
        endingText.gameObject.SetActive(true);
        endingText.text = msg;
        endingText.color = col;
    }

    IEnumerator FadeTo(Image img, Color targetColor, float duration) {
        if (img == null) { yield return new WaitForSeconds(duration); yield break; }
        Color start = img.color;
        targetColor.a = 1f;
        float elapsed = 0f;
        img.gameObject.SetActive(true);
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            img.color = Color.Lerp(start, targetColor, elapsed / duration);
            yield return null;
        }
        img.color = targetColor;
    }

    IEnumerator FadeImageTo(Image img, float targetAlpha, float duration) {
        if (img == null) { yield return new WaitForSeconds(duration); yield break; }
        img.gameObject.SetActive(true);
        Color c = img.color;
        float start = c.a;
        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(start, targetAlpha, elapsed / duration);
            img.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        img.color = c;
    }

    static void SetAlpha(Image img, float a) {
        if (img == null) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }
}
