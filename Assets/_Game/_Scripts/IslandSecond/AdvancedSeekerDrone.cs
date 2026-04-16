using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AdvancedSeekerDrone : Agent {
    [Header("Cài đặt Di chuyển")]
    public float moveSpeed = 12f;
    public float turnSpeed = 250f;
    public bool allowBackward = false;

    [Header("Tham chiếu")]
    public MapManager mapManager;
    public Animator animator;

    private Rigidbody rb;
    private float lastWallHitTime = 0f;

    public override void Initialize() {
        rb = GetComponent<Rigidbody>();

        if (mapManager == null) {
            mapManager = Object.FindFirstObjectByType<MapManager>();
        }

        MaxStep = 0;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnEpisodeBegin() {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (mapManager != null && !mapManager.isResetting) {
            mapManager.ResetSingleDronePosition(this);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Vertical");
        continuousActionsOut[1] = Input.GetAxis("Horizontal");
    }

    public override void CollectObservations(VectorSensor sensor) {
        if (mapManager == null) return;

        // 1. Vận tốc cục bộ (3)
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));

        // 2. Heatmap lưới 3x3 (9)
        Vector2Int myCell = mapManager.WorldToGrid(transform.position);
        for (int x = -1; x <= 1; x++) {
            for (int z = -1; z <= 1; z++) {
                float cellLastVisitTime = mapManager.GetHeat(myCell.x + x, myCell.y + z);
                float timeSinceVisit = Time.time - cellLastVisitTime;
                sensor.AddObservation(Mathf.Clamp01(timeSinceVisit / 10f));
            }
        }

        // 3. Quan sát mục tiêu (6)
        GameObject targetObj = mapManager.GetNearestActivePlayer(transform.position);

        Vector3 toTargetWorld = Vector3.zero;
        float normalizedDistance = 0f;
        float facingDot = 0f;

        if (targetObj != null) {
            toTargetWorld = targetObj.transform.position - transform.position;
            toTargetWorld.y = 0f;

            float distance = toTargetWorld.magnitude;

            float maxMapDimension = Mathf.Max(mapManager.mapSize.x, mapManager.mapSize.y);
            normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(1f, maxMapDimension));

            if (distance > 0.001f) {
                facingDot = Vector3.Dot(transform.forward, toTargetWorld.normalized);
            }
        }

        Vector3 localTargetDir = transform.InverseTransformDirection(toTargetWorld);
        if (localTargetDir.sqrMagnitude > 0.001f) {
            localTargetDir.Normalize();
        }

        float horizontalSpeedRatio = Mathf.Clamp01(
            new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude / Mathf.Max(0.01f, moveSpeed)
        );

        sensor.AddObservation(localTargetDir);
        sensor.AddObservation(normalizedDistance);
        sensor.AddObservation(facingDot);
        sensor.AddObservation(horizontalSpeedRatio);
    }

    public override void OnActionReceived(ActionBuffers actions) {
        if (mapManager == null || rb == null) return;

        float moveInput = actions.ContinuousActions[0];
        float turnInput = actions.ContinuousActions[1];

        if (!allowBackward) {
            moveInput = (moveInput + 1f) * 0.5f;
        }

        Vector3 moveDir = transform.forward * moveInput * moveSpeed;
        rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z);
        transform.Rotate(Vector3.up, turnInput * turnSpeed * Time.fixedDeltaTime);

        if (animator != null) {
            float horizontalSpeed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
            animator.SetFloat("Speed", horizontalSpeed);
        }

        mapManager.ProcessHeatmapReward(this);

        // --- REWARD SHAPING: RẢI BÁNH MÌ VỤN ---
        GameObject targetObj = mapManager.GetNearestActivePlayer(transform.position);
        if (targetObj != null) {
            Vector3 toTarget = targetObj.transform.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 1f) {
                Vector3 dirToTarget = toTarget.normalized;
                float lookDot = Vector3.Dot(transform.forward, dirToTarget);

                // 1. Thưởng nhẹ nếu quay mặt về hướng mục tiêu (Góc nhìn < 45 độ)
                if (lookDot > 0.7f) {
                    AddReward(0.0005f);
                }

                // 2. Thưởng đậm hơn chút nếu đang thực sự di chuyển về hướng đó
                Vector3 currentVelocityXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (currentVelocityXZ.magnitude > 1f) {
                    float moveDot = Vector3.Dot(currentVelocityXZ.normalized, dirToTarget);
                    // Nếu hướng di chuyển gần như song song với hướng tới mục tiêu
                    if (moveDot > 0.8f) {
                        AddReward(0.001f);
                    }
                }
            }
        }
        // ---------------------------------------
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            mapManager.OnPlayerCaught(other.gameObject, this);
        }
    }

    private void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle")) {
            if (Time.time - lastWallHitTime > 0.5f) {
                AddReward(-0.02f);
                lastWallHitTime = Time.time;
            }
        }
    }
}