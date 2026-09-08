using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Bottom-center weapon HUD showing Rifle (1), Pistol (2) and Grenade (G).
/// Assign the UI boxes in the Inspector, or leave them empty to auto-create at runtime.
/// Highlights the currently equipped weapon.
/// </summary>
public class WeaponHudController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private PlayerGrenadeThrower grenadeThrower;

    [Header("HUD Root (optional)")]
    [Tooltip("Parent object for the weapon HUD. If empty, a new one is created under the Canvas.")]
    [SerializeField] private RectTransform hudRoot;

    [Header("Weapon Boxes (optional)")]
    [Tooltip("Leave empty to auto-create at runtime.")]
    [SerializeField] private Image rifleBox;
    [SerializeField] private Image pistolBox;
    [SerializeField] private Image grenadeBox;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.1f, 0.1f, 0.1f, 0.75f);
    [SerializeField] private Color activeColor = new Color(0.2f, 0.6f, 1f, 0.9f);
    [SerializeField] private Color textColor = Color.white;

    [Header("Layout")]
    [SerializeField] private Vector2 boxSize = new Vector2(90f, 70f);
    [SerializeField] private float boxSpacing = 10f;
    [SerializeField] private float bottomOffset = 20f;

    private TextMeshProUGUI rifleText;
    private TextMeshProUGUI pistolText;
    private TextMeshProUGUI grenadeText;

    private void Start()
    {
        if (weaponSwitcher == null)
        {
            weaponSwitcher = GetComponent<WeaponSwitcher>();
        }

        if (grenadeThrower == null)
        {
            grenadeThrower = GetComponent<PlayerGrenadeThrower>();
        }

        CreateHudIfNeeded();
    }

    private void Update()
    {
        UpdateHighlight();
    }

    private void CreateHudIfNeeded()
    {
        if (hudRoot == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("WeaponHudController: No Canvas found in scene.");
                return;
            }

            GameObject hudRootObj = new GameObject("Weapon HUD");
            hudRootObj.transform.SetParent(canvas.transform, false);
            hudRoot = hudRootObj.AddComponent<RectTransform>();
            hudRoot.anchorMin = new Vector2(0.5f, 0f);
            hudRoot.anchorMax = new Vector2(0.5f, 0f);
            hudRoot.pivot = new Vector2(0.5f, 0f);
            hudRoot.anchoredPosition = new Vector2(0f, bottomOffset);
            hudRoot.sizeDelta = new Vector2(300f, 80f);
        }

        float totalWidth = boxSize.x * 3f + boxSpacing * 2f;
        float startX = -totalWidth * 0.5f + boxSize.x * 0.5f;

        if (rifleBox == null)
        {
            rifleBox = CreateBox(hudRoot, "Rifle Box", new Vector2(startX, 0f), "RIFLE\n[1]", out rifleText);
        }

        if (pistolBox == null)
        {
            pistolBox = CreateBox(hudRoot, "Pistol Box", new Vector2(startX + boxSize.x + boxSpacing, 0f), "PISTOL\n[2]", out pistolText);
        }

        if (grenadeBox == null)
        {
            grenadeBox = CreateBox(hudRoot, "Grenade Box", new Vector2(startX + (boxSize.x + boxSpacing) * 2f, 0f), "GRENADE\n[G]", out grenadeText);
        }
    }

    private Image CreateBox(Transform parent, string name, Vector2 anchoredPosition, string label, out TextMeshProUGUI text)
    {
        GameObject boxObj = new GameObject(name);
        boxObj.transform.SetParent(parent, false);

        RectTransform rect = boxObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = boxSize;

        Image image = boxObj.AddComponent<Image>();
        image.color = normalColor;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(boxObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 18f;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = false;

        return image;
    }

    private void UpdateHighlight()
    {
        if (weaponSwitcher == null)
        {
            return;
        }

        bool rifleActive = weaponSwitcher.IsPrimaryActive;
        bool pistolActive = weaponSwitcher.IsSecondaryActive;
        bool grenadeActive = grenadeThrower != null && grenadeThrower.IsHoldingGrenade;

        SetBoxState(rifleBox, rifleActive && !grenadeActive);
        SetBoxState(pistolBox, pistolActive && !grenadeActive);
        SetBoxState(grenadeBox, grenadeActive);
    }

    private void SetBoxState(Image box, bool active)
    {
        if (box != null)
        {
            box.color = active ? activeColor : normalColor;
        }
    }
}