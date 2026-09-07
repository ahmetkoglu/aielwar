using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Throwable grenade projectile. Add this to the grenade prefab or let PlayerGrenadeThrower add it at runtime.
/// Explodes after a fuse, applies area damage via IDamageable, optional force, and optional explosion FX.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GrenadeProjectile : MonoBehaviour
{
    [Header("Explosion")]
    [SerializeField, Min(0.05f)] private float fuseTime = 2.4f;
    [SerializeField, Min(0f)] private float damage = 65f;
    [SerializeField, Min(0.1f)] private float radius = 5f;
    [SerializeField, Min(0f)] private float explosionForce = 650f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("FX")]
    [SerializeField] private GameObject explosionFxPrefab;
    [SerializeField, Min(0.05f)] private float explosionFxLifetime = 3f;
    [SerializeField] private bool destroyOnExplode = true;

    [Header("Audio")]
    [SerializeField] private AudioClip explosionClip;
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 0.95f;
    [SerializeField, Min(0.1f)] private float explosionAudioLifetime = 2f;

    private GameObject attacker;
    private bool hasExploded;
    private float explodeTime;

    public void Initialize(GameObject grenadeAttacker, float customFuseTime, float customDamage, float customRadius, float customForce, GameObject customExplosionFx)
    {
        attacker = grenadeAttacker;
        fuseTime = customFuseTime;
        damage = customDamage;
        radius = customRadius;
        explosionForce = customForce;

        if (customExplosionFx != null)
        {
            explosionFxPrefab = customExplosionFx;
        }

        explodeTime = Time.time + fuseTime;
    }

    public void SetExplosionAudio(AudioClip clip, float volume)
    {
        if (clip != null)
        {
            explosionClip = clip;
        }

        explosionVolume = Mathf.Clamp01(volume);
    }

    private void Awake()
    {
        explodeTime = Time.time + fuseTime;
    }

    private void Update()
    {
        if (!hasExploded && Time.time >= explodeTime)
        {
            Explode();
        }
    }

    public void Explode()
    {
        if (hasExploded)
        {
            return;
        }

        hasExploded = true;
        Vector3 explosionPosition = transform.position;

        PlayExplosionSound(explosionPosition);
        SpawnExplosionFx(explosionPosition);
        ApplyExplosionDamage(explosionPosition);
        ApplyExplosionForce(explosionPosition);

        if (destroyOnExplode)
        {
            Destroy(gameObject);
        }
    }

    private void SpawnExplosionFx(Vector3 explosionPosition)
    {
        if (explosionFxPrefab == null)
        {
            return;
        }

        GameObject spawnedFx = Instantiate(explosionFxPrefab, explosionPosition, Quaternion.identity);
        Destroy(spawnedFx, explosionFxLifetime);
    }

    private void PlayExplosionSound(Vector3 explosionPosition)
    {
        if (explosionClip == null)
        {
            return;
        }

        GameObject audioObject = new GameObject("Grenade Explosion Audio");
        audioObject.transform.position = explosionPosition;
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = explosionClip;
        source.volume = explosionVolume;
        source.spatialBlend = 0.75f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 4f;
        source.maxDistance = 45f;
        source.Play();
        Destroy(audioObject, explosionAudioLifetime);
    }

    private void ApplyExplosionDamage(Vector3 explosionPosition)
    {
        Collider[] hits = Physics.OverlapSphere(explosionPosition, radius, hitLayers, triggerInteraction);
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            IDamageable damageable = FindDamageableInParents(hit.transform);
            if (damageable == null || !damageable.IsAlive || damagedTargets.Contains(damageable))
            {
                continue;
            }

            Vector3 closestPoint = hit.ClosestPoint(explosionPosition);
            float distance = Vector3.Distance(explosionPosition, closestPoint);
            float falloff = Mathf.Clamp01(1f - distance / radius);
            float finalDamage = damage * Mathf.Lerp(0.35f, 1f, falloff);
            Vector3 direction = (closestPoint - explosionPosition).sqrMagnitude > 0.001f
                ? (closestPoint - explosionPosition).normalized
                : Vector3.up;

            DamageInfo damageInfo = new DamageInfo(
                finalDamage,
                attacker != null ? attacker : gameObject,
                gameObject,
                closestPoint,
                -direction,
                direction,
                hit,
                false
            );

            damageable.TakeDamage(damageInfo);
            damagedTargets.Add(damageable);
        }
    }

    private void ApplyExplosionForce(Vector3 explosionPosition)
    {
        if (explosionForce <= 0f)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(explosionPosition, radius, hitLayers, triggerInteraction);
        for (int i = 0; i < hits.Length; i++)
        {
            Rigidbody body = hits[i] != null ? hits[i].attachedRigidbody : null;
            if (body != null && !body.isKinematic)
            {
                body.AddExplosionForce(explosionForce, explosionPosition, radius, 0.75f, ForceMode.Impulse);
            }
        }
    }

    private static IDamageable FindDamageableInParents(Transform startTransform)
    {
        Transform current = startTransform;
        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDamageable damageable)
                {
                    return damageable;
                }
            }

            current = current.parent;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}