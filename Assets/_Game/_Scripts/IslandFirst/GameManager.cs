using System;
using UnityEngine;

namespace Aircraft {
    public enum GameDifficulty {
        Easy,
        Normal,
        Hard,
        Imposible
    }

    public delegate void OnStateChangeHandler();

    public class GameManager : MonoBehaviour {
        public static GameManager Instance {
            get; private set;
        }
        public event OnStateChangeHandler OnStateChange;
        [Header("============Loading==============")]
        public GameObject canvasLoading;
        public UI_Loading ui_fakeLoading;
        public DataManager dataManager;
        [Header("=============Data Change Scene==============")]
        //Island 1
        public GameDifficulty GameDifficultyIsland1 { get; set; }
        public int numberLevel = 0;
        public int DifficultyCountIsland1 { get; set; } = 4;
        //Island 2
        public GameDifficulty GameDifficultyIsland2 { get; set; }
        public int DifficultyCountIsland2 { get; set; } = 4;
        //Island 3
        public GameDifficulty GameDifficultyIsland3 { get; set; }
        public int DifficultyCountIsland3 { get; set; } = 3;
        [Header("=============Data Game======================")]
        public GameDataConfig gameDataConfig;
        public bool bool_isMobile = false;
        private void Awake() {
            if (Instance == null) {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
            }
            else {
                Destroy(gameObject);
            }
        }

        public void OnApplicationQuit() {
            Instance = null;
        }
        /// <summary>
        /// Điểm vào duy nhất cho MỌI chuyển scene trong game.
        /// Bật canvas loading → load async thật → tắt canvas khi xong.
        /// </summary>
        public void GoToScene(string sceneName) {
            canvasLoading.SetActive(true);
            ui_fakeLoading.OnLoadComplete = () => canvasLoading.SetActive(false);
            ui_fakeLoading.ShowRealLoading(sceneName);
        }

        // Giữ lại để backward-compatible, redirect sang GoToScene
        public void LoadLevel(string levelName) => GoToScene(levelName);
        public void ShowFakeLoadingGame(string levelName) => GoToScene(levelName);

        private void Start() {
            dataManager.Init();
        }

    }
}
[Serializable]
public enum CurrentIsland {
    StartScene,
    MainMenuFlyIsland,
    MainMenuIsland2,
    Island3_MenuBoard,
    FinalBossScene,
}
