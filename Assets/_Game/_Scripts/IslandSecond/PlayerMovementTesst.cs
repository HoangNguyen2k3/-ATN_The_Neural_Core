using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMovementTesst : MonoBehaviour {
    public enum ControlMode {
        WanderAI,
        PlayerManual
    }

    [Header("Control Mode")]
    public ControlMode controlMode = ControlMode.WanderAI;

    [Header("Manual Movement (MainIsland-like)")]
    public CharacterController controller;
    public Transform cam;
    public Animator animator;
    public float moveSpeed = 6f;
    public float turnSmoothTime = 0.1f;
    public float gravity = -19.62f;
    public float jumpHeight = 1.5f;
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Wander AI")]
    public float wanderRadius = 10f;
    public float wanderTimer = 3f;
    public float minMoveDistance = 1f;

    private NavMeshAgent agent;
    private float timer;
    private float turnSmoothVelocity;
    private Vector3 velocity;
    private bool isGrounded;

    void OnEnable() {
        agent = GetComponent<NavMeshAgent>();
        timer = wanderTimer;

        if (controlMode == ControlMode.PlayerManual && agent != null) {
            agent.enabled = false;
        }
        else if (agent != null) {
            agent.enabled = true;
        }

        if (controlMode == ControlMode.PlayerManual && controller == null) {
            controller = GetComponent<CharacterController>();
        }
    }

    void Update() {
        if (controlMode == ControlMode.PlayerManual) {
            HandleManualMovement();
            return;
        }

        HandleWanderAI();
    }

    private void HandleManualMovement() {
        if (controller == null || cam == null) return;

        if (agent != null && agent.enabled) {
            agent.enabled = false;
        }

        if (groundCheck != null) {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        }
        else {
            isGrounded = controller.isGrounded;
        }

        if (isGrounded && velocity.y < 0f) {
            velocity.y = -2f;
        }

        if (animator != null) animator.SetBool("IsGrounded", isGrounded);

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        if (direction.magnitude >= 0.1f) {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);

            if (animator != null) animator.SetBool("IsMoving", true);
        }
        else {
            if (animator != null) animator.SetBool("IsMoving", false);
        }

        if (Input.GetButtonDown("Jump") && isGrounded) {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleWanderAI() {
        if (agent == null) return;

        if (!agent.enabled) {
            agent.enabled = true;
        }

        if (!agent.isOnNavMesh) return; // FIX LỖI NAVMESH: Chờ Agent khởi tạo và bắt được NavMesh xong mới chạy

        timer += Time.deltaTime;

        // Nếu đã đến giờ đi dạo, hoặc đã đi đến đích
        if (timer >= wanderTimer || !agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid) {
            Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, NavMesh.AllAreas);
            if (Vector3.Distance(transform.position, newPos) >= minMoveDistance) {
                agent.SetDestination(newPos);
            }
            timer = 0;
        }
    }

    // Tìm điểm ngẫu nhiên an toàn trên NavMesh
    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask) {
        Vector3 randDirection = Random.insideUnitSphere * dist;
        randDirection += origin;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(randDirection, out navHit, dist, layermask)) {
            return navHit.position;
        }
        return origin;
    }
}
