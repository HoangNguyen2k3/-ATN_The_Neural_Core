using UnityEngine;

/// <summary>
/// Đặt vào Exit Zone (trigger collider).
/// Khi Human Player vào → thông báo thắng.
/// Khi AI Companion vào → không làm gì (chỉ drone mới chặn được).
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour {
    [Header("Visual Feedback")]
    [Tooltip("ParticleSystem chạy liên tục tại Exit (gán trong Inspector)")]
    public ParticleSystem exitParticle;

    [Tooltip("Tốc độ pulse của ánh sáng/scale")]
    public float pulseSpeed = 2f;

    [Tooltip("Biên độ scale pulse")]
    public float pulseAmount = 0.1f;

    private Vector3 _baseScale;
    private Collider _col;

    void Awake() {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
        _baseScale = transform.localScale;
    }

    void Start() {
        if (exitParticle != null && !exitParticle.isPlaying) {
            exitParticle.Play();
        }
    }

    void Update() {
        // Pulse animation để Exit dễ nhìn thấy
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = _baseScale * pulse;
    }

    void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            // Kiểm tra xem ai vừa bước vào bằng Component
            if (other.GetComponent<HumanPlayerController>() != null) {
                Debug.Log("[ExitZone] Human Player đã thoát!");
                // ReSharper disable once Unity.NoNullPropagation
                IslandGameManager.Instance?.OnPlayerReachedExit();
            } else {
                // AI Companion vào Exit → biến mất / deactivate
                other.gameObject.SetActive(false);
            }
        }
    }

    void OnDrawGizmos() {
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.35f);
        Gizmos.DrawCube(transform.position, transform.lossyScale);
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}
