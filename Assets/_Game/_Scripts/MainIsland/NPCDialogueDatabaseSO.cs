using System.Collections.Generic;
using UnityEngine;

// 1. Định nghĩa cấu trúc dữ liệu cho MỘT cuộc hội thoại (Ví dụ: Trước khi mở Cổng 1)
[System.Serializable]
public class DialogueStage {
    public string stageName; // Tên gợi nhớ trên Inspector (VD: "Pre-Island 1", "Pre-Island 2")

    [TextArea(2, 5)]
    public string[] dialogueLines; // Các câu thoại trong giai đoạn này (Kịch bản tiếng Anh ta vừa làm)
}

// 2. Tạo Scriptable Object để chứa TOÀN BỘ các cuộc hội thoại
[CreateAssetMenu(fileName = "npcDialogueDatabase", menuName = "Data/NPC Dialogue Database")]
public class NPCDialogueDatabaseSO : ScriptableObject {
    public List<DialogueStage> allStages; // Danh sách các kịch bản theo thứ tự
}