using UnityEngine;

public class CheckDrop : MonoBehaviour {
    public Vector3 posStartPlayer;

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            other.gameObject.transform.position = posStartPlayer;
        }
    }
}
