using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Fixed virtual joystick — đặt cố định góc trái màn hình.
/// Kéo knob trong vòng tròn → ghi normalized Vector2 vào MobileInputBridge.MoveInput.
///
/// Setup:
///   1. Tạo Image (vòng tròn ngoài) → thêm script này.
///   2. Tạo Image con (knob) → kéo vào trường knobTransform.
///   3. Đặt outerRadius khớp với bán kính vòng ngoài (pixels).
/// </summary>
public class VirtualJoystick : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Tooltip("RectTransform của nút knob (vòng tròn trong)")]
    public RectTransform knobTransform;

    [Tooltip("Bán kính tối đa knob có thể dịch chuyển (pixels)")]
    public float outerRadius = 80f;

    private RectTransform _rect;
    private Vector2 _center;
    private bool _isDragging;

    void Awake() {
        _rect = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData) {
        _isDragging = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, eventData.position, eventData.pressEventCamera, out _center);
        UpdateJoystick(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData) {
        if (!_isDragging) return;
        UpdateJoystick(eventData.position, eventData.pressEventCamera);
    }

    public void OnPointerUp(PointerEventData eventData) {
        _isDragging = false;
        MobileInputBridge.MoveInput = Vector2.zero;
        if (knobTransform != null) knobTransform.anchoredPosition = Vector2.zero;
    }

    private void UpdateJoystick(Vector2 screenPos, Camera cam) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, screenPos, cam, out Vector2 localPos);

        Vector2 offset = localPos - _center;
        Vector2 clamped = Vector2.ClampMagnitude(offset, outerRadius);

        if (knobTransform != null)
            knobTransform.anchoredPosition = clamped;

        MobileInputBridge.MoveInput = clamped / outerRadius;
    }
}
