using UnityEngine;

public class UI_Animation : MonoBehaviour {
    public enum AnimationType { None, Scale, Rotation, Floating }

    public AnimationType animationType;

    [Header("--- SCALE (To nhỏ) ---")]
    public float scaleSpeed = 2f;
    public float scaleAmount = 0.1f;

    [Header("--- ROTATION (Xoay) ---")]
    public float rotateSpeed = 50f;
    public Vector3 rotateAxis = Vector3.forward;

    [Header("--- FLOATING (Bay bổng) ---")]
    public float floatSpeed = 2f;
    public float floatAmount = 10f;

    private Vector3 _initialScale;
    private Vector3 _initialPosition;
    private RectTransform _rectTransform;

    void Awake() {
        _rectTransform = GetComponent<RectTransform>();
        _initialScale = _rectTransform.localScale;
        _initialPosition = _rectTransform.anchoredPosition;
    }

    void Update() {
        switch (animationType) {
            case AnimationType.Scale:
                HandleScale();
                break;
            case AnimationType.Rotation:
                HandleRotation();
                break;
            case AnimationType.Floating:
                HandleFloating();
                break;
        }
    }

    private void HandleScale() {
        // Tạo hiệu ứng nhịp thở (Pulsing)
        float sin = Mathf.Sin(Time.time * scaleSpeed);
        float scaleFactor = 1 + (sin * scaleAmount);
        _rectTransform.localScale = _initialScale * scaleFactor;
    }

    private void HandleRotation() {
        // Xoay liên tục quanh trục chỉ định
        _rectTransform.Rotate(rotateAxis * rotateSpeed * Time.deltaTime);
    }

    private void HandleFloating() {
        // Di chuyển lên xuống nhẹ nhàng
        float newY = Mathf.Sin(Time.time * floatSpeed) * floatAmount;
        _rectTransform.anchoredPosition = (Vector2)_initialPosition + new Vector2(0, newY);
    }

    // Hàm bổ trợ để reset về trạng thái ban đầu nếu cần
    public void ResetUI() {
        _rectTransform.localScale = _initialScale;
        _rectTransform.anchoredPosition = _initialPosition;
        _rectTransform.localRotation = Quaternion.identity;
    }
}