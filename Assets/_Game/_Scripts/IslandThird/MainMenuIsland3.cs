using System.Collections.Generic;
using Aircraft;
using UnityEngine;

public class MainMenuIsland3 : MonoBehaviour {
    [Header("UI Cấu Hình")]
    private string sceneToStart = "Island3_Gameplay";
    private GameDifficulty selectedDifficulty;
    public List<GameObject> list_borderObj = new();

    void Start() {
        selectedDifficulty = GameDifficulty.Easy;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    public void SetDifficulty(int difficultyIndex) {
        selectedDifficulty = (GameDifficulty)difficultyIndex;
        foreach (GameObject obj in list_borderObj) {
            obj.SetActive(false);
        }
        list_borderObj[difficultyIndex].SetActive(true);
    }
    public void StartButtonClicked() {
        GameManager.Instance.GameDifficultyIsland3 = selectedDifficulty;
        GameManager.Instance.LoadLevel(sceneToStart);
    }
    public void QuitButtonClicked() {
        GameManager.Instance.LoadLevel("StartScene");
    }
}
