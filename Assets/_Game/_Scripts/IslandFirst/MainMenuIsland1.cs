using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using TMPro;
using UnityEngine;
namespace Aircraft {

    public class MainMenuIsland1 : MonoBehaviour {
        [Tooltip("The list of levels (scene names) that can be loaded")]
        public List<string> levels;

        [Tooltip("The dropdown for selecting the game difficulty")]
        public TMP_Dropdown difficultyDropdown;

        public string selectedLevel;
        private GameDifficulty selectedDifficulty;

        public List<AircraftArea> list_DemoArea = new();
        public CinemachineVirtualCamera virtualCamera;
        private void Start() {
            difficultyDropdown.ClearOptions();
            difficultyDropdown.AddOptions(Enum.GetNames(typeof(GameDifficulty)).ToList());
            selectedDifficulty = GameDifficulty.Easy;
            Setup();
        }
        public void Setup() {
            int level = GameManager.Instance.numberLevel;
            var area = Instantiate(list_DemoArea[level]);
            virtualCamera.Follow = area.airCraftTarget;
            virtualCamera.LookAt = area.airCraftTarget;
            SetLevel(level);
        }
        public void SetLevel(int levelIndex) {
            selectedLevel = levels[levelIndex];
        }

        public void SetDifficulty(int difficultyIndex) {
            selectedDifficulty = (GameDifficulty)difficultyIndex;
            Debug.Log(selectedDifficulty + "SET");
        }
        public void StartButtonClicked() {
            GameManager.Instance.GameDifficulty = selectedDifficulty;
            GameManager.Instance.LoadLevel(selectedLevel);
        }
        public void QuitButtonClicked() {
            Application.Quit();
        }
    }
}
