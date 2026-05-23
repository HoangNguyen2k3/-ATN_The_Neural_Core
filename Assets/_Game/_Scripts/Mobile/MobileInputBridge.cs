using UnityEngine;

/// <summary>
/// Lớp trung gian giữa mobile UI (joystick, buttons) và các controller.
/// Static class — không cần MonoBehaviour, không cần tìm instance.
///
/// UI components ghi vào đây. Controllers đọc từ đây với fallback tự động sang keyboard/mouse.
/// </summary>
public static class MobileInputBridge {

    // ─── Movement ────────────────────────────────────────────────────
    /// <summary> Vector di chuyển từ VirtualJoystick. Vector2.zero khi ngón tay nhả. </summary>
    public static Vector2 MoveInput;

    // ─── Camera ──────────────────────────────────────────────────────
    /// <summary> Delta xoay camera từ RightTouchLook. Vector2.zero khi không chạm. </summary>
    public static Vector2 CameraLookDelta;

    // ─── Held buttons (true khi đang giữ, false khi nhả) ────────────
    public static bool SprintHeld;
    public static bool BlockHeld;

    // ─── Aircraft-specific ───────────────────────────────────────────
    /// <summary> -1 / 0 / 1. Set bởi PitchDown/PitchUp buttons. </summary>
    public static float AircraftPitch;
    /// <summary> -1 / 0 / 1. Set bởi YawLeft/YawRight buttons. </summary>
    public static float AircraftYaw;
    public static bool AircraftBoost;

    // ─── One-shot buttons (auto-clear sau khi đọc 1 lần) ────────────
    private static bool _jumpDown;
    private static bool _dodgeDown;
    private static bool _rangedDown;
    private static bool _meleeDown;

    public static bool JumpDown {
        get { var v = _jumpDown;   _jumpDown   = false; return v; }
        set { _jumpDown   = value; }
    }
    public static bool DodgeDown {
        get { var v = _dodgeDown;  _dodgeDown  = false; return v; }
        set { _dodgeDown  = value; }
    }
    public static bool RangedDown {
        get { var v = _rangedDown; _rangedDown = false; return v; }
        set { _rangedDown = value; }
    }
    public static bool MeleeDown {
        get { var v = _meleeDown;  _meleeDown  = false; return v; }
        set { _meleeDown  = value; }
    }

    // ─── Helpers ─────────────────────────────────────────────────────
    /// <summary> True khi có joystick input (dùng để phân biệt mobile vs keyboard). </summary>
    public static bool HasMoveInput => MoveInput.sqrMagnitude > 0.01f;

    /// <summary> True khi có camera touch input. </summary>
    public static bool HasCameraInput => CameraLookDelta.sqrMagnitude > 0.01f;
}
