using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD nhỏ ở góc màn hình Hub, hiển thị tiến trình 4 viên đá.
/// Gắn lên Canvas trong StartScene.
/// </summary>
public class StoneHUDController : MonoBehaviour {
    [System.Serializable]
    public struct StoneSlot {
        [Tooltip("Image type = Filled, Fill Method = Horizontal")]
        public Image fillImage;
        [Tooltip("Text hiển thị % như '25%' hoặc '--'")]
        public TextMeshProUGUI percentLabel;
    }

    [Header("4 slot tương ứng: Snow / Desert / Island2 / Island3")]
    public StoneSlot[] slots = new StoneSlot[4];

    void OnEnable() => Refresh();

    /// <summary> Gọi sau mỗi khi dữ liệu stone thay đổi để cập nhật UI. </summary>
    public void Refresh() {
        if (DataManager.Ins == null || !DataManager.Ins.isLoaded) return;

        for (int i = 0; i < 4; i++) {
            float pct = DataManager.Ins.GetStonePercent(i);
            int pctInt = Mathf.RoundToInt(pct * 100f);

            if (slots[i].fillImage != null)
                slots[i].fillImage.fillAmount = pct;

            if (slots[i].percentLabel != null)
                slots[i].percentLabel.text = pctInt > 0 ? $"{pctInt}%" : "--";
        }
    }
}
