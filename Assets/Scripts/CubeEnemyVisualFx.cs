using System.Collections;
using UnityEngine;

/// <summary>
/// Visual-only polish for cube enemy prototypes: sliding hover, forward roll while moving,
/// golden charge flash/particle burst when the enemy fires, and red flash feedback when damaged.
/// </summary>
public class CubeEnemyVisualFx : MonoBehaviour
{
    [Header("Movement Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform[] rollingParts;
    [SerializeField, Min(0f)] private float bobAmount = 0.08f;
    [SerializeField, Min(0f)] private float bobSpeed = 8f;
    [SerializeField, Min(0f)] private float rollDegreesPerMeter = 150f;
    [SerializeField, Min(0f)] private float leanAngle = 7f;
    [SerializeField, Min(0f)] private float visualSmooth = 12f;
    [Header("Fire Flash")]
    [Tooltip("Only these renderers flash yellow when firing. If empty, Body Hitbox and Chest Cyan Armor Plate are found automatically.")]
    [SerializeField] private Renderer[] fireFlashRenderers;
    [SerializeField] private Renderer[] flashRenderers;
    [SerializeField] private ParticleSystem fireChargeFx;
    [SerializeField] private Color fireFlashColor = new Color(1f, 0.72f, 0.08f, 1f);
    [SerializeField, Min(0.01f)] private float fireFlashDuration = 0.18f;
    [SerializeField, Min(0f)] private float emissionIntensity = 2.2f;

    [Header("Damage Flash")]
    [SerializeField] private Health health;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.02f, 0.02f, 1f);
    [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.16f;
    [SerializeField, Min(0f)] private float damageEmissionIntensity = 3f;
    [SerializeField, Min(0f)] private float damagePunchScale = 0.08f;

    private MaterialPropertyBlock propertyBlock;
    private Vector3 lastPosition;
    private Vector3 visualBaseLocalPosition;
    private Quaternion visualBaseLocalRotation;
    private Coroutine fireRoutine;
    private Coroutine damageRoutine;
    private float fireFlashWeight;
    private float damageFlashWeight;
    private float damagePunchWeight;
    private float lastObservedHealth;
    private bool hasObservedHealth;
    private float rollAmount;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (rollingParts == null || rollingParts.Length == 0)
        {
            rollingParts = new[] { visualRoot };
        }

        if (flashRenderers == null || flashRenderers.Length == 0)
        {
            flashRenderers = GetNonParticleRenderersInChildren();
        }

        if (fireFlashRenderers == null || fireFlashRenderers.Length == 0)
        {
            fireFlashRenderers = FindDefaultFireFlashRenderers();
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        lastPosition = transform.position;
        visualBaseLocalPosition = visualRoot.localPosition;
        visualBaseLocalRotation = visualRoot.localRotation;
        ClearAllFlashBlocks();
    }

    private void Start()
    {
        CacheObservedHealth();
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health != null)
        {
            health.OnDamaged.AddListener(PlayDamageFx);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged.RemoveListener(PlayDamageFx);
        }

        ClearAllFlashBlocks();
    }

    private void Update()
    {
        DetectHealthDropFallback();
        UpdateMovementVisual();
    }

    private void DetectHealthDropFallback()
    {
        if (health == null)
        {
            return;
        }

        float currentHealth = health.CurrentHealth;
        if (!hasObservedHealth)
        {
            lastObservedHealth = currentHealth;
            hasObservedHealth = true;
            return;
        }

        if (currentHealth < lastObservedHealth)
        {
            PlayDamageFx();
        }

        lastObservedHealth = currentHealth;
    }

    private void CacheObservedHealth()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health == null)
        {
            return;
        }

        lastObservedHealth = health.CurrentHealth;
        hasObservedHealth = true;
    }

    public void PlayFireFx()
    {
        if (fireChargeFx != null)
        {
            fireChargeFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fireChargeFx.Play(true);
        }

        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
        }

        fireRoutine = StartCoroutine(FireFlashRoutine());
    }

    public void PlayDamageFx(DamageInfo damageInfo)
    {
        PlayDamageFx();
    }

    public void PlayDamageFx()
    {
        if (damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
        }

        damageRoutine = StartCoroutine(DamageFlashRoutine());
    }

    private void UpdateMovementVisual()
    {
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;

        float movingWeight = Mathf.Clamp01(speed / 2f);
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount * movingWeight;
        Vector3 damagePunch = Vector3.up * (damagePunchScale * damagePunchWeight);
        Vector3 targetLocalPosition = visualBaseLocalPosition + Vector3.up * bob + damagePunch;
        visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetLocalPosition, visualSmooth * Time.deltaTime);

        if (delta.sqrMagnitude > 0.00001f)
        {
            rollAmount += delta.magnitude * rollDegreesPerMeter;
        }

        float lean = Mathf.Sin(Time.time * bobSpeed * 0.65f) * leanAngle * movingWeight;
        visualRoot.localRotation = Quaternion.Slerp(
            visualRoot.localRotation,
            visualBaseLocalRotation * Quaternion.Euler(0f, 0f, lean),
            visualSmooth * Time.deltaTime
        );

        for (int i = 0; i < rollingParts.Length; i++)
        {
            if (rollingParts[i] != null && rollingParts[i] != visualRoot)
            {
                rollingParts[i].localRotation = Quaternion.Euler(rollAmount, 0f, 0f);
            }
        }
    }

    private IEnumerator FireFlashRoutine()
    {
        float elapsed = 0f;
        while (elapsed < fireFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fireFlashDuration);
            fireFlashWeight = 1f - t;
            ApplyFlash();
            yield return null;
        }

        fireFlashWeight = 0f;
        ApplyFlash();
        fireRoutine = null;
    }

    private IEnumerator DamageFlashRoutine()
    {
        float elapsed = 0f;
        while (elapsed < damageFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageFlashDuration);
            damageFlashWeight = 1f - Mathf.SmoothStep(0f, 1f, t);
            damagePunchWeight = Mathf.Sin(t * Mathf.PI) * damageFlashWeight;
            ApplyFlash();
            yield return null;
        }

        damageFlashWeight = 0f;
        damagePunchWeight = 0f;
        ApplyFlash();
        damageRoutine = null;
    }

    private void ApplyFlash()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (fireFlashWeight > 0f)
        {
            ApplyRendererFlash(fireFlashRenderers, fireFlashColor, emissionIntensity, fireFlashWeight);
        }

        if (damageFlashWeight > 0f)
        {
            ApplyRendererFlash(flashRenderers, damageFlashColor, damageEmissionIntensity, damageFlashWeight);
        }

        if (fireFlashWeight <= 0f && damageFlashWeight <= 0f)
        {
            ClearAllFlashBlocks();
        }
    }

    private void ApplyRendererFlash(Renderer[] renderers, Color flashColor, float flashEmissionIntensity, float weight)
    {
        if (renderers == null)
        {
            return;
        }

        Color emission = flashColor * (flashEmissionIntensity * weight);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            Color originalColor = GetRendererBaseColor(renderer);
            Color baseColor = Color.Lerp(originalColor, flashColor, weight);

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", baseColor);
            propertyBlock.SetColor("_Color", baseColor);
            propertyBlock.SetColor("_EmissionColor", emission);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private Color GetRendererBaseColor(Renderer targetRenderer)
    {
        if (targetRenderer == null || targetRenderer.sharedMaterial == null)
        {
            return Color.white;
        }

        Material material = targetRenderer.sharedMaterial;
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        return Color.white;
    }

    private void ClearAllFlashBlocks()
    {
        ClearPropertyBlocks(flashRenderers);
        ClearPropertyBlocks(fireFlashRenderers);
    }

    private void ClearPropertyBlocks(Renderer[] renderers)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && !(renderers[i] is ParticleSystemRenderer))
            {
                renderers[i].SetPropertyBlock(null);
            }
        }
    }

    private Renderer[] GetNonParticleRenderersInChildren()
    {
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
        var renderers = new System.Collections.Generic.List<Renderer>();

        for (int i = 0; i < childRenderers.Length; i++)
        {
            if (childRenderers[i] != null && !(childRenderers[i] is ParticleSystemRenderer))
            {
                renderers.Add(childRenderers[i]);
            }
        }

        return renderers.ToArray();
    }

    private Renderer[] FindDefaultFireFlashRenderers()
    {
        Renderer[] childRenderers = GetNonParticleRenderersInChildren();
        var renderers = new System.Collections.Generic.List<Renderer>();

        for (int i = 0; i < childRenderers.Length; i++)
        {
            string rendererName = childRenderers[i].name;
            if (rendererName.Contains("Body Hitbox") || rendererName.Contains("Chest Cyan Armor Plate"))
            {
                renderers.Add(childRenderers[i]);
            }
        }

        return renderers.Count > 0 ? renderers.ToArray() : childRenderers;
    }

}