using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour {
    public void ChangedScene(string sceneChange) {
        SceneManager.LoadScene(sceneChange);
    }
}
