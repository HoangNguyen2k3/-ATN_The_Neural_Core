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
        OpenScene("_Game/_Scene/_Island2/Island2Scene");
    }
    [MenuItem("Open Scenes/_06_MenuDroneIsland_#6")]
    public static void OpenMenuDroneIsland() {
        OpenScene("_Game/_Scene/_Island2/MainMenuIsland2");
    }
    [MenuItem("Open Scenes/_07_CombatIsland_#7")]
    public static void OpenCombatIsland() {
        OpenScene("_Game/_Scene/_Island3/Island3_Gameplay");
    }
    [MenuItem("Open Scenes/_08_MenuCombatIsland_#8")]
    public static void OpenMenuCombatIsland() {
        OpenScene("_Game/_Scene/_Island3/Island3_MenuBoard");
    }
    [MenuItem("Open Scenes/_09_TRAIN_CombatIsland_#9")]
    public static void Train_OpenMenuCombatIsland() {
        OpenScene("_Game/_Scene/_Island3/Train_Island3");
    }
    private static void OpenScene(string path) {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
            EditorSceneManager.OpenScene("Assets/" + path + ".unity");
        }
    }
}
#endif