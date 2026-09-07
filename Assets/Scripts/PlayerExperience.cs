using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Minimal player XP/level component for roguelike progression.
/// Add manually to the player root.
/// </summary>
public class PlayerExperience : MonoBehaviour
{
    [Header("Experience")]
    [SerializeField, Min(1)] private int startingLevel = 1;
    [SerializeField, Min(1)] private int baseXpToLevel = 100;
    [SerializeField, Min(1f)] private float levelScaling = 1.25f;

    [Header("Events")]
    public UnityEvent<int> OnExperienceChanged;
    public UnityEvent<int> OnLevelChanged;
    public UnityEvent OnLevelUp;

    public int Level { get; private set; }
    public int CurrentExperience { get; private set; }
    public int ExperienceToNextLevel { get; private set; }

    private void Awake()
    {
        Level = Mathf.Max(1, startingLevel);
        ExperienceToNextLevel = CalculateXpForLevel(Level);
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        CurrentExperience += amount;
        OnExperienceChanged?.Invoke(CurrentExperience);

        while (CurrentExperience >= ExperienceToNextLevel)
        {
            CurrentExperience -= ExperienceToNextLevel;
            Level++;
            ExperienceToNextLevel = CalculateXpForLevel(Level);
            OnLevelChanged?.Invoke(Level);
            OnLevelUp?.Invoke();
        }
    }

    private int CalculateXpForLevel(int level)
    {
        return Mathf.RoundToInt(baseXpToLevel * Mathf.Pow(levelScaling, Mathf.Max(0, level - 1)));
    }
}