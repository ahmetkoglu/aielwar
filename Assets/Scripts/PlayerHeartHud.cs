using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows player health as heart slots. Each heart represents a fixed amount of health.
/// Empty Image components are used so heart sprites can be assigned later in the Inspector.
/// </summary>
public class PlayerHeartHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Image[] hearts;

    [Header("Heart Settings")]
    [SerializeField, Min(1f)] private float healthPerHeart = 10f;
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color missingColor = new Color(1f, 1f, 1f, 0.25f);

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<AdvancedFpsController>()?.GetComponent<Health>();
        }

        Refresh(true);
    }

    private void Start()
    {
        Refresh(true);
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDamaged.AddListener(HandleHealthChanged);
            playerHealth.OnHealed.AddListener(Refresh);
            Refresh(true);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDamaged.RemoveListener(HandleHealthChanged);
            playerHealth.OnHealed.RemoveListener(Refresh);
        }
    }

    public void Refresh()
    {
        Refresh(false);
    }

    private void Refresh(bool useMaxHealthIfNotInitialized)
    {
        if (playerHealth == null || hearts == null)
        {
            return;
        }

        float currentHealth = playerHealth.CurrentHealth;
        if (useMaxHealthIfNotInitialized && currentHealth <= 0f && playerHealth.IsAlive)
        {
            currentHealth = playerHealth.MaxHealth;
        }

        int visibleHeartCount = Mathf.CeilToInt(currentHealth / healthPerHeart);

        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null)
            {
                continue;
            }

            bool hasThisHeart = i < visibleHeartCount;
            hearts[i].color = hasThisHeart ? availableColor : missingColor;
        }
    }

    private void HandleHealthChanged(DamageInfo damageInfo)
    {
        Refresh();
    }
}