using UnityEngine;

[CreateAssetMenu(fileName = "gameConfigData", menuName = "Data/Game Config Data")]
public class GameDataConfig : ScriptableObject {
    public PortalDatabaseSO databaseSO; // Nguồn chứa text
    public NPCDialogueDatabaseSO npcDialogueDB;   // NGUỒN CHỨA DATA CỦA NPC (Mới thêm)
}
