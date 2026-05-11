using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour {
    public void ChangedScene(string sceneChange) {
        if (Aircraft.GameManager.Instance != null)
            Aircraft.GameManager.Instance.GoToScene(sceneChange);
        else
            SceneManager.LoadScene(sceneChange); // fallback nếu chạy scene độc lập
    }
}
