using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a Canvas/UI damage feedback effect when the attached Health takes damage.
/// Add manually to the player root. Assign your Canvas FX object to damageScreenFx.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerDamageScreenFx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject damageScreenFx;
    [SerializeField] private CanvasGroup damageCanvasGroup;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.35f;

    [Header("Behaviour")]
    [SerializeField] private bool deactivateFxWhenHidden = true;
    [SerializeField] private bool restartEffectOnEveryHit = true;

    private Health health;
    private Coroutine flashRoutine;

    private void Awake()
    {
        health = GetComponent<Health>();

        if (damageCanvasGroup == null && damageScreenFx != null)
        {
            damageCanvasGroup = damageScreenFx.GetComponentInChildren<CanvasGroup>(true);
        }

        HideInstantly();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDamaged.AddListener(HandleDamaged);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged.RemoveListener(HandleDamaged);
        }
    }

    private void HandleDamaged(DamageInfo damageInfo)
    {
        if (damageScreenFx == null)
        {
            return;
        }

        if (flashRoutine != null && restartEffectOnEveryHit)
        {
            StopCoroutine(flashRoutine);
        }

        if (flashRoutine == null || restartEffectOnEveryHit)
        {
            flashRoutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        damageScreenFx.SetActive(true);

        if (damageCanvasGroup != null)
        {
            damageCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(flashDuration);

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);

            if (damageCanvasGroup != null)
            {
                damageCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            }

            yield return null;
        }

        HideInstantly();
        flashRoutine = null;
    }

    private void HideInstantly()
    {
        if (damageCanvasGroup != null)
        {
            damageCanvasGroup.alpha = 0f;
        }

        if (damageScreenFx != null && deactivateFxWhenHidden)
        {
            damageScreenFx.SetActive(false);
        }
    }
}