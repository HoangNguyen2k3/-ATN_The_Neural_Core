using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Nút action mobile generic. Chọn loại trong Inspector → tự động ghi vào MobileInputBridge.
///
/// Held types (Sprint, Block): giữ = true, nhả = false.
/// One-shot types (Jump, Dodge, Ranged, Melee): nhấn 1 lần = true (auto-clear sau khi đọc).
/// Aircraft types: nhấn = set giá trị, nhả = reset về 0.
/// </summary>
public class MobileActionButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler
{
    public enum MobileAction {
        Jump, Sprint, Dodge, Block, Ranged, Melee,
        AircraftPitchUp, AircraftPitchDown,
        AircraftYawLeft, AircraftYawRight,
        AircraftBoost
    }

    public MobileAction actionType;

    public void OnPointerDown(PointerEventData eventData) {
        switch (actionType) {
            case MobileAction.Jump:   MobileInputBridge.JumpDown   = true; break;
            case MobileAction.Dodge:  MobileInputBridge.DodgeDown  = true; break;
            case MobileAction.Ranged: MobileInputBridge.RangedDown = true; break;
            case MobileAction.Melee:  MobileInputBridge.MeleeDown  = true; break;

            case MobileAction.Sprint: MobileInputBridge.SprintHeld = true; break;
            case MobileAction.Block:  MobileInputBridge.BlockHeld  = true; break;

            case MobileAction.AircraftPitchUp:   MobileInputBridge.AircraftPitch =  1f; break;
            case MobileAction.AircraftPitchDown: MobileInputBridge.AircraftPitch = -1f; break;
            case MobileAction.AircraftYawRight:  MobileInputBridge.AircraftYaw   =  1f; break;
            case MobileAction.AircraftYawLeft:   MobileInputBridge.AircraftYaw   = -1f; break;
            case MobileAction.AircraftBoost:     MobileInputBridge.AircraftBoost = true; break;
        }
    }

    public void OnPointerUp(PointerEventData eventData) {
        switch (actionType) {
            case MobileAction.Sprint: MobileInputBridge.SprintHeld = false; break;
            case MobileAction.Block:  MobileInputBridge.BlockHeld  = false; break;

            case MobileAction.AircraftPitchUp:
            case MobileAction.AircraftPitchDown: MobileInputBridge.AircraftPitch = 0f;  break;
            case MobileAction.AircraftYawRight:
            case MobileAction.AircraftYawLeft:   MobileInputBridge.AircraftYaw   = 0f;  break;
            case MobileAction.AircraftBoost:     MobileInputBridge.AircraftBoost = false; break;
        }
    }

    void OnDisable() {
        // Reset mọi held state khi button bị disable
        OnPointerUp(null);
    }
}
