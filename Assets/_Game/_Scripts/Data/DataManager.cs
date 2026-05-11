using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class DataManager : MonoBehaviour {
    public static DataManager Ins { get; private set; }
    public bool isLoaded = false;
    public bool isLoadedDataInternet { get; private set; }
    public GameSave gameSave;
    GameSave gameSave_BackUp;
    private void OnApplicationPause(bool pause) {
        if (!pause && isLoaded && gameSave != null) {
        }
        SaveData();
    }
    private void OnApplicationQuit() { SaveData(); }
    public void Init() {
        if (Ins != null && Ins != this) {
            Destroy(gameObject);
        }
        else {
            Ins = this;
            DontDestroyOnLoad(gameObject);
        }
        LoadData();
    }
    public void LoadData() {
        if (isLoaded) return;

        if (PlayerPrefs.HasKey("GameSave")) {
            string jsonData = PlayerPrefs.GetString("GameSave");
            gameSave = JsonUtility.FromJson<GameSave>(jsonData);
            Debug.Log(gameSave);
            if (gameSave != null)
                Debug.Log(gameSave.isNew);
        }

        // Validate mảng đá (save cũ không có trường này sẽ bị null sau FromJson)
        if (gameSave != null && (gameSave.stonePercent == null || gameSave.stonePercent.Length < 4))
            gameSave.stonePercent = new float[4];

        // Kiểm tra nếu gameSave vẫn null (lần đầu thật sự) hoặc bị ép buộc là mới
        if (gameSave == null || gameSave.isNew) {
            Debug.Log("Game mới hoàn toàn hoặc Init lại dữ liệu");
            gameSave = new GameSave();
            gameSave.isNew = false; // Đánh dấu đã qua bước khởi tạo
        }
        else {
            Debug.Log("Load dữ liệu cũ thành công");
        }

        isLoaded = true;
    }
    public void SaveData() {
        try {
            if (!isLoaded) return;
            if (gameSave == null) {
                if (gameSave_BackUp != null) {
                    gameSave = gameSave_BackUp;
                    Debug.LogError("gameSave bị null, backup thành công");
                }
                else {
                    gameSave = new GameSave();
                    Debug.LogError("gameSave bị null, backup ko thành công. Reset data");
                }
            }
            gameSave_BackUp = gameSave;
            PlayerPrefs.SetString("GameSave", JsonUtility.ToJson(gameSave));
            PlayerPrefs.Save();
        }
        catch (Exception ex) {
            Debug.LogError(ex);
        }
    }
    public void UpdateSoundVolume(float volume) {
        gameSave.soundVolume = volume;
        SaveData();
    }
    public void UpdateMusicVolume(float volume) {
        gameSave.musicVolume = volume;
        SaveData();
    }

    //Cập nhật rung
    public void UpdateVibrateAmount(float amount) {
        gameSave.vibrateAmount = amount;
        SaveData();
    }

    // ── Stone Progress ──────────────────────────────────────────────

    /// <summary>
    /// Cộng dồn % vào viên đá sau mỗi lần thắng.
    /// contribution = 1 / totalLevels → mọi win đều cộng cùng 1 lượng.
    /// Ví dụ: 4 cấp → mỗi win +25%. 1 lần Impossible = 4 lần Easy.
    /// </summary>
    public void AddStoneProgress(int stoneIndex, int totalLevels) {
        if (stoneIndex < 0 || stoneIndex >= 4) return;
        if (totalLevels <= 0) return;

        float contribution = 1f / totalLevels;
        float newVal = Mathf.Min(1f, gameSave.stonePercent[stoneIndex] + contribution);
        gameSave.stonePercent[stoneIndex] = newVal;
        SaveData();
        Debug.Log($"[Stone] #{stoneIndex}: +{contribution * 100f:F1}% → tổng {newVal * 100f:F1}%");
    }

    /// <summary> Trả về % tích lũy (0.0 → 1.0) của viên đá. </summary>
    public float GetStonePercent(int stoneIndex) {
        if (stoneIndex < 0 || stoneIndex >= 4) return 0f;
        return gameSave.stonePercent[stoneIndex];
    }

    /// <summary> True khi cả 4 viên đá đều đạt 100%. </summary>
    public bool AllStonesAt100() {
        if (gameSave?.stonePercent == null) return false;
        for (int i = 0; i < 4; i++)
            if (gameSave.stonePercent[i] < 1f) return false;
        return true;
    }

    // ── Hub Player Position ─────────────────────────────────────────

    /// <summary> Lưu vị trí player trong đảo chính. </summary>
    public void SaveHubPosition(Vector3 pos, float rotY) {
        gameSave.hubPosX = pos.x;
        gameSave.hubPosY = pos.y;
        gameSave.hubPosZ = pos.z;
        gameSave.hubRotY = rotY;
        gameSave.hasHubPosition = true;
        SaveData();
    }

    /// <summary>
    /// Lấy vị trí hub đã lưu. Trả về false nếu chưa có lần nào được lưu.
    /// </summary>
    public bool TryGetHubPosition(out Vector3 pos, out float rotY) {
        pos = new Vector3(gameSave.hubPosX, gameSave.hubPosY, gameSave.hubPosZ);
        rotY = gameSave.hubRotY;
        return gameSave.hasHubPosition;
    }
}
