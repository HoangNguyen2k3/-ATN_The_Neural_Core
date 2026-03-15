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
    }

    private void SetupDialogData() {
        if (currentPortal == null) return;

        areaNameText.text = currentPortal.myTextData.areaName;
        descriptionText.text = currentPortal.myTextData.areaDescription;

        // Xử lý thông tin viên đá ký ức
        if (currentPortal.hasMemoryStone)
            stoneStatusText.text = "01 (Claimed)";
        else
            stoneStatusText.text = "00";

        // Kiểm tra biến isOpenPortal (true = mở, false = đóng)
        if (!currentPortal.isOpenPortal) {
            statusText.text = "Status: Lock";
            conditionText.text = currentPortal.myTextData.unlockCondition;
            confirmButton.interactable = false; // Khoá nút Đồng ý
        }
        else {
            statusText.text = "Status: UnLock";
            conditionText.text = "Condition: Passed";
            confirmButton.interactable = true; // Mở nút Đồng ý
        }
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