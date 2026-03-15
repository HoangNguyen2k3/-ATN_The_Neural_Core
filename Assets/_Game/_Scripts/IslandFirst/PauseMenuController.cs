using UnityEngine;
using static Aircraft.RaceManager;

namespace Aircraft {
    public class PauseMenuController : MonoBehaviour {
        private void Start() {
            GameManager.Instance.OnStateChange += OnStateChange;
        }

        private void OnStateChange() {
            if (RaceManager.Ins.CurrentState == GameState.Playing) {
                gameObject.SetActive(false);
            }
        }

        public void ResumeButtonClicked() {
            RaceManager.Ins.SetGameState(GameState.Playing);
        }

        public void MainMenuButtonClicked() {
            GameManager.Instance.LoadLevel("MainMenu");
        }

        private void OnDestroy() {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChange -= OnStateChange;
        }
    }
}
