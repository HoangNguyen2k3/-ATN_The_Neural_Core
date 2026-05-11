using UnityEngine;

/// <summary>
/// Gắn lên Player trong StartScene (đảo chính).
/// Tự động lưu vị trí khi rời scene và khôi phục khi vào lại.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerPositionSaver : MonoBehaviour {
    private CharacterController _cc;

    void Awake() {
        _cc = GetComponent<CharacterController>();
    }

    void Start() {
        RestorePosition();
    }

    /// <summary> Khôi phục vị trí đã lưu từ DataManager. </summary>
    private void RestorePosition() {
        if (DataManager.Ins == null || !DataManager.Ins.isLoaded) return;

        Vector3 savedPos;
        float savedRotY;
        if (!DataManager.Ins.TryGetHubPosition(out savedPos, out savedRotY)) return;

        // Tắt CharacterController để đặt position mà không bị collider cản
        _cc.enabled = false;
        transform.position = savedPos;
        transform.rotation = Quaternion.Euler(0f, savedRotY, 0f);
        _cc.enabled = true;
    }

    /// <summary> Lưu vị trí hiện tại. </summary>
    private void SavePosition() {
        if (DataManager.Ins == null || !DataManager.Ins.isLoaded) return;
        DataManager.Ins.SaveHubPosition(transform.position, transform.eulerAngles.y);
    }

    // Lưu khi thoát game
    void OnApplicationQuit() => SavePosition();

    // Lưu khi object bị disable (scene unload hoặc portal transition)
    void OnDisable() => SavePosition();
}
