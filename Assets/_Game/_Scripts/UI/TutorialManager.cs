using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý Tutorial Popup dạng slide-show.
/// UI thiết kế hoàn toàn trong scene (Canvas).
///
/// CÁCH SETUP TRONG SCENE:
/// 1. Tạo GameObject "TutorialPanel" trên Canvas, thêm component CanvasGroup.
/// 2. Bên trong tạo các child "Slide_01", "Slide_02"... Mỗi slide = 1 trang.
///    Trong mỗi Slide: đặt Image lớn (hình minh hoạ) + TMP_Text caption ngắn.
/// 3. Gắn script này vào "TutorialPanel", rồi kéo slides/buttons/dots vào Inspector.
/// 4. Nút "?" gọi TutorialManager.Instance.ShowTutorial() hoặc dùng OnClick UnityEvent.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    // ─── Inspector Fields ──────────────────────────────────────────────────
    [Header("Slides — kéo theo đúng thứ tự")]
    public List<GameObject> slides = new List<GameObject>();

    [Header("Navigation Buttons")]
    public Button btnNext;
    public Button btnPrev;
    public Button btnClose;

    [Header("Page Dots (Image array)")]
    public Image[] pageDots;
    public Color dotActiveColor   = new Color(1f, 0.85f, 0f);      // Vàng neon
    public Color dotInactiveColor = new Color(1f, 1f, 1f, 0.3f);   // Trắng mờ

    [Header("Page Counter Text (tuỳ chọn — để trống nếu không dùng)")]
    public TMP_Text pageCountText;

    [Header("Animation")]
    [Range(0f, 0.5f)]
    public float fadeDuration = 0.2f;

    // ─── Runtime ──────────────────────────────────────────────────────────
    private int          _slideIndex = 0;
    private bool         _isBusy     = false;
    private CanvasGroup  _canvasGroup;

    // ════════════════════════════════════════════════════════════════════════
    private void Awake()
    {
        // Singleton cục bộ — mỗi scene có 1 TutorialManager riêng
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // CanvasGroup để fade
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        // Gắn event buttons
        if (btnNext  != null) btnNext.onClick.AddListener(NextSlide);
        if (btnPrev  != null) btnPrev.onClick.AddListener(PrevSlide);
        if (btnClose != null) btnClose.onClick.AddListener(CloseTutorial);

        // Ẩn panel lúc khởi động
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ════════════════════════════════════════════════════════════════════════
    #region Public API

    /// <summary>Mở Tutorial từ slide đầu tiên. Gọi từ nút "?".</summary>
    public void ShowTutorial()
    {
        if (slides == null || slides.Count == 0)
        {
            Debug.LogWarning("[TutorialManager] Danh sách slides rỗng!", this);
            return;
        }

        gameObject.SetActive(true);
        _slideIndex = 0;
        DisplaySlide(false);
        StartCoroutine(DoFade(0f, 1f));
    }

    /// <summary>Đóng Tutorial. Gọi từ nút "Đóng / Đã hiểu".</summary>
    public void CloseTutorial()
    {
        StartCoroutine(CloseCoroutine());
    }

    /// <summary>Sang slide tiếp theo.</summary>
    public void NextSlide()
    {
        if (_isBusy) return;
        if (_slideIndex >= slides.Count - 1) return;
        _slideIndex++;
        DisplaySlide(true);
    }

    /// <summary>Quay về slide trước.</summary>
    public void PrevSlide()
    {
        if (_isBusy) return;
        if (_slideIndex <= 0) return;
        _slideIndex--;
        DisplaySlide(true);
    }

    /// <summary>Nhảy tới slide bất kỳ (dùng khi click vào page dot).</summary>
    public void JumpToSlide(int index)
    {
        if (_isBusy) return;
        if (index < 0 || index >= slides.Count) return;
        _slideIndex = index;
        DisplaySlide(true);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Display Logic

    private void DisplaySlide(bool withFlash)
    {
        // Ẩn tất cả, hiện đúng slide hiện tại
        for (int i = 0; i < slides.Count; i++)
        {
            if (slides[i] != null)
                slides[i].SetActive(i == _slideIndex);
        }

        // Cập nhật trạng thái nút điều hướng
        if (btnPrev != null) btnPrev.interactable = (_slideIndex > 0);
        if (btnNext != null) btnNext.interactable = (_slideIndex < slides.Count - 1);

        // Đổi label nút Đóng khi đến slide cuối
        UpdateCloseButtonLabel();

        // Cập nhật page dots
        UpdatePageDots();

        // Cập nhật page counter text
        UpdatePageCountText();

        // Flash nhẹ khi chuyển slide
        if (withFlash && fadeDuration > 0f)
            StartCoroutine(DoFlash());
    }

    private void UpdateCloseButtonLabel()
    {
        if (btnClose == null) return;
        var label = btnClose.GetComponentInChildren<TMP_Text>();
        if (label == null) return;
        label.text = (_slideIndex == slides.Count - 1) ? "✓  Đã hiểu!" : "✕  Đóng";
    }

    private void UpdatePageDots()
    {
        if (pageDots == null) return;
        for (int i = 0; i < pageDots.Length; i++)
        {
            if (pageDots[i] == null) continue;
            bool isActive = (i == _slideIndex);
            pageDots[i].color = isActive ? dotActiveColor : dotInactiveColor;
            pageDots[i].rectTransform.localScale = isActive
                ? new Vector3(1.35f, 1.35f, 1f)
                : Vector3.one;
        }
    }

    private void UpdatePageCountText()
    {
        if (pageCountText == null) return;
        // Dùng SetText để tránh GC allocation
        pageCountText.SetText("{0} / {1}", _slideIndex + 1, slides.Count);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Coroutines

    private IEnumerator DoFlash()
    {
        _isBusy = true;
        float half = fadeDuration * 0.5f;

        // Fade xuống
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            _canvasGroup.alpha = Mathf.Lerp(1f, 0.5f, t / half);
            yield return null;
        }
        // Fade lên
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            _canvasGroup.alpha = Mathf.Lerp(0.5f, 1f, t / half);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
        _isBusy = false;
    }

    private IEnumerator DoFade(float from, float to)
    {
        _canvasGroup.alpha = from;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            _canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        _canvasGroup.alpha = to;
    }

    private IEnumerator CloseCoroutine()
    {
        yield return StartCoroutine(DoFade(1f, 0f));
        gameObject.SetActive(false);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════
    #region Keyboard Support (test trong Editor)

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.RightArrow)) NextSlide();
        if (Input.GetKeyDown(KeyCode.LeftArrow))  PrevSlide();
        if (Input.GetKeyDown(KeyCode.Escape))     CloseTutorial();
    }

    #endregion
}
