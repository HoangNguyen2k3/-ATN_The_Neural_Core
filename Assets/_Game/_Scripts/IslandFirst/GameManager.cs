using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [Header("=============Data Change Scene==============")]
        //Island 1
        public GameDifficulty GameDifficultyIsland1 { get; set; }
        public int numberLevel = 0;
        //Island 2
        public GameDifficulty GameDifficultyIsland2 { get; set; }
        [Header("=============Data Game======================")]
        public GameDataConfig gameDataConfig;
        private void Awake() {
            if (Instance == null) {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
            }
            else {
                Destroy(gameObject);
            }
        }

        public void OnApplicationQuit() {
            Instance = null;
        }
        public void LoadLevel(string levelName) {
            StartCoroutine(LoadLevelAsync(levelName));
        }

        private IEnumerator LoadLevelAsync(string levelName) {
            AsyncOperation operation = SceneManager.LoadSceneAsync(levelName);
            while (operation.isDone == false) {
                yield return null;
            }
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
        }
        public void ShowFakeLoadingGame(string levelName) {
            ui_fakeLoading.ShowFakeLoading(2, levelName);
        }
    }
}
[Serializable]
public enum CurrentIsland {
    StartScene,
    MainMenuFlyIsland,
    MainMenuIsland2,
    Island3,
}
