#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

public class OpenScenes : Editor {
    [MenuItem("Open Scenes/_01_Loading_#1")]
    public static void OpenLoading() {
        OpenScene("_Game/_Scene/Loading");
    }
    [MenuItem("Open Scenes/_02_MainIsland_#2")]
    public static void OpenMainIsland() {
        OpenScene("_Game/_Scene/StartScene");
    }
    [MenuItem("Open Scenes/_03_FlyIsland_#3")]
    public static void OpenFlyIsland() {
        OpenScene("_Game/_Scene/_Island1/FlyIsland");
    }
    [MenuItem("Open Scenes/_04_MenuFlyIsland_#4")]
    public static void OpenMainMenuFlyIsland() {
        OpenScene("_Game/_Scene/_Island1/MainMenuFlyIsland");
    }
    [MenuItem("Open Scenes/_05_DroneIsland_#5")]
    public static void OpenDroneIsland() {
        OpenScene("_Game/_Scene/DroneIsland");
    }
    private static void OpenScene(string path) {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
            EditorSceneManager.OpenScene("Assets/" + path + ".unity");
        }
    }
}
#endif