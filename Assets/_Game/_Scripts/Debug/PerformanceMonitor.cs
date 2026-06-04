using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Performance Monitor — DontDestroyOnLoad, gắn vào Scene đầu tiên.
///
/// Trên điện thoại: Nhấn vào tiêu đề "PERFORMANCE MONITOR" để ẩn/hiện.
///                  Nhấn nút [RESET] để reset số liệu.
/// Trên Editor:     F3 = ẩn/hiện, F4 = reset (giữ nguyên để tiện debug).
///
/// Tự động reset FPS khi chuyển Scene mới.
///
/// LƯU Ý Agent Processing Time: đo toàn bộ OnActionReceived(),
/// không chỉ riêng model.Execute(). Trong báo cáo gọi là:
/// "Thời gian xử lý hành động Agent".
/// </summary>
public class PerformanceMonitor : MonoBehaviour {
    public static PerformanceMonitor Ins { get; private set; }

    // ─── UI Refs ────────────────────────────────────────────────────
    [Header("UI References")]
    public TextMeshProUGUI monitorText;
    [Tooltip("Nút RESET trên màn hình (tạo Button UI gán vào đây)")]
    public Button resetButton;
    [Tooltip("Nút ẨN/HIỆN — nhấn vào text header hoặc Button riêng")]
    public Button toggleButton;

    [Header("Settings")]
    public bool showOnScreen = true;
    [Tooltip("Khoảng thời gian cập nhật số liệu (giây)")]
    public float updateInterval = 1f;
    [Tooltip("Tự động reset FPS/Agent khi chuyển sang scene mới")]
    public bool autoResetOnSceneLoad = true;

    // ─── FPS ────────────────────────────────────────────────────────
    private float _timer;
    private int   _frameCount;
    private float _currentFps;
    private float _avgFps;
    private float _minFps = 9999f;
    private float _maxFps;
    private float _totalFps;
    private int   _fpsSamples;

    // ─── Time ───────────────────────────────────────────────────────
    private float  _testStartTime;
    private float  _lastSceneLoadTime = -1f;
    private string _lastSceneLoadName = "";

    // ─── Agent Processing Time ──────────────────────────────────────
    private readonly Stopwatch _agentStopwatch = new Stopwatch();
    private double _lastAgentMs;
    private double _avgAgentMs;
    private double _totalAgentMs;
    private int    _agentSamples;
    private bool   _agentMeasuring;

    // ─── Profiler ───────────────────────────────────────────────────
    private ProfilerRecorder _totalReservedMemRecorder;
    private ProfilerRecorder _gcReservedMemRecorder;
    private ProfilerRecorder _systemUsedMemRecorder;

    // ─── Internal ───────────────────────────────────────────────────
    private string _currentSceneName = "";

    // ════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake() {
        if (Ins != null && Ins != this) { Destroy(gameObject); return; }
        Ins = this;
        DontDestroyOnLoad(gameObject);
        _testStartTime   = Time.realtimeSinceStartup;
        _currentSceneName = SceneManager.GetActiveScene().name;
    }

    private void OnEnable() {
        _totalReservedMemRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
        _gcReservedMemRecorder    = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
        _systemUsedMemRecorder    = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Gán sự kiện nút bấm trên màn hình
        if (resetButton  != null) resetButton.onClick.AddListener(OnResetButtonPressed);
        if (toggleButton != null) toggleButton.onClick.AddListener(OnToggleButtonPressed);
    }

    private void OnDisable() {
        _totalReservedMemRecorder.Dispose();
        _gcReservedMemRecorder.Dispose();
        _systemUsedMemRecorder.Dispose();

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (resetButton  != null) resetButton.onClick.RemoveListener(OnResetButtonPressed);
        if (toggleButton != null) toggleButton.onClick.RemoveListener(OnToggleButtonPressed);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        // Tự động reset FPS và Agent khi vào scene mới
        if (autoResetOnSceneLoad && scene.name != _currentSceneName) {
            _currentSceneName = scene.name;
            ResetFpsAndAgent(); // Chỉ reset FPS + Agent, giữ lại Load Scene time
            Debug.Log($"[PerformanceMonitor] Auto-reset khi vào scene: {scene.name}");
        }
    }

    private void Update() {
        // ─── Phím tắt chỉ dùng trên Editor / máy có bàn phím ───
        #if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetKeyDown(KeyCode.F3)) OnToggleButtonPressed();
        if (Input.GetKeyDown(KeyCode.F4)) OnResetButtonPressed();
        #endif

        // ─── Tính FPS ───────────────────────────────────────────
        _timer += Time.unscaledDeltaTime;
        _frameCount++;

        if (_timer >= updateInterval) {
            _currentFps = _frameCount / _timer;

            // Bỏ qua mẫu < 5 FPS (xảy ra khi đang load scene, không phải gameplay thực)
            if (_currentFps > 5f) {
                _minFps = Mathf.Min(_minFps, _currentFps);
                _maxFps = Mathf.Max(_maxFps, _currentFps);
                _totalFps += _currentFps;
                _fpsSamples++;
                _avgFps = _fpsSamples > 0 ? _totalFps / _fpsSamples : 0f;
            }

            _frameCount = 0;
            _timer      = 0f;

            if (showOnScreen) UpdateUI();
        }
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Button Handlers (gọi từ UI Button trên màn hình)

    public void OnToggleButtonPressed() {
        showOnScreen = !showOnScreen;
        if (monitorText != null)
            monitorText.transform.parent.gameObject.SetActive(showOnScreen);
    }

    public void OnResetButtonPressed() {
        ResetStats();
        Debug.Log("[PerformanceMonitor] Reset số liệu.");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region UI

    private void UpdateUI() {
        if (monitorText == null) return;

        float elapsed  = Time.realtimeSinceStartup - _testStartTime;
        float totalMB  = BytesToMB(_totalReservedMemRecorder.LastValue);
        float gcMB     = BytesToMB(_gcReservedMemRecorder.LastValue);
        float systemMB = BytesToMB(_systemUsedMemRecorder.LastValue);

        string fpsColor = _currentFps >= 60f ? "#00FF88"
                        : _currentFps >= 30f ? "#FFD700"
                        : "#FF4444";

        string minDisplay = _fpsSamples > 0 ? $"<color=#FF4444>{_minFps:F0}</color>" : "--";
        string maxDisplay = _fpsSamples > 0 ? $"<color=#00FF88>{_maxFps:F0}</color>" : "--";

        monitorText.text =
            "<b><color=#00BFFF>── PERFORMANCE MONITOR ──</color></b>\n" +
            $"<color=#AAAAAA>Scene:</color> {SceneManager.GetActiveScene().name}\n" +
            $"<color=#AAAAAA>Thời gian đo:</color> {FormatTime(elapsed)}\n\n" +

            $"<b><color=#FFD700>FPS</color></b>\n" +
            $"  Hiện tại : <color={fpsColor}><b>{_currentFps:F0}</b></color>\n" +
            $"  Trung bình: {_avgFps:F0}  |  Min: {minDisplay}  |  Max: {maxDisplay}\n\n" +

            $"<b><color=#FFD700>RAM</color></b>\n" +
            $"  Unity Reserved : {totalMB:F1} MB\n" +
            $"  GC/Managed     : {gcMB:F1} MB\n" +
            $"  System Used    : {systemMB:F1} MB\n\n" +

            $"<b><color=#FFD700>LOAD SCENE</color></b>\n" +
            $"  {(_lastSceneLoadTime < 0 ? "Chưa có dữ liệu" : $"{_lastSceneLoadName}: {_lastSceneLoadTime:F2}s")}\n\n" +

            $"<b><color=#FFD700>AGENT PROCESSING TIME</color></b>\n" +
            $"  <color=#AAAAAA>(Thời gian xử lý hành động Agent)</color>\n" +
            $"  Lần cuối  : {_lastAgentMs:F3} ms\n" +
            $"  Trung bình: {_avgAgentMs:F3} ms  ({_agentSamples} mẫu)\n\n" +

            $"<b><color=#FFD700>THIẾT BỊ</color></b>\n" +
            $"  Battery Temp : {GetBatteryTemp()}\n" +
            $"  APK Size     : {GetApkSize()}\n\n" +

            $"<color=#666666>[Nhấn TOGGLE để ẩn | RESET để xoá số liệu]</color>";
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>Gọi đầu OnActionReceived() để bắt đầu đo.</summary>
    public void BeginAgentMeasure() {
        _agentMeasuring = true;
        _agentStopwatch.Restart();
    }

    /// <summary>Gọi cuối OnActionReceived() để kết thúc đo.</summary>
    public void EndAgentMeasure() {
        if (!_agentMeasuring) return;
        _agentStopwatch.Stop();
        _agentMeasuring = false;

        _lastAgentMs   = _agentStopwatch.Elapsed.TotalMilliseconds;
        _totalAgentMs += _lastAgentMs;
        _agentSamples++;
        _avgAgentMs    = _totalAgentMs / _agentSamples;
    }

    /// <summary>Load scene và đo thời gian tải.</summary>
    public void LoadSceneWithTimer(string sceneName) {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    /// <summary>Reset toàn bộ số liệu.</summary>
    public void ResetStats() {
        ResetFpsAndAgent();
        _lastSceneLoadTime = -1f;
        _lastSceneLoadName = "";
    }

    /// <summary>Chỉ reset FPS và Agent, giữ lại thời gian Load Scene.</summary>
    public void ResetFpsAndAgent() {
        _timer      = 0f;
        _frameCount = 0;
        _currentFps = 0f;
        _avgFps     = 0f;
        _minFps     = 9999f;
        _maxFps     = 0f;
        _totalFps   = 0f;
        _fpsSamples = 0;

        _lastAgentMs  = 0;
        _avgAgentMs   = 0;
        _totalAgentMs = 0;
        _agentSamples = 0;
        _agentMeasuring = false;

        _testStartTime = Time.realtimeSinceStartup;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Scene Load

    private IEnumerator LoadSceneRoutine(string sceneName) {
        float start = Time.realtimeSinceStartup;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;

        _lastSceneLoadTime = Time.realtimeSinceStartup - start;
        _lastSceneLoadName = sceneName;
        Debug.Log($"[PerformanceMonitor] Load '{sceneName}': {_lastSceneLoadTime:F2}s");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════
    #region Helpers

    private static float BytesToMB(long bytes) => bytes / (1024f * 1024f);

    private static string FormatTime(float seconds) {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    private static string GetApkSize() {
#if UNITY_ANDROID && !UNITY_EDITOR
        try {
            string path = Application.dataPath;
            if (File.Exists(path)) return $"{BytesToMB(new FileInfo(path).Length):F1} MB";
            return "N/A";
        } catch { return "N/A"; }
#else
        return "Editor Only";
#endif
    }

    private static string GetBatteryTemp() {
#if UNITY_ANDROID && !UNITY_EDITOR
        try {
            using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var ac = up.GetStatic<AndroidJavaObject>("currentActivity");
            using var ft = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED");
            using var bi = ac.Call<AndroidJavaObject>("registerReceiver", null, ft);
            int temp = bi.Call<int>("getIntExtra", "temperature", -1);
            return temp > 0 ? $"{temp / 10f:F1} °C" : "N/A";
        } catch { return "N/A"; }
#else
        return "Android Only";
#endif
    }

    #endregion
}
