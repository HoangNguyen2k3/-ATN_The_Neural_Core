using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Aircraft {
    public class AircraftPlayer : AircraftAgent {
        [Header("Input Binding")]
        public InputAction pitchInput;
        public InputAction yawInput;
        public InputAction boostInput;
        public InputAction pauseInput;
        public override void Initialize() {
            base.Initialize();
            pitchInput.Enable();
            yawInput.Enable();
            boostInput.Enable();
            pauseInput.Enable();
        }
        /// <summary>
        /// Read player input and convert to action array
        /// </summary>
        /// <param name="actionsOut">An array of floats for agentAction to use</param>
        public override void Heuristic(in ActionBuffers actionsOut) {
            // Mobile bridge override (nút màn hình) có ưu tiên cao hơn InputAction
            float pitchValue = MobileInputBridge.AircraftPitch != 0f
                ? MobileInputBridge.AircraftPitch
                : Mathf.Round(pitchInput.ReadValue<float>());
            float yawValue = MobileInputBridge.AircraftYaw != 0f
                ? MobileInputBridge.AircraftYaw
                : Mathf.Round(yawInput.ReadValue<float>());
            float boostValue = MobileInputBridge.AircraftBoost
                ? 1f
                : Mathf.Round(boostInput.ReadValue<float>());

            if (pitchValue == -1f) pitchValue = 2f;
            if (yawValue == -1f) yawValue = 2f;
            var continuous = actionsOut.DiscreteActions;
            continuous[0] = (int)pitchValue;
            continuous[1] = (int)yawValue;
            continuous[2] = (int)boostValue;
        }
        public void OnDestroy() {
            pitchInput.Disable();
            yawInput.Disable();
            boostInput.Disable();
            pauseInput.Disable();
        }
    }
}
