using System;
using UnityEngine;

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
        //        LoadData();
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
}
