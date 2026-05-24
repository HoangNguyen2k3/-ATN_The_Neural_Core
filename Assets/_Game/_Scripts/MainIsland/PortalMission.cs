using Aircraft;
using UnityEngine;

public class PortalMission : MonoBehaviour {
    [Header("Cấu hình Dữ Liệu (Scriptable Object)")]
    public PortalType myPortalType; // Cổng này là cổng nào?

    [Header("Trạng thái In-game (Có thể thay đổi)")]
    public bool isOpenPortal = true;

    [Header("Stone Config")]
    [Tooltip("-1 = portal không có đá. 0=Snow, 1=Desert, 2=Island2, 3=Island3. -2 = cổng Final Boss (check all 4 stones).")]
    public int stoneIndex = -1;
    [Tooltip("Số cấp độ khó của portal này (3 hoặc 4). Dùng để tính % mỗi lần win.")]
    public int totalDifficultyLevels = 4;

    // Tính tự động từ DataManager — không cần set trong Inspector
    public bool hasMemoryStone =>
        stoneIndex >= 0 && DataManager.Ins != null
        && DataManager.Ins.gameSave.stonePercent[stoneIndex] > 0f;

    public float stoneFillPercent =>
        stoneIndex >= 0 && DataManager.Ins != null
        ? DataManager.Ins.GetStonePercent(stoneIndex)
        : 0f;

    [Header("Cấu hình Scene")]
    public CurrentIsland currentIsland;

    [Header("Final Boss Scene (chỉ dùng khi stoneIndex == -2)")]
    [Tooltip("Tên scene riêng cho chuỗi kết thúc game")]
    public string finalArenaSceneName = "FinalArena";

    [Header("=============Island1================")]
    public int numberOfLevel = 0;

    [HideInInspector]
    public string sceneToLoad;

    // Biến để lưu trữ data text lấy được từ SO
    [HideInInspector]
    public PortalInfo myTextData;

    private void Start() {
        sceneToLoad = currentIsland.ToString();

        if (GameManager.Instance != null) {
            myTextData = GameManager.Instance.gameDataConfig.databaseSO.GetPortalData(myPortalType);
            if (myTextData == null)
                Debug.LogError($"Không tìm thấy dữ liệu Text cho cổng: {myPortalType} trong Database!");
        }

        // Final Boss portal: tự kiểm tra điều kiện mỗi lần scene load
        if (stoneIndex == -2 && DataManager.Ins != null && DataManager.Ins.isLoaded)
            isOpenPortal = DataManager.Ins.AllStonesAt100();
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player") && myTextData != null) {
            // Truyền chính script này sang UI để hiển thị
            PortalUIManager.Instance.ShowDialog(this);
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.CompareTag("Player")) {
            PortalUIManager.Instance.HideDialog();
        }
    }

    public void TransportToMission() {
        // Final Boss portal: load scene riêng cho chuỗi kết thúc
        if (stoneIndex == -2) {
            LoadFinalArenaScene();
            return;
        }

        SetupDataToMission();

        if (!string.IsNullOrEmpty(sceneToLoad)) {
            Cursor.lockState = CursorLockMode.None;
            GameManager.Instance.ShowFakeLoadingGame(sceneToLoad);
        }
        else {
            Debug.LogWarning("Tên Scene trống! Kiểm tra lại Enum CurrentIsland.");
        }
    }

    private void LoadFinalArenaScene() {
        // Lưu vị trí hub trước khi chuyển scene
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && DataManager.Ins != null && DataManager.Ins.isLoaded)
            DataManager.Ins.SaveHubPosition(player.transform.position, player.transform.eulerAngles.y);

        PortalUIManager.Instance.HideDialog();
        Cursor.lockState = CursorLockMode.None;

        if (GameManager.Instance != null)
            GameManager.Instance.ShowFakeLoadingGame(finalArenaSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(finalArenaSceneName);
    }

    public void SetupDataToMission() {
        switch (currentIsland) {
            case CurrentIsland.MainMenuFlyIsland:
                GameManager.Instance.numberLevel = numberOfLevel;
                GameManager.Instance.DifficultyCountIsland1 = totalDifficultyLevels;
                break;
            case CurrentIsland.MainMenuIsland2:
                GameManager.Instance.numberLevel = numberOfLevel;
                GameManager.Instance.DifficultyCountIsland2 = totalDifficultyLevels;
                break;
            case CurrentIsland.Island3_MenuBoard:
                GameManager.Instance.DifficultyCountIsland3 = totalDifficultyLevels;
                break;
        }
    }
}