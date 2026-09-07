using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Lightweight FPS hit feedback UI: center hit marker, damage numbers, critical and kill feedback.
/// Expects its UI references to exist in the scene hierarchy.
/// </summary>
public class HitFeedbackUi : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform hitMarkerRoot;
    [SerializeField] private TMP_Text damageNumberPrefab;
    [SerializeField] private TMP_Text killText;

    [Header("Hit Marker")]
    [SerializeField] private Color normalHitColor = Color.white;
    [SerializeField] private Color criticalHitColor = new Color(1f, 0.85f, 0.05f, 1f);
    [SerializeField] private Color killHitColor = new Color(1f, 0.1f, 0.05f, 1f);
    [SerializeField, Min(0.02f)] private float hitMarkerDuration = 0.16f;
    [SerializeField, Min(1f)] private float normalHitScale = 1f;
    [SerializeField, Min(1f)] private float criticalHitScale = 1.25f;
    [SerializeField, Min(1f)] private float killHitScale = 1.45f;

    [Header("Damage Numbers")]
    [SerializeField] private Vector2 damageNumberStartOffset = new Vector2(0f, 52f);
    [SerializeField] private Vector2 damageNumberDrift = new Vector2(0f, 46f);
    [SerializeField, Min(0.05f)] private float damageNumberDuration = 0.65f;
    [SerializeField] private int normalDamageFontSize = 28;
    [SerializeField] private int criticalDamageFontSize = 36;

    [Header("Kill Text")]
    [SerializeField] private string killMessage = "KILL";
    [SerializeField, Min(0.05f)] private float killTextDuration = 0.55f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip normalHitClip;
    [SerializeField] private AudioClip criticalHitClip;
    [SerializeField] private AudioClip killClip;
    [SerializeField, Range(0f, 1f)] private float normalHitVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float criticalHitVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float killVolume = 0.8f;

    private Image[] markerImages;
    private Coroutine markerRoutine;
    private Coroutine killRoutine;
    private bool referencesReady;

    private void Awake()
    {
        CacheReferences();
        HideInstantly();
    }

    public void ShowHit(float damageAmount, bool isCritical, bool isKill)
    {
        ShowHit(damageAmount, isCritical, isKill, Vector3.zero);
    }

    public void ShowHit(float damageAmount, bool isCritical, bool isKill, Vector3 worldPosition)
    {
        if (!referencesReady)
        {
            CacheReferences();
        }

        if (!referencesReady)
        {
            return;
        }

        Color color = isKill ? killHitColor : isCritical ? criticalHitColor : normalHitColor;
        float scale = isKill ? killHitScale : isCritical ? criticalHitScale : normalHitScale;

        PlayHitSound(isCritical, isKill);

        if (markerRoutine != null)
        {
            StopCoroutine(markerRoutine);
        }

        markerRoutine = StartCoroutine(HitMarkerRoutine(color, scale));
        StartCoroutine(DamageNumberRoutine(Mathf.RoundToInt(damageAmount), color, isCritical || isKill));

        if (isKill)
        {
            if (killRoutine != null)
            {
                StopCoroutine(killRoutine);
            }

            killRoutine = StartCoroutine(KillTextRoutine());
        }
    }

    private IEnumerator HitMarkerRoutine(Color color, float scale)
    {
        hitMarkerRoot.gameObject.SetActive(true);
        hitMarkerRoot.localScale = Vector3.one * scale;

        SetMarkerColor(color);

        float elapsed = 0f;
        while (elapsed < hitMarkerDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hitMarkerDuration);
            Color faded = color;
            faded.a = Mathf.Lerp(1f, 0f, t);
            SetMarkerColor(faded);
            hitMarkerRoot.localScale = Vector3.Lerp(Vector3.one * scale, Vector3.one * 0.82f, t);
            yield return null;
        }

        hitMarkerRoot.gameObject.SetActive(false);
        markerRoutine = null;
    }

    private IEnumerator DamageNumberRoutine(int amount, Color color, bool emphasized)
    {
        TMP_Text number = Instantiate(damageNumberPrefab, damageNumberPrefab.transform.parent);
        number.gameObject.SetActive(true);
        number.text = amount.ToString();
        number.color = color;
        number.fontSize = emphasized ? criticalDamageFontSize : normalDamageFontSize;
        number.rectTransform.anchoredPosition = damageNumberStartOffset + new Vector2(Random.Range(-22f, 22f), Random.Range(-6f, 8f));
        number.rectTransform.localScale = emphasized ? Vector3.one * 1.12f : Vector3.one;

        Vector2 start = number.rectTransform.anchoredPosition;
        Vector2 end = start + damageNumberDrift;
        float elapsed = 0f;

        while (elapsed < damageNumberDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageNumberDuration);
            float eased = EaseOutCubic(t);
            number.rectTransform.anchoredPosition = Vector2.Lerp(start, end, eased);
            Color faded = color;
            faded.a = Mathf.Lerp(1f, 0f, t);
            number.color = faded;
            yield return null;
        }

        Destroy(number.gameObject);
    }

    private IEnumerator KillTextRoutine()
    {
        killText.gameObject.SetActive(true);
        killText.text = killMessage;

        float elapsed = 0f;
        while (elapsed < killTextDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / killTextDuration);
            float pop = EaseOutBack(Mathf.Clamp01(t * 2f));
            Color color = killHitColor;
            color.a = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
            killText.color = color;
            killText.rectTransform.localScale = Vector3.one * pop;
            yield return null;
        }

        killText.gameObject.SetActive(false);
        killRoutine = null;
    }

    private void CacheReferences()
    {
        referencesReady = hitMarkerRoot != null && damageNumberPrefab != null && killText != null;

        if (!referencesReady)
        {
            Debug.LogWarning($"{nameof(HitFeedbackUi)} on {name} is missing scene UI references. Run Tools/Setup Hit Feedback UI or assign the references manually.", this);
            markerImages = System.Array.Empty<Image>();
            return;
        }

        markerImages = hitMarkerRoot.GetComponentsInChildren<Image>(true);
        damageNumberPrefab.gameObject.SetActive(false);
    }

    private void PlayHitSound(bool isCritical, bool isKill)
    {
        if (audioSource == null)
        {
            return;
        }

        AudioClip clip = isKill ? killClip : isCritical ? criticalHitClip : normalHitClip;
        if (clip == null)
        {
            return;
        }

        float volume = isKill ? killVolume : isCritical ? criticalHitVolume : normalHitVolume;
        audioSource.PlayOneShot(clip, volume);
    }

    private void SetMarkerColor(Color color)
    {
        for (int i = 0; i < markerImages.Length; i++)
        {
            if (markerImages[i] != null)
            {
                markerImages[i].color = color;
            }
        }
    }

    private void HideInstantly()
    {
        if (hitMarkerRoot != null)
        {
            hitMarkerRoot.gameObject.SetActive(false);
        }

        if (killText != null)
        {
            killText.gameObject.SetActive(false);
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