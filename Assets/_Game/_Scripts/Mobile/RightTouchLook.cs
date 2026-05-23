using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Panel transparent phủ nửa phải màn hình. Drag trên panel → xoay camera.
/// Ghi delta vào MobileInputBridge.CameraLookDelta.
///
/// Setup:
///   1. Tạo Image transparent (alpha = 0), anchor phủ nửa phải màn hình.
///   2. Thêm script này + component EventTrigger (hoặc chỉ script này nếu dùng IPointer).
///   3. Đảm bảo Canvas có GraphicRaycaster.
/// </summary>
public class RightTouchLook : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("Độ nhạy xoay camera (cao hơn = xoay nhanh hơn)")]
    public float sensitivity = 150f;

    private bool _isDragging;
    private Vector2 _lastPos;

    public void OnPointerDown(PointerEventData eventData) {
        _isDragging = true;
        _lastPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData) {
        if (!_isDragging) return;

        Vector2 delta = eventData.position - _lastPos;
        _lastPos = eventData.position;

        // Normalize theo screen size để nhất quán trên mọi độ phân giải
        Vector2 normalized = new Vector2(
            delta.x / Screen.width,
            delta.y / Screen.height
        ) * sensitivity;

        MobileInputBridge.CameraLookDelta = normalized;
    }

    public void OnPointerUp(PointerEventData eventData) {
        _isDragging = false;
        MobileInputBridge.CameraLookDelta = Vector2.zero;
    }

    // Đảm bảo clear khi bị disable (ví dụ khi mở menu)
    void OnDisable() {
        _isDragging = false;
        MobileInputBridge.CameraLookDelta = Vector2.zero;
    }
}
