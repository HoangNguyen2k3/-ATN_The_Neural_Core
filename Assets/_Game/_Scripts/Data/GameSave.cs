using UnityEngine;
[System.Serializable]
public class GameSave {
    public bool isNew;
    [Space]
    public float soundVolume = 1;
    public float musicVolume = 1;
    public float vibrateAmount = 1;
    public float[] stonePercent = new float[4];

    public GameSave() {
        soundVolume = 1;
        musicVolume = 0;
        vibrateAmount = 0;
        stonePercent = new float[4]; // tất cả 0.0
    }
}