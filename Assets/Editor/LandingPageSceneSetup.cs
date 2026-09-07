using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class LandingPageSceneSetup
{
    [MenuItem("Tools/Setup Landing Page Scene")]
    public static void Execute()
    {
        GameObject player = GameObject.Find("GameObject");
        AdvancedFpsController playerController = player != null ? player.GetComponent<AdvancedFpsController>() : null;
        PlayerParachuteDrop parachute = player != null ? player.GetComponent<PlayerParachuteDrop>() : null;
        Camera playerCamera = player != null ? player.GetComponentInChildren<Camera>(true) : null;

        if (parachute != null)
        {
            SerializedObject parachuteSo = new SerializedObject(parachute);
            SerializedProperty beginDropOnStart = parachuteSo.FindProperty("beginDropOnStart");
            SerializedProperty startUiToHide = parachuteSo.FindProperty("startUiToHide");
            if (beginDropOnStart != null)
            {
                beginDropOnStart.boolValue = false;
            }
            if (startUiToHide != null)
            {
                startUiToHide.objectReferenceValue = null;
            }
            parachuteSo.ApplyModifiedProperties();
        }

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject manager = GameObject.Find("Landing Page Manager") ?? new GameObject("Landing Page Manager");
        LandingPageController landing = manager.GetComponent<LandingPageController>() ?? manager.AddComponent<LandingPageController>();

        GameObject menuCameraObject = GameObject.Find("Menu Camera") ?? new GameObject("Menu Camera");
        Camera menuCamera = menuCameraObject.GetComponent<Camera>() ?? menuCameraObject.AddComponent<Camera>();
        menuCameraObject.transform.position = new Vector3(-1.8f, 155f, -65.4f);
        menuCameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        menuCamera.fieldOfView = 48f;
        menuCamera.clearFlags = CameraClearFlags.Skybox;

        GameObject gameplayHud = GameObject.Find("Canvas/Gameplay HUD Root") ?? new GameObject("Gameplay HUD Root");
        gameplayHud.transform.SetParent(canvas.transform, false);
        MoveIfExists("Canvas/ammocontainer", gameplayHud.transform);
        MoveIfExists("Canvas/healthcontainer", gameplayHud.transform);

        GameObject oldStartDrop = GameObject.Find("Canvas/Start Drop Button");
        if (oldStartDrop != null)
        {
            Object.DestroyImmediate(oldStartDrop);
        }

        GameObject panel = GameObject.Find("Canvas/Landing Page Panel") ?? new GameObject("Landing Page Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;
        Image panelImage = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.35f);
        panelImage.raycastTarget = false;

        Text title = CreateText(panel.transform, "Game Title", "AI EL WAR", 64, new Color(1f, 0.92f, 0.72f, 1f));
        SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(700f, 110f), new Vector2(0f, 170f));

        Button startButton = CreateButton(panel.transform, "Start Game Button", "START", new Color(0.08f, 0.45f, 0.18f, 0.92f));
        SetRect(startButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(260f, 70f), new Vector2(0f, 35f));
        startButton.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(startButton.onClick, landing.StartGame);

        Button exitButton = CreateButton(panel.transform, "Exit Game Button", "EXIT GAME", new Color(0.45f, 0.08f, 0.08f, 0.92f));
        SetRect(exitButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(260f, 70f), new Vector2(0f, -55f));
        exitButton.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(exitButton.onClick, landing.ExitGame);

        GameObject dotObject = GameObject.Find("Canvas/Landing Page Panel/Menu Cursor Dot") ?? new GameObject("Menu Cursor Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dotObject.transform.SetParent(panel.transform, false);
        RectTransform dotRect = dotObject.GetComponent<RectTransform>();
        SetRect(dotRect, Vector2.zero, new Vector2(12f, 12f), Vector2.zero);
        Image dotImage = dotObject.GetComponent<Image>() ?? dotObject.AddComponent<Image>();
        dotImage.color = Color.white;
        dotImage.raycastTarget = false;
        dotObject.transform.SetAsLastSibling();

        SerializedObject landingSo = new SerializedObject(landing);
        landingSo.FindProperty("menuCamera").objectReferenceValue = menuCamera;
        landingSo.FindProperty("playerCamera").objectReferenceValue = playerCamera;
        landingSo.FindProperty("playerController").objectReferenceValue = playerController;
        landingSo.FindProperty("parachuteDrop").objectReferenceValue = parachute;
        landingSo.FindProperty("landingPageRoot").objectReferenceValue = panel;
        landingSo.FindProperty("gameplayHudRoot").objectReferenceValue = gameplayHud;
        landingSo.FindProperty("menuCursorDot").objectReferenceValue = dotRect;
        landingSo.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(canvas.gameObject);
        EditorUtility.SetDirty(menuCameraObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("Landing page scene setup completed.");
    }

    private static void MoveIfExists(string path, Transform newParent)
    {
        GameObject found = GameObject.Find(path);
        if (found != null)
        {
            found.transform.SetParent(newParent, false);
        }
    }

    private static Button CreateButton(Transform parent, string name, string label, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        Button button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        button.targetGraphic = image;
        Text text = CreateText(go.transform, "Text", label, label == "START" ? 34 : 30, Color.white);
        SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        return button;
    }

    private static Text CreateText(Transform parent, string name, string value, int size, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>() ?? go.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position, bool stretch = false)
    {
        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}