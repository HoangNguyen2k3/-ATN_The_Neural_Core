using Aircraft;
using UnityEngine;

public class PortalMission : MonoBehaviour {
    private string sceneToLoad; // Tên của Scene nhiệm vụ muốn chuyển đến
    public bool isOpenPortal = true;
    public CurrentIsland currentIsland;
    [Header("=============Island1================")]
    public int numberOfLevel = 0;
    private void Start() {
        sceneToLoad = currentIsland.ToString();
    }
    private void OnTriggerEnter(Collider other) {
        // Kiểm tra nếu đối tượng va chạm là Player và cổng đã mở
        if (other.CompareTag("Player") && isOpenPortal) {
            TransportToMission();
        }
        else if (other.CompareTag("Player") && !isOpenPortal) {
            Debug.Log("Cổng đang đóng. Bạn cần hoàn thành điều kiện để mở!");
        }
    }
    private void TransportToMission() {
        SetupDataToMission();
        if (!string.IsNullOrEmpty(sceneToLoad)) {
            Cursor.lockState = CursorLockMode.None;
            GameManager.Instance.ShowFakeLoadingGame(sceneToLoad);
        }
        else {
            Debug.LogWarning("Chưa nhập tên Scene trong Inspector!");
        }
    }
    public void SetupDataToMission() {
        switch (currentIsland) {
            case CurrentIsland.MainMenuFlyIsland:
                GameManager.Instance.numberLevel = numberOfLevel; break;

        }
    }
}
