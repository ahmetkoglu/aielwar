using UnityEngine;

/// <summary>
/// Pickup item that heals the player on contact.
/// Attach to a GameObject with a trigger Collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HealthPickup : MonoBehaviour
{
    [Header("Healing")]
    [SerializeField, Min(1f)] private float healAmount = 25f;

    [Header("Effects")]
    [SerializeField] private GameObject pickupEffectPrefab;
    [SerializeField, Min(0.01f)] private float effectLifetime = 1.5f;

    [Header("Rotation")]
    [SerializeField] private bool rotate = true;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);

    private void Update()
    {
        if (rotate)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Try to heal the player
        Health health = other.GetComponentInParent<Health>();
        if (health == null || !health.IsAlive)
        {
            return;
        }

        // Check if it's the player by tag
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player"))
        {
            return;
        }

        // Apply healing
        health.Heal(healAmount);

        // Spawn pickup effect
        if (pickupEffectPrefab != null)
        {
            GameObject effect = Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, effectLifetime);
        }

        // Destroy the pickup
        Destroy(gameObject);
    }
}