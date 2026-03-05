using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
namespace Aircraft {
    public class AircraftAgent : Agent {
        [Header("Movement Param")]
        public float thrust = 1000f; //Di chuyển lên trước 
        public float pitchSpeed = 100f; // Xoay y
        public float yawSpeed = 100f; //Xoay x
        public float rollSpeed = 100f; //Xoay z
        public float boostMultiplier = 2f; //boost để tăng tốc độ di chuyển

        [Header("Explosion Stuff")]
        [Tooltip("The aircraft mesh that will disappear on explosion")]
        public GameObject meshObj;

        [Tooltip("Game object of the explosion particle effect")]
        public GameObject explosionEffect;

        [Header("Training")]
        [Tooltip("Number of steps to time out after in training")]
        public int stepTimeout = 300;
        public int NextCheckpointIndex { get; set; }
        //Component keep track of
        private AircraftArea area;
        new private Rigidbody rigid;
        private TrailRenderer trailRenderer;

        //when the next step timeout will be during training
        private float nextStepTimeout;
        //whether the aircraft is frozen (intentionally not flying)
        private bool frozen = false;
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
            //override max step 0 is infinite
            MaxStep = area.isTrainingMode ? 5000 : 0;

        }
        /// <summary>
        /// Called when a new episode begin
        /// </summary>
        public override void OnEpisodeBegin() {
            //Reset something
            rigid.angularVelocity = Vector3.zero;
            rigid.linearVelocity = Vector3.zero;
            trailRenderer.emitting = false;
            area.ResetAgentPosition(this, area.isTrainingMode);

            if (area.isTrainingMode) { nextStepTimeout = StepCount + stepTimeout; }
        }
        public override void OnActionReceived(ActionBuffers actions) {

            if (frozen) return;

            pitchChange = actions.DiscreteActions[0];//up or none
            if (pitchChange == 2) {
                pitchChange = -1f;//down
            }
            yawChange = actions.DiscreteActions[1];
            if (yawChange == 2) {
                yawChange = -1f;//left
            }
            boost = actions.DiscreteActions[2] == 1;
            if (boost && !trailRenderer.emitting) trailRenderer.Clear();
            trailRenderer.emitting = boost;
            ProcessMovement();
            if (area.isTrainingMode) {
                //small negative reward step
                AddReward(-1f / MaxStep);
                //make sure we not run out of time
                if (StepCount > nextStepTimeout) {
                    AddReward(-0.5f);
                    EndEpisode();
                }
                Vector3 localCheckpointDir = VectorToNextCheckPoint();
                if (localCheckpointDir.magnitude < Academy.Instance.EnvironmentParameters.GetWithDefault("checkpoint_radius", 0f)) {
                    GotCheckpoint();
                }

            }
        }
        public override void CollectObservations(VectorSensor sensor) {
            //Observe aircraft velocity (1 vector3 = 3 values)
            sensor.AddObservation(transform.InverseTransformDirection(rigid.linearVelocity));
            //Where is the next check point
            sensor.AddObservation(VectorToNextCheckPoint());
            //orientation of the next checkpoint
            Vector3 nextCheckpointForward = area.list_checkpoint[NextCheckpointIndex].transform.forward;
            sensor.AddObservation(transform.InverseTransformDirection(nextCheckpointForward));
            //Total obs = 3+3+3 =9
        }
        public override void Heuristic(in ActionBuffers actionsOut) {
            Debug.LogError("Make sure behavior Type is Heuristic Only");
        }
        //Stop Agent
        public void FreezeAgent() {
            Debug.Assert(area.isTrainingMode == false, "Freeze/Thaw not support");
            frozen = true;
            rigid.Sleep();
            trailRenderer.emitting = false;
        }
        //Resume Agent
        public void ThawAgent() {
            Debug.Assert(area.isTrainingMode == false, "Freeze/Thaw not support");
            frozen = false;
            rigid.WakeUp();
        }
        /// <summary>
        /// Get a vector to the next checkpoint the agent needs to fly through
        /// </summary>
        /// <returns>A local-space vector</returns>
        private Vector3 VectorToNextCheckPoint() {
            Vector3 nextCheckPointDir = area.list_checkpoint[NextCheckpointIndex].transform.position - transform.position;
            Vector3 localCheckPointDir = transform.InverseTransformDirection(nextCheckPointDir);
            return localCheckPointDir;
        }
        /// <summary>
        /// Call when agent through checkpoint
        /// </summary>
        private void GotCheckpoint() {
            NextCheckpointIndex = (NextCheckpointIndex + 1) % area.list_checkpoint.Count;
            if (area.isTrainingMode) {
                AddReward(.5f);
                nextStepTimeout = StepCount + stepTimeout;
            }

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
        private void OnTriggerEnter(Collider other) {
            if (other.transform.CompareTag("checkpoint") &&
                other.gameObject == area.list_checkpoint[NextCheckpointIndex]) {
                GotCheckpoint();
            }
        }
        private void OnCollisionEnter(Collision collision) {
            if (!collision.transform.CompareTag("agent")) {
                if (area.isTrainingMode) {
                    AddReward(-1f);
                    EndEpisode();
                }
                else {
                    StartCoroutine(ExplosionReset());
                }
            }
        }
        private IEnumerator ExplosionReset() {
            FreezeAgent();

            meshObj.gameObject.SetActive(false);
            explosionEffect.gameObject.SetActive(true);

            yield return new WaitForSeconds(2f);

            meshObj.gameObject.SetActive(true);
            explosionEffect.gameObject.SetActive(false);

            area.ResetAgentPosition(this);
            yield return new WaitForSeconds(1f);
            ThawAgent();
        }
    }
}
