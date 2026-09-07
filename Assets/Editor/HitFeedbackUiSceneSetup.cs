using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class HitFeedbackUiSceneSetup
{
    private const string FontAssetPath = "Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset";
    private const string NormalHitClipPath = "Assets/Audio/HitFeedback/hit_normal.wav";
    private const string CriticalHitClipPath = "Assets/Audio/HitFeedback/hit_critical.wav";
    private const string KillClipPath = "Assets/Audio/HitFeedback/hit_kill.wav";
    private const string WeaponFireClipPath = "Assets/Audio/Weapons/rifle_fire_assault_01.wav";
    private const string MusicLoopPath = "Assets/Audio/Music/battle_fun_chiptune_loop.wav";
    private const string FootstepClipPath = "Assets/Audio/Player/footstep_soft_01.wav";
    private const string FreefallWindLoopPath = "Assets/Audio/Player/freefall_wind_loop.wav";
    private const string ParachuteOpenClipPath = "Assets/Audio/Player/parachute_open.wav";
    private const string GrenadeExplosionClipPath = "Assets/Audio/Weapons/grenade_explosion_01.wav";
    private static readonly string[] TargetScenes =
    {
        "Assets/Demo.unity",
        "Assets/PolygonBattleRoyale/Scenes/Demo.unity"
    };

    [MenuItem("Tools/Setup Hit Feedback UI")]
    public static void SetupCurrentScene()
    {
        SetupScene(SceneManager.GetActiveScene());
    }

    [InitializeOnLoadMethod]
    private static void AutoSetupActiveSceneAfterScriptsReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
            {
                return;
            }

            if (Object.FindAnyObjectByType<HitFeedbackUi>() != null)
            {
                return;
            }

            SetupScene(activeScene);
        };
    }

    public static void SetupAllDemoScenes()
    {
        foreach (string scenePath in TargetScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SetupScene(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void SetupScene(Scene scene)
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        GameObject feedbackObject = FindOrCreateChild(canvas.transform, "Hit Feedback UI", typeof(RectTransform), typeof(HitFeedbackUi), typeof(AudioSource));
        RectTransform feedbackRect = feedbackObject.GetComponent<RectTransform>();
        StretchToParent(feedbackRect);

        AudioSource audioSource = feedbackObject.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        AudioClip normalHitClip = AssetDatabase.LoadAssetAtPath<AudioClip>(NormalHitClipPath);
        AudioClip criticalHitClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CriticalHitClipPath);
        AudioClip killClip = AssetDatabase.LoadAssetAtPath<AudioClip>(KillClipPath);
        AudioClip musicLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicLoopPath);
        AudioClip footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FootstepClipPath);
        AudioClip freefallWindLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(FreefallWindLoopPath);
        AudioClip parachuteOpenClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ParachuteOpenClipPath);
        AudioClip grenadeExplosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GrenadeExplosionClipPath);

        GameObject musicObject = FindOrCreateChild(canvas.transform, "Gameplay Music", typeof(AudioSource));
        Component musicController = EnsureRuntimeComponent(musicObject, "GameplayMusicController");
        AudioSource musicSource = musicObject.GetComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0.18f;
        musicSource.clip = musicLoop;
        if (musicController != null)
        {
            SerializedObject musicSerializedObject = new SerializedObject(musicController);
            musicSerializedObject.FindProperty("musicLoop").objectReferenceValue = musicLoop;
            musicSerializedObject.FindProperty("volume").floatValue = 0.18f;
            musicSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject markerRootObject = FindOrCreateChild(feedbackRect, "Hit Marker Root", typeof(RectTransform));
        RectTransform markerRoot = markerRootObject.GetComponent<RectTransform>();
        CenterRect(markerRoot, Vector2.zero, new Vector2(100f, 100f));
        markerRootObject.SetActive(false);

        ConfigureMarkerLine(markerRoot, "Top", new Vector2(4f, 18f), new Vector2(0f, 23f));
        ConfigureMarkerLine(markerRoot, "Bottom", new Vector2(4f, 18f), new Vector2(0f, -23f));
        ConfigureMarkerLine(markerRoot, "Left", new Vector2(18f, 4f), new Vector2(-23f, 0f));
        ConfigureMarkerLine(markerRoot, "Right", new Vector2(18f, 4f), new Vector2(23f, 0f));

        GameObject numberContainerObject = FindOrCreateChild(feedbackRect, "Damage Numbers", typeof(RectTransform));
        RectTransform numberContainer = numberContainerObject.GetComponent<RectTransform>();
        CenterRect(numberContainer, Vector2.zero, new Vector2(420f, 220f));

        TextMeshProUGUI damageNumberTemplate = ConfigureTmpText(
            numberContainer,
            "Damage Number Template",
            string.Empty,
            28f,
            Color.white,
            fontAsset,
            new Vector2(0f, 52f),
            new Vector2(180f, 64f)
        );
        damageNumberTemplate.gameObject.SetActive(false);

        TextMeshProUGUI killText = ConfigureTmpText(
            feedbackRect,
            "Kill Text",
            "KILL",
            34f,
            new Color(1f, 0.1f, 0.05f, 1f),
            fontAsset,
            new Vector2(0f, 96f),
            new Vector2(260f, 60f)
        );
        killText.gameObject.SetActive(false);

        HitFeedbackUi feedbackUi = feedbackObject.GetComponent<HitFeedbackUi>();
        SerializedObject feedbackSerializedObject = new SerializedObject(feedbackUi);
        feedbackSerializedObject.FindProperty("hitMarkerRoot").objectReferenceValue = markerRoot;
        feedbackSerializedObject.FindProperty("damageNumberPrefab").objectReferenceValue = damageNumberTemplate;
        feedbackSerializedObject.FindProperty("killText").objectReferenceValue = killText;
        feedbackSerializedObject.FindProperty("audioSource").objectReferenceValue = audioSource;
        feedbackSerializedObject.FindProperty("normalHitClip").objectReferenceValue = normalHitClip;
        feedbackSerializedObject.FindProperty("criticalHitClip").objectReferenceValue = criticalHitClip;
        feedbackSerializedObject.FindProperty("killClip").objectReferenceValue = killClip;
        feedbackSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        FpsWeaponController weaponController = Object.FindAnyObjectByType<FpsWeaponController>();
        if (weaponController != null)
        {
            AudioSource weaponAudioSource = weaponController.GetComponent<AudioSource>();
            if (weaponAudioSource == null)
            {
                weaponAudioSource = weaponController.gameObject.AddComponent<AudioSource>();
            }

            weaponAudioSource.playOnAwake = false;
            weaponAudioSource.loop = false;
            weaponAudioSource.spatialBlend = 0.45f;

            AudioClip weaponFireClip = AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponFireClipPath);

            SerializedObject weaponSerializedObject = new SerializedObject(weaponController);
            weaponSerializedObject.FindProperty("hitFeedbackUi").objectReferenceValue = feedbackUi;
            weaponSerializedObject.FindProperty("fireAudioSource").objectReferenceValue = weaponAudioSource;
            weaponSerializedObject.FindProperty("fireClip").objectReferenceValue = weaponFireClip;
            weaponSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        AdvancedFpsController fpsController = Object.FindAnyObjectByType<AdvancedFpsController>();
        if (fpsController != null)
        {
            AudioSource footstepSource = GetOrAddChildAudioSource(fpsController.transform, "Footstep Audio Source", 0f);
            SerializedObject fpsSerializedObject = new SerializedObject(fpsController);
            fpsSerializedObject.FindProperty("footstepAudioSource").objectReferenceValue = footstepSource;
            fpsSerializedObject.FindProperty("footstepClip").objectReferenceValue = footstepClip;
            fpsSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerParachuteDrop parachuteDrop = Object.FindAnyObjectByType<PlayerParachuteDrop>();
        if (parachuteDrop != null)
        {
            AudioSource windSource = GetOrAddChildAudioSource(parachuteDrop.transform, "Freefall Wind Audio Source", 0f);
            AudioSource parachuteSource = GetOrAddChildAudioSource(parachuteDrop.transform, "Parachute Audio Source", 0f);
            SerializedObject parachuteSerializedObject = new SerializedObject(parachuteDrop);
            parachuteSerializedObject.FindProperty("windAudioSource").objectReferenceValue = windSource;
            parachuteSerializedObject.FindProperty("parachuteAudioSource").objectReferenceValue = parachuteSource;
            parachuteSerializedObject.FindProperty("freefallWindLoop").objectReferenceValue = freefallWindLoop;
            parachuteSerializedObject.FindProperty("parachuteOpenClip").objectReferenceValue = parachuteOpenClip;
            parachuteSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerGrenadeThrower grenadeThrower = Object.FindAnyObjectByType<PlayerGrenadeThrower>();
        if (grenadeThrower != null)
        {
            SerializedObject grenadeThrowerSerializedObject = new SerializedObject(grenadeThrower);
            grenadeThrowerSerializedObject.FindProperty("explosionClip").objectReferenceValue = grenadeExplosionClip;
            grenadeThrowerSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        GrenadeProjectile grenadeProjectile = Object.FindAnyObjectByType<GrenadeProjectile>();
        if (grenadeProjectile != null)
        {
            SerializedObject grenadeSerializedObject = new SerializedObject(grenadeProjectile);
            grenadeSerializedObject.FindProperty("explosionClip").objectReferenceValue = grenadeExplosionClip;
            grenadeSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Hit Feedback UI setup completed for scene: {scene.path}");
    }

    private static GameObject FindOrCreateChild(Transform parent, string childName, params System.Type[] components)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            EnsureComponents(existing.gameObject, components);
            return existing.gameObject;
        }

        GameObject child = new GameObject(childName, components);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void EnsureComponents(GameObject target, params System.Type[] components)
    {
        for (int i = 0; i < components.Length; i++)
        {
            if (target.GetComponent(components[i]) == null)
            {
                target.AddComponent(components[i]);
            }
        }
    }

    private static Component EnsureRuntimeComponent(GameObject target, string typeName)
    {
        System.Type type = System.Type.GetType($"{typeName}, Assembly-CSharp");
        if (type == null)
        {
            Debug.LogWarning($"Could not find runtime component type: {typeName}");
            return null;
        }

        Component component = target.GetComponent(type);
        if (component == null)
        {
            component = target.AddComponent(type);
        }

        return component;
    }

    private static void ConfigureMarkerLine(RectTransform parent, string lineName, Vector2 size, Vector2 position)
    {
        GameObject line = FindOrCreateChild(parent, lineName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = line.GetComponent<RectTransform>();
        CenterRect(rect, position, size);
        Image image = line.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static AudioSource GetOrAddChildAudioSource(Transform parent, string childName, float spatialBlend)
    {
        GameObject audioObject = FindOrCreateChild(parent, childName, typeof(AudioSource));
        AudioSource source = audioObject.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = spatialBlend;
        return source;
    }

    private static TextMeshProUGUI ConfigureTmpText(RectTransform parent, string objectName, string text, float fontSize, Color color, TMP_FontAsset fontAsset, Vector2 position, Vector2 size)
    {
        GameObject textObject = FindOrCreateChild(parent, objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        CenterRect(rect, position, size);

        TextMeshProUGUI tmpText = textObject.GetComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.font = fontAsset;
        tmpText.fontSize = fontSize;
        tmpText.fontStyle = FontStyles.Bold;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = color;
        tmpText.raycastTarget = false;
        tmpText.outlineColor = Color.black;
        tmpText.outlineWidth = 0.25f;
        return tmpText;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void CenterRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }
}