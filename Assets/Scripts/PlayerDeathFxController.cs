using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Player death presentation: drops/tilts the FPS camera like the player collapses,
/// fades a red overlay, shows a pop-up message, and restarts the current scene with R.
/// Uses a built-in coroutine tween so it works even if DOTween is not installed.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDeathFxController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private AdvancedFpsController fpsController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Image redOverlay;
    [SerializeField] private RectTransform restartPopup;
    [SerializeField] private Text restartText;

    [Header("Camera Death Fall")]
    [SerializeField, Min(0.05f)] private float cameraFallDuration = 0.85f;
    [SerializeField] private Vector3 cameraFallLocalOffset = new Vector3(0.32f, -0.82f, 0.18f);
    [SerializeField] private Vector3 cameraFallLocalEulerOffset = new Vector3(72f, 0f, -28f);
    [SerializeField, Min(0f)] private float cameraImpactBounce = 0.08f;

    [Header("Red Death Overlay")]
    [SerializeField, Range(0f, 1f)] private float finalRedAlpha = 0.58f;
    [SerializeField, Min(0.05f)] private float redFadeDuration = 0.65f;
    [SerializeField] private Color deathRedColor = new Color(0.75f, 0f, 0f, 0.58f);

    [Header("Restart Popup")]
    [SerializeField] private string restartMessage = "ÖLDÜN\nR ile bölüme tekrar başlayabilirsin";
    [SerializeField, Min(0.01f)] private float popupDelay = 0.55f;
    [SerializeField, Min(0.05f)] private float popupTweenDuration = 0.42f;
    [SerializeField] private Vector2 popupSize = new Vector2(620f, 190f);
    [SerializeField] private KeyCode restartKey = KeyCode.R;

    private Vector3 cameraStartLocalPosition;
    private Quaternion cameraStartLocalRotation;
    private bool isDead;
    private Coroutine deathRoutine;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (fpsController == null)
        {
            fpsController = GetComponent<AdvancedFpsController>();
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera != null)
        {
            cameraStartLocalPosition = playerCamera.transform.localPosition;
            cameraStartLocalRotation = playerCamera.transform.localRotation;
        }

        EnsureUiExists();
        HideDeathUiInstantly();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDied.AddListener(HandleDied);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDied.RemoveListener(HandleDied);
        }
    }

    private void Update()
    {
        if (isDead && Input.GetKeyDown(restartKey))
        {
            RestartCurrentScene();
        }
    }

    private void HandleDied(DamageInfo damageInfo)
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        fpsController?.SetMovementLocked(true);

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
        }

        deathRoutine = StartCoroutine(DeathSequenceRoutine());
    }

    private IEnumerator DeathSequenceRoutine()
    {
        if (redOverlay != null)
        {
            redOverlay.gameObject.SetActive(true);
        }

        if (restartPopup != null)
        {
            restartPopup.gameObject.SetActive(false);
        }

        Coroutine cameraRoutine = StartCoroutine(CameraFallRoutine());
        Coroutine overlayRoutine = StartCoroutine(RedOverlayRoutine());

        yield return new WaitForSeconds(popupDelay);
        yield return StartCoroutine(PopupRoutine());

        if (cameraRoutine != null)
        {
            yield return cameraRoutine;
        }

        if (overlayRoutine != null)
        {
            yield return overlayRoutine;
        }
    }

    private IEnumerator CameraFallRoutine()
    {
        if (playerCamera == null)
        {
            yield break;
        }

        Transform cameraTransform = playerCamera.transform;
        Vector3 fromPosition = cameraTransform.localPosition;
        Quaternion fromRotation = cameraTransform.localRotation;
        Vector3 targetPosition = cameraStartLocalPosition + cameraFallLocalOffset;
        Quaternion targetRotation = cameraStartLocalRotation * Quaternion.Euler(cameraFallLocalEulerOffset);

        float elapsed = 0f;
        while (elapsed < cameraFallDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / cameraFallDuration));
            float bounce = Mathf.Sin(t * Mathf.PI) * cameraImpactBounce * (1f - t);

            cameraTransform.localPosition = Vector3.Lerp(fromPosition, targetPosition, t) + Vector3.up * bounce;
            cameraTransform.localRotation = Quaternion.Slerp(fromRotation, targetRotation, t);
            yield return null;
        }

        cameraTransform.localPosition = targetPosition;
        cameraTransform.localRotation = targetRotation;
    }

    private IEnumerator RedOverlayRoutine()
    {
        if (redOverlay == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Color start = deathRedColor;
        start.a = 0f;
        Color end = deathRedColor;
        end.a = finalRedAlpha;

        while (elapsed < redFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / redFadeDuration));
            redOverlay.color = Color.Lerp(start, end, t);
            yield return null;
        }

        redOverlay.color = end;
    }

    private IEnumerator PopupRoutine()
    {
        if (restartPopup == null)
        {
            yield break;
        }

        restartPopup.gameObject.SetActive(true);
        restartPopup.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < popupTweenDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popupTweenDuration);
            float overshoot = EaseOutBack(t);
            restartPopup.localScale = Vector3.one * overshoot;
            yield return null;
        }

        restartPopup.localScale = Vector3.one;
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
    }

    private void EnsureUiExists()
    {
        if (targetCanvas == null)
        {
            targetCanvas = FindObjectOfType<Canvas>();
        }

        if (targetCanvas == null)
        {
            GameObject canvasObject = new GameObject("Death FX Canvas");
            targetCanvas = canvasObject.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (redOverlay == null)
        {
            GameObject overlayObject = new GameObject("Death Red Overlay");
            overlayObject.transform.SetParent(targetCanvas.transform, false);
            redOverlay = overlayObject.AddComponent<Image>();
            redOverlay.raycastTarget = false;

            RectTransform overlayRect = redOverlay.rectTransform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
        }

        if (restartPopup == null)
        {
            GameObject popupObject = new GameObject("Death Restart Popup");
            popupObject.transform.SetParent(targetCanvas.transform, false);

            Image popupImage = popupObject.AddComponent<Image>();
            popupImage.color = new Color(0.03f, 0.03f, 0.035f, 0.88f);

            restartPopup = popupObject.GetComponent<RectTransform>();
            restartPopup.anchorMin = new Vector2(0.5f, 0.5f);
            restartPopup.anchorMax = new Vector2(0.5f, 0.5f);
            restartPopup.pivot = new Vector2(0.5f, 0.5f);
            restartPopup.anchoredPosition = Vector2.zero;
            restartPopup.sizeDelta = popupSize;

            GameObject textObject = new GameObject("Restart Text");
            textObject.transform.SetParent(popupObject.transform, false);
            restartText = textObject.AddComponent<Text>();
            restartText.text = restartMessage;
            restartText.alignment = TextAnchor.MiddleCenter;
            restartText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            restartText.fontSize = 32;
            restartText.fontStyle = FontStyle.Bold;
            restartText.color = Color.white;

            RectTransform textRect = restartText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 18f);
            textRect.offsetMax = new Vector2(-24f, -18f);
        }
        else if (restartText != null)
        {
            restartText.text = restartMessage;
        }
    }

    private void HideDeathUiInstantly()
    {
        if (redOverlay != null)
        {
            Color hidden = deathRedColor;
            hidden.a = 0f;
            redOverlay.color = hidden;
            redOverlay.gameObject.SetActive(false);
        }

        if (restartPopup != null)
        {
            restartPopup.localScale = Vector3.zero;
            restartPopup.gameObject.SetActive(false);
        }
    }

    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}