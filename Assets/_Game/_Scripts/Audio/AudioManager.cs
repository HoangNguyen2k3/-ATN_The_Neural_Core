using System.Collections;
using UnityEngine;

/// <summary>
/// AudioManager — Singleton DontDestroyOnLoad.
/// Quản lý nhạc nền và SFX cơ bản cho toàn game.
///
/// SETUP TRONG SCENE (GameManager GameObject — scene đầu tiên load):
/// 1. Gắn script này vào GameObject có sẵn (VD: cùng GameObject với GameManager).
/// 2. Thêm 2 AudioSource component, gán vào musicSource và sfxSource.
///    musicSource: Loop = true, Play On Awake = false.
///    sfxSource:   Loop = false, Play On Awake = false.
/// 3. Kéo AudioClip vào các slot trong Inspector.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Music Clips")]
    public AudioClip music;

    [Header("SFX — UI")]
    public AudioClip sfxButtonClick;

    [Header("SFX — Common")]
    public AudioClip sfxVictory;
    public AudioClip sfxDefeat;

    private const string KEY_MUSIC = "settings_music";
    private const string KEY_SFX   = "settings_sfx";

    private Coroutine _fadeCo;

    // ════════════════════════════════════════════════════════════════════
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Tự tạo AudioSource nếu chưa gán trong Inspector
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        // Áp dụng volume đã lưu
        musicSource.volume = PlayerPrefs.GetFloat(KEY_MUSIC, 0.7f);
        sfxSource.volume   = PlayerPrefs.GetFloat(KEY_SFX,   1.0f);
    }

    private void Start()
    {
        PlayMusic();
    }

    // ════════════════════════════════════════════════════════════════════
    #region Music

    /// <summary>Phát nhạc nền (clip duy nhất). Tự động crossfade nếu đang phát clip khác.</summary>
    public void PlayMusic(float fadeDuration = 1f)
    {
        if (music == null) return;
        if (musicSource.clip == music && musicSource.isPlaying) return;

        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(Crossfade(fadeDuration));
    }

    public void StopMusic(float fadeDuration = 0.5f)
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeOut(fadeDuration));
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════
    #region SFX

    public void PlayButtonClick() => PlaySFX(sfxButtonClick);
    public void PlayVictory()     => PlaySFX(sfxVictory);
    public void PlayDefeat()      => PlaySFX(sfxDefeat);

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════
    #region Volume (gọi từ SettingsManager)

    public void SetMusicVolume(float v)
    {
        v = Mathf.Clamp01(v);
        musicSource.volume = v;
        PlayerPrefs.SetFloat(KEY_MUSIC, v);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float v)
    {
        v = Mathf.Clamp01(v);
        sfxSource.volume = v;
        PlayerPrefs.SetFloat(KEY_SFX, v);
        PlayerPrefs.Save();
    }

    public float GetMusicVolume() => musicSource.volume;
    public float GetSFXVolume()   => sfxSource.volume;

    #endregion

    // ════════════════════════════════════════════════════════════════════
    #region Coroutines

    private IEnumerator Crossfade(float duration)
    {
        float savedVol = musicSource.volume;

        for (float t = 0; t < duration * 0.5f; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(savedVol, 0f, t / (duration * 0.5f));
            yield return null;
        }

        musicSource.clip = music;
        musicSource.Play();

        for (float t = 0; t < duration * 0.5f; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, savedVol, t / (duration * 0.5f));
            yield return null;
        }
        musicSource.volume = savedVol;
    }

    private IEnumerator FadeOut(float duration)
    {
        float savedVol = musicSource.volume;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(savedVol, 0f, t / duration);
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = savedVol;
    }

    #endregion
}
