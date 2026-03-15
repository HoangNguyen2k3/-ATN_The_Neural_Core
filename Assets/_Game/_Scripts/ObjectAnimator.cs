using UnityEngine;

public class ObjectAnimator : MonoBehaviour {
    public enum AnimationType {
        Rotate,
        UpDown,
        LeftRight,
        ForwardBackward,
        ScalePulse
    }

    [Header("Animation Type")]
    public AnimationType animationType;

    [Header("Speed")]
    public float speed = 2f;

    [Header("Movement Amount")]
    public float amount = 1f;

    [Header("Rotation Speed")]
    public Vector3 rotationAxis = new Vector3(0, 1, 0);

    private Vector3 startPos;
    private Vector3 startScale;

    void Start() {
        startPos = transform.localPosition;
        startScale = transform.localScale;
    }

    void Update() {
        float sin = Mathf.Sin(Time.time * speed) * amount;

        switch (animationType) {
            case AnimationType.Rotate:
                transform.Rotate(rotationAxis * speed * Time.deltaTime);
                break;

            case AnimationType.UpDown:
                transform.localPosition = startPos + Vector3.up * sin;
                break;

            case AnimationType.LeftRight:
                transform.localPosition = startPos + Vector3.right * sin;
                break;

            case AnimationType.ForwardBackward:
                transform.localPosition = startPos + Vector3.forward * sin;
                break;

            case AnimationType.ScalePulse:
                float scale = 1 + Mathf.Sin(Time.time * speed) * amount * 0.2f;
                transform.localScale = startScale * scale;
                break;
        }
    }
}