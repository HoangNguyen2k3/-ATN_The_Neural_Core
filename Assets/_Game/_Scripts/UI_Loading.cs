using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_Loading : MonoBehaviour {
    public enum LoadingType {
        SceneLoading,
        FakeLoading
    }

    [Header("Mode")]
    public LoadingType loadingType;

    [Header("UI")]
    public Image loadingFill;
    public TextMeshProUGUI textProcess;
    public TextMeshProUGUI textLoading;

    [Header("Scene Loading")]
    public string sceneName;
    [Tooltip("Thời gian tối thiểu hiển thị loading screen (tránh flicker khi scene load quá nhanh)")]
    public float minDisplayTime = 0.8f;
    [Tooltip("Thời gian smooth fill từ 95% lên 100%")]
    public float loadingEndTime = 0.4f;

    [Header("Fake Loading (legacy)")]
    public float fakeLoadingTime = 2f;
    public float textAnimSpeed = 0.4f;

    // GameManager đăng ký callback này để tắt canvas khi load xong
    public System.Action OnLoadComplete;

    private AsyncOperation asyncOperation;

    private void Start() {
        if (loadingType == LoadingType.SceneLoading && !string.IsNullOrEmpty(sceneName))
            StartCoroutine(SceneLoadingRoutine());
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Bắt đầu load scene thật với progress bar theo real async progress.
    /// Gọi từ GameManager.GoToScene().
    /// </summary>
    public void ShowRealLoading(string targetScene) {
        sceneName = targetScene;
        gameObject.SetActive(true);
        StartCoroutine(SceneLoadingRoutine());
    }

    /// <summary> Legacy fake loading — giữ lại để backward compatible. </summary>
    public void ShowFakeLoading(float duration, string nameScene) {
        sceneName = nameScene;
        fakeLoadingTime = duration;
        gameObject.SetActive(true);
        StartCoroutine(FakeLoadingRoutine());
    }

    // ── Coroutines ──────────────────────────────────────────────────

    IEnumerator SceneLoadingRoutine() {
        if (loadingFill != null) loadingFill.fillAmount = 0f;
        SetProgressText(0);

        // Bắt đầu load ngay lập tức — không fake wait
        asyncOperation = SceneManager.LoadSceneAsync(sceneName);
        asyncOperation.allowSceneActivation = false;

        float elapsed = 0f;

        // Chờ đến khi load xong (progress >= 0.9) VÀ đã hiện đủ minDisplayTime
        while (asyncOperation.progress < 0.9f || elapsed < minDisplayTime) {
            elapsed += Time.deltaTime;

            float realPct = asyncOperation.progress / 0.9f;       // 0 → 1
            float timePct = minDisplayTime > 0f
                ? elapsed / minDisplayTime
                : 1f;

            // Hiện giá trị nhỏ hơn: không bao giờ chạy nhanh hơn load thật
            float display = Mathf.Min(realPct, timePct) * 0.95f;

            if (loadingFill != null) loadingFill.fillAmount = display;
            SetProgressText(Mathf.RoundToInt(display * 100f));

            yield return null;
        }

        // Smooth fill 95% → 100%
        float t = 0f;
        float startFill = loadingFill != null ? loadingFill.fillAmount : 0.95f;
        while (t < loadingEndTime) {
            t += Time.deltaTime;
            float fill = Mathf.Lerp(startFill, 1f, t / loadingEndTime);
            if (loadingFill != null) loadingFill.fillAmount = fill;
            SetProgressText(Mathf.RoundToInt(fill * 100f));
            yield return null;
        }

        if (loadingFill != null) loadingFill.fillAmount = 1f;
        SetProgressText(100);

        // Kích hoạt scene
        asyncOperation.allowSceneActivation = true;

        // Đợi 1 frame để scene switch xong rồi mới báo hoàn thành
        yield return null;

        OnLoadComplete?.Invoke();
        gameObject.SetActive(false);
    }

    IEnumerator FakeLoadingRoutine() {
        int dotCount = 0;
        float timer = 0;

        asyncOperation = SceneManager.LoadSceneAsync(sceneName);
        asyncOperation.allowSceneActivation = false;

        while (asyncOperation.progress < 0.9f || timer < fakeLoadingTime) {
            timer += textAnimSpeed;
            dotCount = (dotCount + 1) % 4;
            if (textLoading != null)
                textLoading.text = "Loading" + new string('.', dotCount);
            yield return new WaitForSeconds(textAnimSpeed);
        }

        asyncOperation.allowSceneActivation = true;
        yield return new WaitForSeconds(1);

        OnLoadComplete?.Invoke();
        gameObject.SetActive(false);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private void SetProgressText(int percent) {
        if (textProcess != null)
            textProcess.text = $"Loading {percent}%";
    }
}
