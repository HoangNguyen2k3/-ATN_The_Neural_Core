using UnityEngine;

public class Rotate : MonoBehaviour {
    public Vector3 rotateSpeed;
    public bool isRandomize = false;
    private void Start() {
        if (isRandomize)
            transform.Rotate(rotateSpeed.normalized * Random.Range(0, 360f));
    }
    private void Update() {
        transform.Rotate(rotateSpeed * Time.deltaTime, Space.Self);
    }
}
