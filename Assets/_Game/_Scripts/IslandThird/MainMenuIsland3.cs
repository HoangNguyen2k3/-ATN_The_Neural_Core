using System;
using System.Linq;
using Aircraft;
using TMPro;
using UnityEngine;

/// <summary>
/// Menu Đảo 3 — Chọn độ khó AI Boss rồi bắt đầu trận đấu.
/// Pattern giống MainMenuIsland2.cs.
/// </summary>
public class MainMenuIsland3 : MonoBehaviour {
    [Header("UI Cấu Hình")]
    [Tooltip("Dropdown chọn cấp độ / độ khó AI Boss")]
    public TMP_Dropdown difficultyDropdown;

    [Tooltip("Tên Scene gameplay Đảo 3")]
    private string sceneToStart = "Island3Scene";

    private GameDifficulty selectedDifficulty;

    void Start() {
        if (difficultyDropdown != null) {
            difficultyDropdown.ClearOptions();
            difficultyDropdown.AddOptions(Enum.GetNames(typeof(GameDifficulty)).ToList());
            selectedDifficulty = GameDifficulty.Easy;
        } else {
            Debug.LogWarning("[MainMenuIsland3] Chưa gán TMP_Dropdown.");
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary> Gắn vào Event OnValueChanged của Dropdown </summary>
    public void SetDifficulty(int difficultyIndex) {
        selectedDifficulty = (GameDifficulty)difficultyIndex;
        Debug.Log("[MainMenuIsland3] Đã đổi AI Boss sang: " + selectedDifficulty);
    }

    /// <summary> Gắn vào Nút Bắt Đầu (Start Button) </summary>
    public void StartButtonClicked() {
        GameManager.Instance.GameDifficultyIsland3 = selectedDifficulty;
        GameManager.Instance.LoadLevel(sceneToStart);
    }

    /// <summary> Gắn vào Nút Thoát (Quit/Back) </summary>
    public void QuitButtonClicked() {
        GameManager.Instance.LoadLevel("StartScene");
    }
}
