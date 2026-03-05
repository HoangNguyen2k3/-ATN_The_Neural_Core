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
            //Pitch: 1==up, 0==none,-1==down
            float pitchValue = Mathf.Round(pitchInput.ReadValue<float>());
            //Yaw: 1==turn right,0==none,-1==turn left
            float yawValue = Mathf.Round(yawInput.ReadValue<float>());
            //Boost: 1==boost,0==no boost
            float boostValue = Mathf.Round((boostInput.ReadValue<float>()));

            //convert
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
