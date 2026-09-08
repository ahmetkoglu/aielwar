using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Manages score, kill streak combos, and displays combo effects.
/// Attach to the player root or a persistent game manager.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("Score Display")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private string scorePrefix = "SCORE: ";

    [Header("Combo System")]
    [SerializeField, Min(0.1f)] private float comboTimeWindow = 2.5f;
    [SerializeField] private int baseScorePerKill = 100;

    [Header("Combo Text Effect")]
    [SerializeField] private TMP_Text comboTextPrefab;
    [SerializeField] private string comboMessage = "COMBO!";
    [SerializeField] private Color comboColor = new Color(1f, 0.85f, 0.05f, 1f);
    [SerializeField, Min(0.05f)] private float comboTextDuration = 0.8f;
    [SerializeField] private Vector2 comboTextStartOffset = new Vector2(0f, 80f);
    [SerializeField] private Vector2 comboTextDrift = new Vector2(0f, 40f);

    [Header("Kill Streak Text Effect")]
    [SerializeField] private TMP_Text streakTextPrefab;
    [SerializeField] private Color streakColor = new Color(1f, 0.4f, 0.1f, 1f);
    [SerializeField, Min(0.05f)] private float streakTextDuration = 1f;
    [SerializeField] private Vector2 streakTextStartOffset = new Vector2(0f, 110f);
    [SerializeField] private Vector2 streakTextDrift = new Vector2(0f, 50f);
    [SerializeField] private float streakTextStartScale = 1.5f;

    public int Score { get; private set; }
    public int KillStreak { get; private set; }
    public int CurrentMultiplier => Mathf.Max(1, KillStreak);

    private float lastKillTime;
    private Coroutine comboResetRoutine;

    private void Awake()
    {
        if (scoreText != null)
        {
            scoreText.text = $"{scorePrefix}0";
        }

        if (comboTextPrefab != null)
        {
            comboTextPrefab.gameObject.SetActive(false);
        }

        if (streakTextPrefab != null)
        {
            streakTextPrefab.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Called when an enemy dies. Updates score, combo, and shows effects.
    /// </summary>
    public void RegisterEnemyKill()
    {
        float timeSinceLastKill = Time.time - lastKillTime;

        if (timeSinceLastKill <= comboTimeWindow)
        {
            KillStreak++;
        }
        else
        {
            KillStreak = 1;
        }

        lastKillTime = Time.time;

        // Calculate score with multiplier
        int pointsGained = baseScorePerKill * CurrentMultiplier;
        Score += pointsGained;

        // Update score display
        if (scoreText != null)
        {
            scoreText.text = $"{scorePrefix}{Score}";
        }

        // Show streak text if multiplier > 1
        if (CurrentMultiplier >= 2)
        {
            ShowStreakText();
        }

        // Reset combo after window expires
        if (comboResetRoutine != null)
        {
            StopCoroutine(comboResetRoutine);
        }
        comboResetRoutine = StartCoroutine(ComboResetRoutine());
    }

    private IEnumerator ComboResetRoutine()
    {
        yield return new WaitForSeconds(comboTimeWindow);
        KillStreak = 0;
        comboResetRoutine = null;
    }

    private void ShowStreakText()
    {
        if (streakTextPrefab == null)
        {
            return;
        }

        TMP_Text textInstance = Instantiate(streakTextPrefab, streakTextPrefab.transform.parent);
        StartCoroutine(StreakTextRoutine(textInstance));
    }

    private IEnumerator StreakTextRoutine(TMP_Text textInstance)
    {
        textInstance.gameObject.SetActive(true);
        textInstance.text = $"x{CurrentMultiplier} {comboMessage}";
        textInstance.color = streakColor;
        textInstance.rectTransform.anchoredPosition = streakTextStartOffset;
        textInstance.rectTransform.localScale = Vector3.one * streakTextStartScale;

        Vector2 start = streakTextStartOffset;
        Vector2 end = start + streakTextDrift;
        float elapsed = 0f;

        while (elapsed < streakTextDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / streakTextDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            textInstance.rectTransform.anchoredPosition = Vector2.Lerp(start, end, eased);
            textInstance.rectTransform.localScale = Vector3.Lerp(Vector3.one * streakTextStartScale, Vector3.one * 0.6f, t);
            Color faded = streakColor;
            faded.a = Mathf.Lerp(1f, 0f, t);
            textInstance.color = faded;
            yield return null;
        }

        Destroy(textInstance.gameObject);
    }
}