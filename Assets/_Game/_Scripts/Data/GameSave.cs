using UnityEngine;
[System.Serializable]
public class GameSave {
    public bool isNew;
    [Space]
    public float soundVolume = 1;
    public float musicVolume = 1;
    public float vibrateAmount = 1;

    // Tiến trình tích lũy 4 viên đá (0.0 = chưa chơi, 1.0 = 100% mở khoá)
    // Index: 0=Island1_Snow, 1=Island1_Desert, 2=Island2_Main, 3=Island3_Main
    public float[] stonePercent = new float[4];

    // Vị trí player trong đảo chính lần cuối rời khỏi
    public bool hasHubPosition;
    public float hubPosX;
    public float hubPosY;
    public float hubPosZ;
    public float hubRotY;

    public GameSave() {
        soundVolume = 1;
        musicVolume = 0;
        vibrateAmount = 0;
        stonePercent = new float[4];
        hasHubPosition = false;
    }
}
