using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerMovementTesst : MonoBehaviour {
    public float wanderRadius = 10f;
    public float wanderTimer = 3f;
    public float minMoveDistance = 1f;

    private NavMeshAgent agent;
    private float timer;

    void OnEnable() {
        agent = GetComponent<NavMeshAgent>();
        timer = wanderTimer;
    }

    void Update() {
        if (agent == null) return;
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
