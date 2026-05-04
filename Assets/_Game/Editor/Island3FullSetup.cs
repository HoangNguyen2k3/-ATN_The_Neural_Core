using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public class Island3FullSetup
{
    [MenuItem("Tools/Island3/Clean and Rebuild Scene")]
    static void CleanAndRebuild()
    {
        // Delete all children from canvases to avoid duplicates
        CleanCanvasChildren("Canvas_HUD");
        CleanCanvasChildren("Canvas_Win");
        CleanCanvasChildren("Canvas_Lose");
        
        // Delete duplicate EventSystems
        var allES = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        for (int i = 1; i < allES.Length; i++) Object.DestroyImmediate(allES[i].gameObject);
        if (allES.Length > 0) Object.DestroyImmediate(allES[0].gameObject);

        // Now run full setup
        BuildFullSceneUI();
    }

    static void CleanCanvasChildren(string name)
    {
        // Find including inactive
        var all = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (var c in all)
        {
            if (c.gameObject.name == name && c.gameObject.scene.isLoaded)
            {
                c.gameObject.SetActive(true);
                var t = c.transform;
                for (int i = t.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(t.GetChild(i).gameObject);
                break;
            }
        }
    }

    [MenuItem("Tools/Island3/Build Full Scene UI")]
    static void BuildFullSceneUI()
    {
        SetupLayerMasks();
        SetupArenaManagerReferences();
        SetupAllCanvases();
        LinkUIToManager();

        var player = GameObject.Find("Player");
        if (player != null)
        {
            var cap = player.GetComponent<CapsuleCollider>();
            if (cap != null) Object.DestroyImmediate(cap);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Island3FullSetup] Scene setup complete!");
    }

    static void SetupLayerMasks()
    {
        var boss = GameObject.Find("Boss");
        var player = GameObject.Find("Player");
        if (boss == null || player == null) return;

        var se = boss.GetComponent<BossSkillExecutor>();
        if (se != null) { se.laserHitMask = 1 << 9; EditorUtility.SetDirty(se); }

        var pc = player.GetComponent<PlayerCombatController>();
        if (pc != null) { pc.attackHitMask = 1 << 8; EditorUtility.SetDirty(pc); }
    }

    static void SetupArenaManagerReferences()
    {
        var mgr = Object.FindFirstObjectByType<BossArenaManager>();
        var boss = GameObject.Find("Boss");
        var player = GameObject.Find("Player");
        if (mgr == null || boss == null || player == null) return;

        mgr.isTrainingMode = false;
        mgr.bossHealth = boss.GetComponent<BossHealthSystem>();
        mgr.playerHealth = player.GetComponent<BossHealthSystem>();
        mgr.bossAgent = boss.GetComponent<AdaptiveBossAgent>();
        mgr.playerAnalyzer = player.GetComponent<PlayerCombatAnalyzer>();

        var sp = GameObject.Find("BossSpawnPoint");
        var pp = GameObject.Find("PlayerSpawnPoint");
        if (sp != null) mgr.bossSpawnPoint = sp.transform;
        if (pp != null) mgr.playerSpawnPoint = pp.transform;

        EditorUtility.SetDirty(mgr);
    }

    static void SetupAllCanvases()
    {
        // Configure all canvases (keep all active for now so Find works)
        ConfigureCanvas("Canvas_HUD", 0);
        ConfigureCanvas("Canvas_Win", 10);
        ConfigureCanvas("Canvas_Lose", 10);

        // Populate UI elements while all canvases are active
        PopulateHUD();
        PopulateWinPanel();
        PopulateLosePanel();

        // NOW deactivate Win/Lose panels (HUD stays active)
        var winGo = GameObject.Find("Canvas_Win");
        if (winGo != null) winGo.SetActive(false);
        var loseGo = GameObject.Find("Canvas_Lose");
        if (loseGo != null) loseGo.SetActive(false);

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    static void ConfigureCanvas(string name, int sortOrder)
    {
        var go = GameObject.Find(name);
        if (go == null) return;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        go.layer = LayerMask.NameToLayer("UI");
        EditorUtility.SetDirty(go);
    }

    static void PopulateHUD()
    {
        var canvas = GameObject.Find("Canvas_HUD");
        if (canvas == null) return;
        var parent = canvas.transform;

        var bossHPBg = MakeImage(parent, "BossHP_Background",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -40), new Vector2(500, 30),
            new Color(0.15f, 0.15f, 0.15f, 0.8f));

        var bossHP = MakeImage(bossHPBg.transform, "BossHP_Fill",
            Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero,
            new Color(0.85f, 0.15f, 0.15f, 1f));
        bossHP.type = Image.Type.Filled;
        bossHP.fillMethod = Image.FillMethod.Horizontal;
        bossHP.fillAmount = 1f;
        var brt = bossHP.GetComponent<RectTransform>();
        brt.offsetMin = new Vector2(3, 3);
        brt.offsetMax = new Vector2(-3, -3);

        MakeText(parent, "BossNameText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -15), new Vector2(500, 30),
            "ADAPTIVE BOSS", 16, Color.white, TextAlignmentOptions.Center);

        MakeText(parent, "PhaseText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(300, -40), new Vector2(150, 30),
            "PHASE 1", 18, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Left);

        var playerHPBg = MakeImage(parent, "PlayerHP_Background",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(30, 50), new Vector2(350, 25),
            new Color(0.15f, 0.15f, 0.15f, 0.8f));

        var playerHP = MakeImage(playerHPBg.transform, "PlayerHP_Fill",
            Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero,
            new Color(0.2f, 0.8f, 0.3f, 1f));
        playerHP.type = Image.Type.Filled;
        playerHP.fillMethod = Image.FillMethod.Horizontal;
        playerHP.fillAmount = 1f;
        var prt = playerHP.GetComponent<RectTransform>();
        prt.offsetMin = new Vector2(3, 3);
        prt.offsetMax = new Vector2(-3, -3);

        MakeText(parent, "PlayerNameText",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(30, 75), new Vector2(350, 25),
            "PLAYER", 14, Color.white, TextAlignmentOptions.Left);

        MakeText(parent, "TimerText",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-30, -30), new Vector2(120, 40),
            "05:00", 28, Color.white, TextAlignmentOptions.Right);

        MakeText(parent, "ControlsHint",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-20, 30), new Vector2(280, 80),
            "LMB: Shoot | RMB: Melee\nSpace: Dodge | Shift: Block",
            12, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Right);
    }

    static void PopulateWinPanel()
    {
        var canvas = GameObject.Find("Canvas_Win");
        if (canvas == null) return;
        var parent = canvas.transform;

        MakeImage(parent, "Overlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.7f));

        MakeText(parent, "WinTitle",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 80), new Vector2(500, 80),
            "VICTORY!", 52, new Color(1f, 0.85f, 0.1f), TextAlignmentOptions.Center);

        MakeText(parent, "WinSubtitle",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 20), new Vector2(500, 40),
            "The Neural Core is yours", 20, Color.white, TextAlignmentOptions.Center);

        MakeButton(parent, "RetryButton",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-100, -60), new Vector2(160, 50),
            "RETRY", new Color(0.2f, 0.6f, 0.9f));

        MakeButton(parent, "MenuButton",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(100, -60), new Vector2(160, 50),
            "MENU", new Color(0.5f, 0.5f, 0.5f));
    }

    static void PopulateLosePanel()
    {
        var canvas = GameObject.Find("Canvas_Lose");
        if (canvas == null) return;
        var parent = canvas.transform;

        MakeImage(parent, "Overlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.7f));

        MakeText(parent, "LoseTitle",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 80), new Vector2(500, 80),
            "DEFEATED", 52, new Color(0.85f, 0.15f, 0.15f), TextAlignmentOptions.Center);

        MakeText(parent, "LoseSubtitle",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 20), new Vector2(500, 40),
            "The Boss has adapted to you", 20, Color.white, TextAlignmentOptions.Center);

        MakeButton(parent, "RetryButton",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-100, -60), new Vector2(160, 50),
            "RETRY", new Color(0.9f, 0.5f, 0.1f));

        MakeButton(parent, "MenuButton",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(100, -60), new Vector2(160, 50),
            "MENU", new Color(0.5f, 0.5f, 0.5f));
    }

    static void LinkUIToManager()
    {
        var mgr = Object.FindFirstObjectByType<BossArenaManager>();
        if (mgr == null) return;

        mgr.hudPanel = GameObject.Find("Canvas_HUD");
        mgr.winPanel = GameObject.Find("Canvas_Win");
        mgr.losePanel = GameObject.Find("Canvas_Lose");

        var bossHPFill = GameObject.Find("BossHP_Fill");
        if (bossHPFill != null) mgr.bossHPBar = bossHPFill.GetComponent<Image>();

        var playerHPFill = GameObject.Find("PlayerHP_Fill");
        if (playerHPFill != null) mgr.playerHPBar = playerHPFill.GetComponent<Image>();

        var timerText = GameObject.Find("TimerText");
        if (timerText != null) mgr.timerText = timerText.GetComponent<TextMeshProUGUI>();

        var phaseText = GameObject.Find("PhaseText");
        if (phaseText != null) mgr.phaseText = phaseText.GetComponent<TextMeshProUGUI>();

        var winCanvas = GameObject.Find("Canvas_Win");
        if (winCanvas != null)
        {
            var wr = winCanvas.transform.Find("RetryButton");
            if (wr != null) mgr.winRetryButton = wr.GetComponent<Button>();
            var wm = winCanvas.transform.Find("MenuButton");
            if (wm != null) mgr.winMenuButton = wm.GetComponent<Button>();
        }

        var loseCanvas = GameObject.Find("Canvas_Lose");
        if (loseCanvas != null)
        {
            var lr = loseCanvas.transform.Find("RetryButton");
            if (lr != null) mgr.loseRetryButton = lr.GetComponent<Button>();
            var lm = loseCanvas.transform.Find("MenuButton");
            if (lm != null) mgr.loseMenuButton = lm.GetComponent<Button>();
        }

        EditorUtility.SetDirty(mgr);
    }

    static Image MakeImage(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size,
        string text, int fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.color = color; tmp.alignment = align;
        return tmp;
    }

    static Button MakeButton(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size,
        string label, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = bgColor;

        var txtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        txtGo.layer = LayerMask.NameToLayer("UI");
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 22; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return go.GetComponent<Button>();
    }
}
