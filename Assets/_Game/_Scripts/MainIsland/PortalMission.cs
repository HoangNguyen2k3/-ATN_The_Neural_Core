using Aircraft;
using UnityEngine;

public class PortalMission : MonoBehaviour {
    [Header("Cấu hình Dữ Liệu (Scriptable Object)")]
    public PortalType myPortalType; // Cổng này là cổng nào?

    [Header("Trạng thái In-game (Có thể thay đổi)")]
    public bool isOpenPortal = true;
    public bool hasMemoryStone = false;

    [Header("Cấu hình Scene")]
    public CurrentIsland currentIsland;

    [Header("=============Island1================")]
    public int numberOfLevel = 0;

    [HideInInspector]
    public string sceneToLoad;

    // Biến để lưu trữ data text lấy được từ SO
    [HideInInspector]
    public PortalInfo myTextData;

    private void Start() {
        sceneToLoad = currentIsland.ToString();

        // Lấy dữ liệu Text từ Database ngay khi game bắt đầu
        if (GameManager.Instance != null) {
            myTextData = GameManager.Instance.gameDataConfig.databaseSO.GetPortalData(myPortalType);
            if (myTextData == null) {
                Debug.LogError($"Không tìm thấy dữ liệu Text cho cổng: {myPortalType} trong Database!");
            }
        }
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
        SetupDataToMission();

        if (!string.IsNullOrEmpty(sceneToLoad)) {
            Cursor.lockState = CursorLockMode.None;
            GameManager.Instance.ShowFakeLoadingGame(sceneToLoad);
        }
        else {
            Debug.LogWarning("Tên Scene trống! Kiểm tra lại Enum CurrentIsland.");
        }
    }

    public void SetupDataToMission() {
        switch (currentIsland) {
            case CurrentIsland.MainMenuFlyIsland:
                GameManager.Instance.numberLevel = numberOfLevel;
                break;
            case CurrentIsland.MainMenuIsland2:
                // Truyền tham số số thứ tự level sang menu của Đảo 2 
                // để hệ thống hiển thị / xử lý map preview tương ứng
                GameManager.Instance.numberLevel = numberOfLevel;
                break;
            case CurrentIsland.Island3:
                // Island 3 không cần numberLevel,
                // MainMenuIsland3 sẽ xử lý chọn độ khó AI
                break;
        }
    }
}