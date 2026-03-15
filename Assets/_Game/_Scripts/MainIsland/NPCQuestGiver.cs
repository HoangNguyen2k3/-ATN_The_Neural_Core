using UnityEngine;

public class NPCQuestGiver : MonoBehaviour {
    [Header("UI & Visuals")]
    public GameObject exclamationMark; // Kéo object dấu chấm than 3D/2D trên đầu NPC vào đây

    [Header("Quest Target")]
    public PortalIndicator targetIndicator; // Trỏ tới script Indicator của cổng muốn dẫn đến

    private bool isPlayerInRange = false;
    private bool hasTalked = false;

    private void Start() {
        // Bật dấu chấm than khi game bắt đầu
        if (exclamationMark != null) exclamationMark.SetActive(true);
    }

    private void Update() {
        // Nếu Player ở gần, chưa nói chuyện và bấm phím E
        if (isPlayerInRange && !hasTalked && Input.GetKeyDown(KeyCode.E)) {
            GiveQuest();
        }
    }

    private void GiveQuest() {
        hasTalked = true;

        // Tắt dấu chấm than
        if (exclamationMark != null) exclamationMark.SetActive(false);

        Debug.Log("NPC: Lõi Dữ Liệu đang gặp nguy hiểm! Cậu hãy mau đến cổng dịch chuyển!");

        // Kích hoạt mũi tên chỉ đường tới cổng
        if (targetIndicator != null) {
            targetIndicator.ShowIndicator();
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            isPlayerInRange = true;
            // Bạn có thể bật một Text UI nhỏ "Bấm E để nói chuyện" ở đây
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.CompareTag("Player")) {
            isPlayerInRange = false;
        }
    }
}