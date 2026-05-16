using Aircraft; // Đảm bảo namespace này khớp với project của bạn
using TMPro;
using UnityEngine;

public class NPCQuestGiver : MonoBehaviour {
    [Header("Data References")]
    public int currentGameStage = 0;  // Tiến độ hiện tại (0: Đảo 1, 1: Đảo 2, 2: Đảo 3)

    [Header("UI & Visuals")]
    public GameObject exclamationMark;
    public GameObject interactButton;  // Nút "Nói chuyện" hiện lên khi đến gần (Dùng cho Mobile)

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;   // Khung nền chứa chữ
    public TextMeshProUGUI dialogueText; // Component Text hiển thị nội dung
    public GameObject nextButton;      // Nút "Tiếp tục" hoặc click vào màn hình để qua câu

    private int currentLineIndex = 0;
    private string[] currentDialogueLines; // Biến tạm lưu kịch bản đang đọc

    [Header("Quest Target")]
    public PortalIndicator targetIndicator;

    private bool isPlayerInRange = false;
    private bool isTalking = false;
    private bool questAssigned = false;

    private void Start() {
        if (exclamationMark != null) exclamationMark.SetActive(true);
        if (interactButton != null) interactButton.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    private void Update() {
        // Hỗ trợ test trên PC bằng phím E 
        if (isPlayerInRange && !questAssigned && Input.GetKeyDown(KeyCode.E)) {
            if (!isTalking) {
                StartDialogue();
            }
            else {
                DisplayNextLine();
            }
        }
    }

    // Gán hàm này vào sự kiện OnClick() của InteractButton trên UI
    public void StartDialogue() {
        isTalking = true; // Đánh dấu là đang nói chuyện

        // Ẩn các UI không cần thiết
        if (interactButton != null) interactButton.SetActive(false);
        if (exclamationMark != null) exclamationMark.SetActive(false);

        // MỞ KHÓA VÀ HIỆN CON TRỎ CHUỘT (Dành cho PC)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        // Lấy kịch bản từ GameManager
        currentDialogueLines = GameManager.Instance.gameDataConfig.npcDialogueDB.allStages[currentGameStage].dialogueLines;

        dialoguePanel.SetActive(true);
        currentLineIndex = 0;
        dialogueText.text = currentDialogueLines[currentLineIndex];
    }

    // Gán hàm này vào sự kiện OnClick() của cái Nút/Khung thoại
    public void DisplayNextLine() {
        currentLineIndex++;

        if (currentLineIndex < currentDialogueLines.Length) {
            dialogueText.text = currentDialogueLines[currentLineIndex];
        }
        else {
            EndDialogue();
        }
    }

    private void EndDialogue() {
        isTalking = false;
        questAssigned = true;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        // KHÓA VÀ ẨN CON TRỎ CHUỘT (Dành cho PC)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Kích hoạt mũi tên chỉ đường tới cổng
        if (targetIndicator != null) {
            targetIndicator.ShowIndicator();
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player") && !questAssigned) {
            isPlayerInRange = true;
            if (interactButton != null) interactButton.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.CompareTag("Player")) {
            isPlayerInRange = false;
            if (interactButton != null) interactButton.SetActive(false);

            // Nếu bỏ chạy giữa chừng lúc đang nói chuyện thì tắt khung thoại
            if (isTalking) {
                isTalking = false;
                if (dialoguePanel != null) dialoguePanel.SetActive(false);
                if (exclamationMark != null) exclamationMark.SetActive(true); // Bật lại dấu !

                // KHÓA CHUỘT LẠI KHI BỎ CHẠY
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}