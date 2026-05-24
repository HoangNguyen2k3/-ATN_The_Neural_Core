using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PortalUIManager : MonoBehaviour {
    public static PortalUIManager Instance;

    [Header("UI References")]
    public GameObject dialogPanel;
    public TextMeshProUGUI areaNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI conditionText;
    public TextMeshProUGUI stoneStatusText; // Text cho viên đá ký ức

    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    private PortalMission currentPortal;

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        dialogPanel.SetActive(false);

        confirmButton.onClick.AddListener(OnConfirmClicked);
        cancelButton.onClick.AddListener(HideDialog);
    }

    public void ShowDialog(PortalMission portal) {
        currentPortal = portal;
        SetupDialogData();
        dialogPanel.SetActive(true);

        // Mở khoá chuột để người chơi có thể bấm nút (nếu game 3D của bạn đang khoá chuột)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void SetupDialogData() {
        if (currentPortal == null) return;

        areaNameText.text = currentPortal.myTextData.areaName;
        descriptionText.text = currentPortal.myTextData.areaDescription;

        // ── Hiển thị trạng thái viên đá ──────────────────────────────
        if (currentPortal.stoneIndex == -2) {
            // Final Boss portal: hiện trạng thái cả 4 đá
            if (stoneStatusText != null)
                stoneStatusText.text = BuildFinalBossStatus();
        }
        else if (currentPortal.stoneIndex >= 0) {
            float pct = currentPortal.stoneFillPercent;
            int pctInt = Mathf.RoundToInt(pct * 100f);
            if (stoneStatusText != null)
                stoneStatusText.text = pctInt > 0
                    ? $"Memory Stone: {StoneBar(pct)} {pctInt}%"
                    : "Memory Stone: --";
        }
        else {
            if (stoneStatusText != null) stoneStatusText.text = "";
        }

        // ── Lock / Unlock ─────────────────────────────────────────────
        if (!currentPortal.isOpenPortal) {
            statusText.text = "Status: Lock";
            conditionText.text = currentPortal.stoneIndex == -2
                ? "Thu thập đủ 4 viên đá 100% để mở khoá"
                : currentPortal.myTextData.unlockCondition;
            confirmButton.interactable = false;
        }
        else {
            statusText.text = "Status: UnLock";
            conditionText.text = "Condition: Passed";
            confirmButton.interactable = true;
        }
    }

    // "●●○○" — 4 ô fill theo % (4 mức tương ứng 4 lần win)
    private string StoneBar(float percent) {
        int filled = Mathf.RoundToInt(percent * 4f);
        string bar = "";
        for (int i = 0; i < 4; i++) bar += i < filled ? "●" : "○";
        return bar;
    }

    // Hiển thị trạng thái từng đá cho final boss portal
    private string BuildFinalBossStatus() {
        if (DataManager.Ins == null) return "";
        string[] names = { "Sky Race I", "Sky Race II", "Stealth Zone", "Boss Arena" };
        string result = "";
        for (int i = 0; i < 4; i++) {
            float p = DataManager.Ins.GetStonePercent(i);
            int pInt = Mathf.RoundToInt(p * 100f);
            result += p >= 1f ? $"100%_" : $"{pInt}%_";
        }
        return result.TrimEnd();
    }

    public void HideDialog() {
        dialogPanel.SetActive(false);
        currentPortal = null;

        // Trả lại trạng thái chuột vào game (Tuỳ thuộc vào logic game của bạn)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnConfirmClicked() {
        if (currentPortal != null && currentPortal.isOpenPortal) {
            // Ẩn Dialog
            dialogPanel.SetActive(false);

            // Chuyển việc load scene lại cho PortalMission xử lý
            currentPortal.TransportToMission();
        }
    }
}