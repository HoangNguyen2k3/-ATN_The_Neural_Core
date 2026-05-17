using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SettingsManager — Quản lý cài đặt âm thanh, đồ họa, độ nhạy Joystick.
/// Chỉ tồn tại ở scene Main Hub.
/// Gắn script này vào root SettingsPanel GameObject trong scene.
///
/// SETUP TRONG SCENE:
/// 1. Tạo SettingsPanel Canvas/Panel (thiết kế trong scene).
/// 2. Gắn script này vào root SettingsPanel.
/// 3. Kéo các Slider/Button vào Inspector.
/// 4. Nút "⚙" gọi SettingsManager.Instance.OpenSettings().
/// </summary>
public class SettingsManager : MonoBehaviour {
    public static SettingsManager Instance { get; private set; }

    // ─── UI References ────────────────────────────────────────────────
    [Header("Music & SFX Sliders")]
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Joystick Sensitivity Slider")]
    [Tooltip("Range: 0.5 (chậm) đến 3.0 (nhạy)")]
    public Slider sensitivitySlider;
    [Tooltip("Text hiển thị giá trị sensitivity hiện tại, VD: '1.5x'")]
    public TMP_Text sensitivityValueText;

    [Header("Graphics Quality Buttons")]
    [Tooltip("Nút 'Thấp' — index 0")]
    public Button btnLow;
    [Tooltip("Nút 'Trung Bình' — index 1")]
    public Button btnMedium;
    [Tooltip("Nút 'Cao' — index 2")]
    public Button btnHigh;

    [Header("Colors — Quality Button Selected/Deselected")]
    public Color colorSelected = new Color(0.2f, 0.8f, 1f);  // Cyan neon
    public Color colorDeselected = new Color(0.3f, 0.3f, 0.3f);

    [Header("Action Buttons")]
    public Button btnSave;
    public Button btnReset;
    public Button btnClose;

    // ─── PlayerPrefs Keys ─────────────────────────────────────────────
    private const string KEY_MUSIC = "settings_music";
    private const string KEY_SFX = "settings_sfx";
    private const string KEY_SENSITIVITY = "settings_sensitivity";
    private const string KEY_QUALITY = "settings_quality";

    // ─── Default Values ───────────────────────────────────────────────
    private const float DEFAULT_MUSIC = 0.7f;
    private const float DEFAULT_SFX = 1.0f;
    private const float DEFAULT_SENSITIVITY = 1.0f;
    private const int DEFAULT_QUALITY = 1;   // Medium

    // ─── Runtime ──────────────────────────────────────────────────────
    private int _selectedQuality;
    private bool _isDirty = false;   // Có thay đổi chưa lưu không

    // ════════════════════════════════════════════════════════════════════
    #region Unity Lifecycle

    private void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() {
        // Gắn events
        musicSlider?.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider?.onValueChanged.AddListener(OnSFXChanged);
        sensitivitySlider?.onValueChanged.AddListener(OnSensitivityChanged);

        btnLow?.onClick.AddListener(() => SelectQuality(0));
        btnMedium?.onClick.AddListener(() => SelectQuality(1));
        btnHigh?.onClick.AddListener(() => SelectQuality(2));

        btnSave?.onClick.AddListener(SaveAndClose);
        btnReset?.onClick.AddListener(ResetToDefault);
        btnClose?.onClick.AddListener(CloseWithoutSave);

        // Ẩn panel khi bắt đầu
        gameObject.SetActive(false);
    }

    private void OnEnable() {
        // Load và hiển thị giá trị hiện tại mỗi lần mở
        LoadAndDisplay();
        _isDirty = false;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>Mở Settings Panel — gọi từ nút ⚙.</summary>
    public void OpenSettings() {
        gameObject.SetActive(true);
    }

    /// <summary>Lưu và đóng.</summary>
    public void SaveAndClose() {
        SaveSettings();
        ApplySettings();
        gameObject.SetActive(false);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
    }

    /// <summary>Đóng không lưu — giá trị slider sẽ reset về saved khi mở lại.</summary>
    public void CloseWithoutSave() {
        gameObject.SetActive(false);
    }

    /// <summary>Reset tất cả về default.</summary>
    public void ResetToDefault() {
        if (musicSlider != null) musicSlider.value = DEFAULT_MUSIC;
        if (sfxSlider != null) sfxSlider.value = DEFAULT_SFX;
        if (sensitivitySlider != null) sensitivitySlider.value = DEFAULT_SENSITIVITY;
        SelectQuality(DEFAULT_QUALITY);

        SaveSettings();
        ApplySettings();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
    }

    /// <summary>Lấy giá trị sensitivity để CameraDragRotate hoặc JoystickInput sử dụng.</summary>
    public float GetSensitivity() {
        return PlayerPrefs.GetFloat(KEY_SENSITIVITY, DEFAULT_SENSITIVITY);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════
    #region Slider Callbacks

    private void OnMusicChanged(float value) {
        // Preview âm lượng nhạc realtime khi kéo slider
        if (AudioManager.Instance != null)
            AudioManager.Instance.musicSource.volume = value;
        _isDirty = true;
    }

    private void OnSFXChanged(float value) {
        if (AudioManager.Instance != null)
            AudioManager.Instance.sfxSource.volume = value;
        _isDirty = true;
    }

    private void OnSensitivityChanged(float value) {
        // Cập nhật text hiển thị
        if (sensitivityValueText != null)
            sensitivityValueText.SetText("{0:F1}x", value);
        _isDirty = true;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════
    #region Quality Selection

    private void SelectQuality(int index) {
        _selectedQuality = index;
        _isDirty = true;

        // Cập nhật màu nút
        UpdateQualityButtonColors();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
    }

    private void UpdateQualityButtonColors() {
        SetButtonColor(btnLow, _selectedQuality == 0);
        SetButtonColor(btnMedium, _selectedQuality == 1);
        SetButtonColor(btnHigh, _selectedQuality == 2);
    }

    private void SetButtonColor(Button btn, bool selected) {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
            img.color = selected ? colorSelected : colorDeselected;
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════
    #region Load / Save / Apply

    private void LoadAndDisplay() {
        float music = PlayerPrefs.GetFloat(KEY_MUSIC, DEFAULT_MUSIC);
        float sfx = PlayerPrefs.GetFloat(KEY_SFX, DEFAULT_SFX);
        float sensitivity = PlayerPrefs.GetFloat(KEY_SENSITIVITY, DEFAULT_SENSITIVITY);
        int quality = PlayerPrefs.GetInt(KEY_QUALITY, DEFAULT_QUALITY);

        // Cập nhật UI — tắt listener tạm để không kích hoạt callback
        if (musicSlider != null) {
            musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            musicSlider.value = music;
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }
        if (sfxSlider != null) {
            sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
            sfxSlider.value = sfx;
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        }
        if (sensitivitySlider != null) {
            sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            sensitivitySlider.value = sensitivity;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
        if (sensitivityValueText != null)
            sensitivityValueText.SetText("{0:F1}x", sensitivity);

        _selectedQuality = quality;
        UpdateQualityButtonColors();
    }

    private void SaveSettings() {
        PlayerPrefs.SetFloat(KEY_MUSIC, musicSlider != null ? musicSlider.value : DEFAULT_MUSIC);
        PlayerPrefs.SetFloat(KEY_SFX, sfxSlider != null ? sfxSlider.value : DEFAULT_SFX);
        PlayerPrefs.SetFloat(KEY_SENSITIVITY, sensitivitySlider != null ? sensitivitySlider.value : DEFAULT_SENSITIVITY);
        PlayerPrefs.SetInt(KEY_QUALITY, _selectedQuality);
        PlayerPrefs.Save();
    }

    private void ApplySettings() {
        if (AudioManager.Instance != null) {
            AudioManager.Instance.SetMusicVolume(PlayerPrefs.GetFloat(KEY_MUSIC, DEFAULT_MUSIC));
            AudioManager.Instance.SetSFXVolume(PlayerPrefs.GetFloat(KEY_SFX, DEFAULT_SFX));
        }

        // Áp dụng chất lượng đồ họa — chỉ gọi khi Save để tránh giật
        QualitySettings.SetQualityLevel(_selectedQuality, applyExpensiveChanges: true);
    }

    #endregion
}
