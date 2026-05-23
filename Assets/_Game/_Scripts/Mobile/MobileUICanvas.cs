using Aircraft;
using UnityEngine;

/// <summary>
/// Gắn lên root Canvas của mobile UI. Bật/tắt toàn bộ canvas dựa trên platform.
/// Bật showOnPC để test mobile UI trên máy tính trong Editor.
/// </summary>
public class MobileUICanvas : MonoBehaviour {
    [Tooltip("Hiển thị mobile UI trên PC (để test trong Editor)")]
    public bool showOnPC = true;

    void Awake() {
        showOnPC = GameManager.Instance.bool_isMobile;
        bool shouldShow = Application.isMobilePlatform || showOnPC;
        gameObject.SetActive(shouldShow);
    }
}
