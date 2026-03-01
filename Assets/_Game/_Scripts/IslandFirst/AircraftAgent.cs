using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;
namespace Aircraft {
    public class AircraftAgent : Agent {
        [Header("Movement Param")]
        public float thrust = 1000f; //Di chuyển lên trước 
        public float pitchSpeed = 100f; // Xoay y
        public float yawSpeed = 100f; //Xoay x
        public float rollSpeed = 100f; //Xoay z
        public float boostMultiplier = 2f; //boost để tăng tốc độ di chuyển
        public int NextCheckpointIndex { get; set; }
        //Component keep track of
        private AircraftArea area;
        new private Rigidbody rigid;
        private TrailRenderer trailRenderer;
        //Control
        private float pitchChange = 0f;
        private float smoothPitchChange = 0f;
        private float maxPitchAngle = 45f;
        private float yawChange = 0f;
        private float smoothYawChange = 0f;
        private float rollChange = 0f;
        private float smoothRollChange = 0f;
        private float maxRollAngle = 45f;
        private bool boost;
        //Call when agent is first init
        public override void Initialize() {
            area = GetComponentInParent<AircraftArea>();
            trailRenderer = GetComponent<TrailRenderer>();
            rigid = GetComponentInParent<Rigidbody>();
        }
        public override void OnActionReceived(ActionBuffers actions) {
            pitchChange = actions.ContinuousActions[0];//up or none
            if (pitchChange == 2) {
                pitchChange = -1f;//down
            }
            yawChange = actions.ContinuousActions[1];
            if (yawChange == 2) {
                yawChange = -1f;//left
            }
            boost = actions.ContinuousActions[2] == 1;
            if (boost && !trailRenderer.emitting) trailRenderer.Clear();
            trailRenderer.emitting = boost;
            ProcessMovement();
        }
        private void ProcessMovement() {
            //Calculate boost
            float boostModifier = boost ? boostMultiplier : 1f;
            //Apply forward thrust
            rigid.AddForce(transform.forward * thrust * boostModifier, ForceMode.Force);
            //Get the current rotation
            Vector3 curRot = transform.rotation.eulerAngles;
            float rollAngle = curRot.z > 180f ? curRot.z - 360f : curRot.z;
            if (yawChange == 0f) {
                rollChange = -rollAngle / maxRollAngle;
            }
            else {
                rollChange = -yawChange;
            }
            //Calculate smooth deltas
            smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
            smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);
            smoothRollChange = Mathf.MoveTowards(smoothRollChange, rollChange, 2f * Time.fixedDeltaTime);
            //Calculate new pitch, yaw, roll. Claim pitch and roll
            float pitch = curRot.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
            if (pitch > 180f) pitch -= 360f;
            pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);
            float yaw = curRot.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;
            float roll = curRot.z + smoothRollChange * Time.fixedDeltaTime * rollSpeed;
            if (roll > 180f) roll -= 360f;
            roll = Mathf.Clamp(roll, -maxRollAngle, maxRollAngle);

            //set the new rotation
            transform.rotation = Quaternion.Euler(pitch, yaw, roll);
        }
    }
}
