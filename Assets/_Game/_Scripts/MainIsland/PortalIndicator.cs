using UnityEngine;

public class PortalIndicator : MonoBehaviour {
    [Header("Target & UI")]
    public Transform targetPortal;
    public GameObject indicatorUI;
    public RectTransform indicatorRect;

    [Header("Settings")]
    public float edgeMargin = 50f;

    private bool isActive = false;
    private Camera mainCam;

    private void Start() {
        mainCam = Camera.main;
        indicatorUI.SetActive(false);
    }

    public void ShowIndicator() {
        isActive = true;
        indicatorUI.SetActive(true);
    }

    public void HideIndicator() {
        isActive = false;
        indicatorUI.SetActive(false);
    }

    private void Update() {
        if (!isActive || targetPortal == null) return;

        // 1. Lấy vị trí của cổng trên màn hình
        Vector3 screenPos = mainCam.WorldToScreenPoint(targetPortal.position);

        // 2. Xử lý khi cổng nằm sau lưng Camera
        if (screenPos.z < 0) {
            screenPos *= -1; // Đảo ngược để mũi tên cắm về phía ngược lại
        }

        // 3. Tính toán góc xoay (Rotation)
        // Tìm tâm màn hình
        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

        // Hướng từ tâm màn hình chỉ về phía cái cổng
        Vector3 direction = screenPos - screenCenter;

        // Tính góc bằng Atan2 (trả về radian, nhân Rad2Deg để ra độ)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Xoay RectTransform. 
        // LƯU Ý: Trừ đi 90 độ vì mặc định góc 0 của Unity là hướng sang phải (Right), 
        // trong khi đa số ảnh UI mũi tên thường vẽ hướng lên trên (Up).
        // Nếu ảnh mũi tên của bạn vốn đã hướng sang phải, hãy xóa chữ "- 90f" đi nhé.
        indicatorRect.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // 4. Ép vị trí của Icon nằm trong giới hạn màn hình (Clamp) để không bị lẹm
        screenPos.x = Mathf.Clamp(screenPos.x, edgeMargin, Screen.width - edgeMargin);
        screenPos.y = Mathf.Clamp(screenPos.y, edgeMargin, Screen.height - edgeMargin);
        screenPos.z = 0; // Đặt Z = 0 cho UI 2D

        // 5. Áp dụng vị trí
        indicatorRect.position = screenPos;
    }
}