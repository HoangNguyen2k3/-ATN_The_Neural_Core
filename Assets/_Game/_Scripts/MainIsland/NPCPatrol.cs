using UnityEngine;
using UnityEngine.AI;

public class NPCPatrol : MonoBehaviour {
    [Header("Cài đặt Tuần tra")]
    public Transform[] waypoints;
    public Animator animator;

    [Header("Cài đặt Chờ & Xoay")]
    public float waitTime = 2f;
    public float rotateSpeed = 45f;

    private int currentPoint = 0;
    private NavMeshAgent agent;

    private bool isWaiting = false;
    private float waitTimer = 0f;

    void Start() {
        ShuffleWaypoints();
        agent = GetComponent<NavMeshAgent>();
        if (waypoints.Length > 0) {
            agent.SetDestination(waypoints[currentPoint].position);
        }
    }

    void Update() {
        if (animator != null) {
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }

        if (isWaiting) {
            waitTimer -= Time.deltaTime;

            transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);

            if (waitTimer <= 0f) {
                isWaiting = false;
                agent.isStopped = false;

                currentPoint = (currentPoint + 1) % waypoints.Length;
                agent.SetDestination(waypoints[currentPoint].position);
            }
        }
        else {
            if (!agent.pathPending && agent.remainingDistance < 0.5f) {
                isWaiting = true;
                waitTimer = waitTime;
                agent.isStopped = true;
            }
        }
    }
    public void ShuffleWaypoints() {
        for (int i = waypoints.Length - 1; i > 0; i--) {
            int randomIndex = Random.Range(0, i + 1);

            Transform temp = waypoints[i];
            waypoints[i] = waypoints[randomIndex];
            waypoints[randomIndex] = temp;
        }
    }
}