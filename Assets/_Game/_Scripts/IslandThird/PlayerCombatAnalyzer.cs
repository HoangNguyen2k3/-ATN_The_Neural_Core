using UnityEngine;

/// <summary>
/// Gắn lên Human Player trong Đảo 3.
/// Đo 3 chỉ số thói quen chiến đấu real-time rồi truyền cho Boss AI đọc.
/// Dùng Sliding Window (cửa sổ trượt) 5 giây gần nhất để tính.
/// </summary>
public class PlayerCombatAnalyzer : MonoBehaviour {
    // ─── Kết quả đo (Boss AI sẽ đọc 3 giá trị này) ──────────────
    [Header("📊 Kết Quả Phân Tích (Chỉ đọc)")]
    [Range(0f, 1f)] public float aggressionScore;   // Độ hung hãn (hay bắn/chém?)
    [Range(0f, 1f)] public float agilityScore;       // Độ nhanh nhẹn (hay dodge/đổi hướng?)
    [Range(0f, 1f)] public float preferredRange;     // Khoảng cách ưa thích (xa=1, gần=0)

    [Tooltip("Bật = DummyBot đang gán thẳng scores. CalculateScores() sẽ bị bỏ qua.")]
    public bool isExternalControl = false;  // Set true bởi DummyPlayerBot trong training

    // ─── Cấu hình ───────────────────────────────────────────────
    [Header("⚙️ Cấu hình")]
    [Tooltip("Thời gian cửa sổ trượt (giây). Mặc định 5s = phân tích 5 giây gần nhất")]
    public float windowDuration = 5f;
    [Tooltip("Bán kính tối đa đấu trường để chuẩn hóa khoảng cách")]
    public float arenaRadius = 20f;

    // ─── Tham chiếu ─────────────────────────────────────────────
    [Header("🔗 Tham chiếu")]
    [Tooltip("Transform của Boss. Gán trong Inspector hoặc tự tìm")]
    public Transform bossTransform;

    // ─── Biến nội bộ ────────────────────────────────────────────
    private int _attackFrames;      // Số frame người chơi tấn công
    private int _dodgeFrames;       // Số frame người chơi dodge/đổi hướng
    private int _totalFrames;       // Tổng frame đã đo
    private float _distanceSum;     // Tổng khoảng cách tích lũy

    private float _windowTimer;     // Bộ đếm cửa sổ trượt
    private Vector3 _lastMoveDir;   // Hướng di chuyển frame trước (để phát hiện đổi hướng)
    private bool _isAttacking;      // Flag: Player có đang giữ nút tấn công không

    // ─── Ngưỡng phát hiện ───────────────────────────────────────
    private const float DirectionChangeThreshold = 0.5f; // Cos(60°) — góc đổi hướng > 60° = dodge

    // ════════════════════════════════════════════════════════════════
    void Start() {
        _windowTimer = windowDuration;
        _lastMoveDir = transform.forward;

        // Auto-find Boss nếu chưa gán (tìm trong cùng Arena)
        if (bossTransform == null) {
            var boss = transform.parent.GetComponentInChildren<AdaptiveBossAgent>();
            if (boss != null) bossTransform = boss.transform;
        }
    }

    void Update() {
        if (bossTransform == null) return;

        _totalFrames++;

        // ═══ 1. ĐO ĐỘ HUNG HÃN (Aggression) ═══
        // Đếm frame Player đang giữ nút tấn công (Click trái hoặc Click phải)
        _isAttacking = Input.GetMouseButton(0) || Input.GetMouseButton(1);
        if (_isAttacking) {
            _attackFrames++;
        }

        // ═══ 2. ĐO ĐỘ NHANH NHẸN (Agility) ═══
        // Phát hiện đổi hướng đột ngột hoặc nhấn Dodge (Space khi đang chạy)
        Vector3 currentMoveDir = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        );

        if (currentMoveDir.sqrMagnitude > 0.01f) {
            currentMoveDir.Normalize();

            // Nếu góc giữa hướng mới và hướng cũ > 60° → tính là dodge/đổi hướng
            float dot = Vector3.Dot(currentMoveDir, _lastMoveDir);
            if (dot < DirectionChangeThreshold) {
                _dodgeFrames++;
            }

            _lastMoveDir = currentMoveDir;
        }

        // Nhấn Space (Dash/Dodge) cũng tính
        if (Input.GetKeyDown(KeyCode.Space)) {
            _dodgeFrames += 5; // Dodge chủ động quan trọng hơn, đếm 5 frame
        }

        // ═══ 3. ĐO KHOẢNG CÁCH ƯA THÍCH (Preferred Range) ═══
        float distanceToBoss = Vector3.Distance(transform.position, bossTransform.position);
        _distanceSum += distanceToBoss;

        // ═══ CẬP NHẬT CỬA SỔ TRƯỢT ═══
        _windowTimer -= Time.deltaTime;
        if (_windowTimer <= 0f) {
            if (!isExternalControl) CalculateScores(); // Skip nếu DummyBot đang control
            ResetWindow();
        }
    }

    // ════════════════════════════════════════════════════════════════
    /// <summary>
    /// Tính toán 3 chỉ số từ dữ liệu window hiện tại.
    /// </summary>
    void CalculateScores() {
        if (_totalFrames <= 0) return;

        // Aggression: Tỉ lệ frame tấn công / tổng frame
        aggressionScore = Mathf.Clamp01((float)_attackFrames / _totalFrames);

        // Agility: Tỉ lệ frame dodge / tổng frame (cap ở 1.0)
        agilityScore = Mathf.Clamp01((float)_dodgeFrames / _totalFrames);

        // Range: Khoảng cách trung bình / bán kính đấu trường
        float avgDistance = _distanceSum / _totalFrames;
        preferredRange = Mathf.Clamp01(avgDistance / arenaRadius);
    }

    /// <summary>
    /// Reset bộ đếm cho cửa sổ trượt mới.
    /// </summary>
    void ResetWindow() {
        _attackFrames = 0;
        _dodgeFrames = 0;
        _totalFrames = 0;
        _distanceSum = 0f;
        _windowTimer = windowDuration;
    }

    // ════════════════════════════════════════════════════════════════
    // API cho các script khác gọi khi Player tấn công (để đếm chính xác hơn)
    // Gọi từ script bắn súng/chém gươm của Player nếu có

    /// <summary> Gọi khi Player bắn/chém 1 phát </summary>
    public void RegisterAttack() {
        _attackFrames += 3; // 1 phát bắn = 3 frame tấn công
    }

    /// <summary> Gọi khi Player dodge/dash </summary>
    public void RegisterDodge() {
        _dodgeFrames += 5;
    }
}
