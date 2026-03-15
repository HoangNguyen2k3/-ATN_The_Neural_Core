using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI_Loading : MonoBehaviour {
    public enum LoadingType {
        SceneLoading,
        FakeLoading
    }

    [Header("Mode")]
    public LoadingType loadingType;

    [Header("UI")]
    public Image loadingFill;
    public TextMeshProUGUI textProcess;
    public TextMeshProUGUI textLoading;

    [Header("Scene Loading")]
    public string sceneName;
    public float loadingTime = 2f;
    public float loadingEndTime = 1f;
    public float progressLimitAmount = 0.85f;

    [Header("Fake Loading")]
    public float fakeLoadingTime = 2f;
    public float textAnimSpeed = 0.4f;

    private AsyncOperation asyncOperation;

    private void Start() {
        if (loadingType == LoadingType.SceneLoading)
            StartCoroutine(SceneLoadingRoutine());
    }
    IEnumerator SceneLoadingRoutine() {
        float time = 0;
        loadingFill.fillAmount = 0;

        while (time < loadingTime) {
            time += Time.deltaTime;

            float progress = Mathf.Lerp(0, progressLimitAmount, time / loadingTime);
            loadingFill.fillAmount = progress;

            if (textProcess != null)
                textProcess.text = "Loading " + Mathf.RoundToInt(progress * 100) + "%";

            yield return null;
        }

        asyncOperation = SceneManager.LoadSceneAsync(sceneName);
        asyncOperation.allowSceneActivation = false;

        while (asyncOperation.progress < 0.9f) {
            float progress = Mathf.Lerp(progressLimitAmount, 0.95f, asyncOperation.progress / 0.9f);
            loadingFill.fillAmount = progress;

            if (textProcess != null)
                textProcess.text = "Loading " + Mathf.RoundToInt(progress * 100) + "%";

            yield return null;
        }

        time = 0;

        while (time < loadingEndTime) {
            time += Time.deltaTime;

            float progress = Mathf.Lerp(loadingFill.fillAmount, 1f, time / loadingEndTime);
            loadingFill.fillAmount = progress;

            if (textProcess != null)
                textProcess.text = "Loading " + Mathf.RoundToInt(progress * 100) + "%";

            yield return null;
        }

        if (textProcess != null)
            textProcess.text = "Loading 100%";

        asyncOperation.allowSceneActivation = true;
    }
    IEnumerator FakeLoadingRoutine() {
        int dotCount = 0;
        float timer = 0;

        asyncOperation = SceneManager.LoadSceneAsync(sceneName);
        asyncOperation.allowSceneActivation = false;

        while (asyncOperation.progress < 0.9f || timer < fakeLoadingTime) {
            timer += textAnimSpeed;

            dotCount = (dotCount + 1) % 4;
            textLoading.text = "Loading" + new string('.', dotCount);

            yield return new WaitForSeconds(textAnimSpeed);
        }
        asyncOperation.allowSceneActivation = true;
        yield return new WaitForSeconds(1);
        gameObject.SetActive(false);
    }
    public void ShowFakeLoading(float duration, string nameScene) {
        sceneName = nameScene;
        fakeLoadingTime = duration;
        gameObject.SetActive(true);
        StartCoroutine(FakeLoadingRoutine());
    }
}