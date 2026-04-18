using System;
using System.Linq;
using Aircraft; // Bắt buộc để nhận GameDifficulty từ GameManager
using TMPro;
using UnityEngine;

public class MainMenuIsland2 : MonoBehaviour {
    [Header("UI Cấu Hình")]
    [Tooltip("Dropdown chọn cấp độ / độ khó để đổi não AI")]
    public TMP_Dropdown difficultyDropdown;

    [Tooltip("Tên Scene tương ứng với Đảo 2 (Thường là Island2 Gameplay)")]
    private string sceneToStart = "Island2Scene";

    private GameDifficulty selectedDifficulty;

    private void Start() {
        if (difficultyDropdown != null) {
            difficultyDropdown.ClearOptions();
            // Lấy danh sách độ khó (Easy, Normal, Hard, Imposible)
            difficultyDropdown.AddOptions(Enum.GetNames(typeof(GameDifficulty)).ToList());
            selectedDifficulty = GameDifficulty.Easy;
        }
        else {
            Debug.LogWarning("[MainMenuIsland2] Chưa gán TMP_Dropdown.");
        }

        // Tùy chỉnh: Nếu bạn truyền numberLevel từ cổng Portal, 
        // bạn có thể dùng nó để Bật/Tắt các vật thể Demo ở đây
        // int level = GameManager.Instance.numberLevel;
    }

    /// <summary> Gắn vào Event OnValueChanged của Dropdown </summary>
    public void SetDifficulty(int difficultyIndex) {
        selectedDifficulty = (GameDifficulty)difficultyIndex;
        Debug.Log("[MainMenuIsland2] Đã đổi AI Model sang: " + selectedDifficulty);
    }

    /// <summary> Gắn vào Nút Bắt Đầu (Start Button) </summary>
    public void StartButtonClicked() {
        // Lưu lựa chọn vào Quản lý tổng 
        GameManager.Instance.GameDifficultyIsland2 = selectedDifficulty;

        // Bắt đầu load scene chơi
        GameManager.Instance.LoadLevel(sceneToStart);
    }

    /// <summary> Gắn vào Nút Thoát (Quit/Back) </summary>
    public void QuitButtonClicked() {
        // Có thể lùi về MainIsland hoặc thoát game
        GameManager.Instance.LoadLevel("StartScene");
    }
}
