using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a minimap that follows the player and shows enemies as red dots,
/// the player as a green dot, and the terrain below.
/// The minimap is north-up (fixed). A view cone rotates to show player look direction.
/// Enemies outside the circular mask are clamped to the edge.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MinimapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private RawImage minimapUiImage;

    [Header("Camera Settings")]
    [SerializeField] private float cameraHeight = 40f;
    [SerializeField] private float cameraSize = 40f;

    [Header("Render Texture")]
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField, Min(64)] private int renderTextureSize = 256;

    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Dot Colors")]
    [SerializeField] private Color playerDotColor = Color.green;
    [SerializeField] private Color enemyDotColor = Color.red;
    [SerializeField] private Color edgeDotColor = new Color(1f, 0.3f, 0.1f, 0.7f);
    [SerializeField, Min(4f)] private float playerDotRadius = 6f;
    [SerializeField, Min(4f)] private float enemyDotRadius = 5f;

    [Header("View Cone")]
    [SerializeField] private bool showViewCone = true;
    [SerializeField, Range(10f, 180f)] private float viewConeAngle = 90f;
    [SerializeField] private Color viewConeColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField, Min(10f)] private float viewConeDistance = 0.8f;

    private Camera minimapCamera;
    private RectTransform playerDotTransform;
    private Transform dotContainer;
    private RectTransform minimapRect;
    private float minimapRadius = 100f;
    private Texture2D playerDotTexture;
    private Texture2D enemyDotTexture;
    private Image playerDotImage;
    private Image viewConeImage;
    private RectTransform viewConeTransform;
    private GameObject maskObject;
    private bool isSetupComplete;

    private void Awake()
    {
        minimapCamera = GetComponent<Camera>();

        if (playerTarget == null)
        {
            FindPlayerByTag();
        }

        if (minimapUiImage != null)
        {
            minimapRect = minimapUiImage.rectTransform;
        }

        SetupCamera();
        SetupRenderTexture();
        CreateDotTextures();
    }

    private void Start()
    {
        ForceMinimapRectPosition();
        CreatePlayerDot();
        SetupCircularMask();
        CreateViewCone();
        SnapCameraToPlayer();
        isSetupComplete = true;
    }

    /// <summary>
    /// Forces the minimap UI to the bottom-left corner with correct pivot/anchors.
    /// This avoids issues with Inspector values being set incorrectly.
    /// </summary>
    private void ForceMinimapRectPosition()
    {
        if (minimapRect == null)
        {
            return;
        }

        minimapRect.anchorMin = new Vector2(0f, 0f);
        minimapRect.anchorMax = new Vector2(0f, 0f);
        minimapRect.pivot = new Vector2(0.5f, 0.5f);
        minimapRect.anchoredPosition = new Vector2(110f, 110f);
        minimapRect.sizeDelta = new Vector2(200f, 200f);
    }

    private void LateUpdate()
    {
        if (!isSetupComplete)
        {
            return;
        }

        SnapCameraToPlayer();
        UpdateEnemyDots();
        UpdateViewCone();
    }

    private void SetupCamera()
    {
        if (minimapCamera == null)
        {
            return;
        }

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = cameraSize;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        minimapCamera.cullingMask = ~0;
    }

    private void SetupRenderTexture()
    {
        if (minimapUiImage == null || minimapCamera == null)
        {
            return;
        }

        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16);
            renderTexture.name = "Minimap RT (Runtime)";
        }

        minimapCamera.targetTexture = renderTexture;
        minimapUiImage.texture = renderTexture;
    }

    private void SetupCircularMask()
    {
        if (minimapUiImage == null || minimapRect == null)
        {
            return;
        }

        Transform parent = minimapUiImage.transform.parent;
        if (parent == null)
        {
            return;
        }

        // Destroy any existing mask from previous runs
        Transform existingMask = parent.Find("Minimap Circle Mask");
        if (existingMask != null)
        {
            // First reparent the minimap UI back to the original parent (Canvas) so it doesn't get destroyed
            if (minimapUiImage.transform.IsChildOf(existingMask))
            {
                Transform originalParent = existingMask.parent;
                minimapUiImage.transform.SetParent(originalParent, false);
            }
            DestroyImmediate(existingMask.gameObject);
        }

        float rectWidth = minimapRect.rect.width;
        if (rectWidth <= 1f)
        {
            rectWidth = 200f;
        }

        maskObject = new GameObject("Minimap Circle Mask");
        maskObject.transform.SetParent(parent, false);
        maskObject.transform.SetSiblingIndex(minimapUiImage.transform.GetSiblingIndex());

        RectTransform maskRect = maskObject.AddComponent<RectTransform>();
        maskRect.anchorMin = minimapRect.anchorMin;
        maskRect.anchorMax = minimapRect.anchorMax;
        maskRect.pivot = minimapRect.pivot;
        maskRect.anchoredPosition = minimapRect.anchoredPosition;
        maskRect.sizeDelta = minimapRect.sizeDelta;

        Mask mask = maskObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        Image maskImage = maskObject.AddComponent<Image>();
        maskImage.sprite = CreateCircleSprite(Mathf.Max(1, Mathf.RoundToInt(rectWidth * 0.5f)));
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;

        minimapUiImage.transform.SetParent(maskObject.transform, false);
        minimapRect.anchorMin = Vector2.zero;
        minimapRect.anchorMax = Vector2.one;
        minimapRect.offsetMin = Vector2.zero;
        minimapRect.offsetMax = Vector2.zero;

        minimapRadius = rectWidth * 0.5f;
    }

    private Sprite CreateCircleSprite(int radius)
    {
        radius = Mathf.Max(1, radius);
        int diameter = radius * 2;
        Texture2D texture = new Texture2D(diameter, diameter);
        Color32[] pixels = new Color32[diameter * diameter];
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * diameter + x] = dist <= radius ? Color.white : Color.clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f));
    }

    private void CreateViewCone()
    {
        if (!showViewCone || minimapUiImage == null)
        {
            return;
        }

        GameObject coneObj = new GameObject("Minimap View Cone");
        coneObj.transform.SetParent(minimapUiImage.transform, false);
        
        viewConeTransform = coneObj.AddComponent<RectTransform>();
        viewConeTransform.anchorMin = new Vector2(0.5f, 0.5f);
        viewConeTransform.anchorMax = new Vector2(0.5f, 0.5f);
        viewConeTransform.sizeDelta = new Vector2(minimapRadius * 2f, minimapRadius * 2f);
        viewConeTransform.anchoredPosition = Vector2.zero;

        viewConeImage = coneObj.AddComponent<Image>();
        viewConeImage.sprite = CreateViewConeSprite();
        viewConeImage.color = viewConeColor;
        viewConeImage.raycastTarget = false;
    }

    private Sprite CreateViewConeSprite()
    {
        int diameter = Mathf.Max(2, Mathf.RoundToInt(minimapRadius * 2f));
        int radius = diameter / 2;
        Texture2D texture = new Texture2D(diameter, diameter);
        Color32[] pixels = new Color32[diameter * diameter];
        Vector2 center = new Vector2(radius, radius);

        float halfAngle = viewConeAngle * 0.5f * Mathf.Deg2Rad;
        float maxDist = radius * viewConeDistance;

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                Vector2 pixelPos = new Vector2(x, y);
                Vector2 dir = pixelPos - center;
                float dist = dir.magnitude;

                if (dist > maxDist || dist < 1f)
                {
                    pixels[y * diameter + x] = Color.clear;
                    continue;
                }

                float angle = Mathf.Atan2(dir.x, dir.y);
                
                if (Mathf.Abs(angle) <= halfAngle)
                {
                    float edgeFade = 1f - (Mathf.Abs(angle) / halfAngle) * 0.3f;
                    float distFade = 1f - (dist / maxDist) * 0.2f;
                    Color c = Color.white;
                    c.a = 1f * edgeFade * distFade;
                    pixels[y * diameter + x] = c;
                }
                else
                {
                    pixels[y * diameter + x] = Color.clear;
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f));
    }

    private void CreateDotTextures()
    {
        int size = Mathf.Max(1, Mathf.CeilToInt(playerDotRadius * 2f));
        playerDotTexture = new Texture2D(size, size);
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(playerDotRadius, playerDotRadius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = dist <= playerDotRadius ? playerDotColor : Color.clear;
            }
        }
        playerDotTexture.SetPixels32(pixels);
        playerDotTexture.Apply();

        size = Mathf.Max(1, Mathf.CeilToInt(enemyDotRadius * 2f));
        enemyDotTexture = new Texture2D(size, size);
        pixels = new Color32[size * size];
        center = new Vector2(enemyDotRadius, enemyDotRadius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = dist <= enemyDotRadius ? enemyDotColor : Color.clear;
            }
        }
        enemyDotTexture.SetPixels32(pixels);
        enemyDotTexture.Apply();
    }

    private void CreatePlayerDot()
    {
        if (minimapUiImage == null || playerDotTexture == null)
        {
            return;
        }

        GameObject dotObj = new GameObject("Player Minimap Dot");
        dotObj.transform.SetParent(minimapUiImage.transform, false);
        playerDotTransform = dotObj.AddComponent<RectTransform>();
        playerDotImage = dotObj.AddComponent<Image>();
        playerDotImage.sprite = CreateSpriteFromTexture(playerDotTexture);
        playerDotImage.raycastTarget = false;

        playerDotTransform.anchorMin = new Vector2(0.5f, 0.5f);
        playerDotTransform.anchorMax = new Vector2(0.5f, 0.5f);
        playerDotTransform.sizeDelta = new Vector2(playerDotRadius * 2f, playerDotRadius * 2f);
        playerDotTransform.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Snap camera directly to player position (no lerp) for accuracy.
    /// </summary>
    private void SnapCameraToPlayer()
    {
        if (playerTarget == null)
        {
            FindPlayerByTag();
            return;
        }

        transform.position = new Vector3(playerTarget.position.x, cameraHeight, playerTarget.position.z);
    }

    private void UpdateViewCone()
    {
        if (viewConeTransform == null || playerTarget == null)
        {
            return;
        }

        // Camera faces up (+Y = north), but UI's "up" is at 0° rotation.
        // Negative because Unity angles increase clockwise but UI rotation is counter-clockwise.
        viewConeTransform.localRotation = Quaternion.Euler(0f, 0f, -playerTarget.eulerAngles.y);
    }

    private void UpdateEnemyDots()
    {
        if (minimapUiImage == null || enemyDotTexture == null || minimapRect == null)
        {
            return;
        }

        minimapRadius = Mathf.Max(1f, minimapRect.rect.width * 0.5f);

        if (dotContainer == null)
        {
            GameObject container = new GameObject("Enemy Dots Container");
            dotContainer = container.transform;
            dotContainer.SetParent(minimapUiImage.transform, false);
        }

        EnemyChaseAttack[] enemies = FindObjectsByType<EnemyChaseAttack>(FindObjectsSortMode.None);

        while (dotContainer.childCount > enemies.Length)
        {
            Transform child = dotContainer.GetChild(dotContainer.childCount - 1);
            if (child != null)
            {
                DestroyImmediate(child.gameObject);
            }
        }

        while (dotContainer.childCount < enemies.Length)
        {
            GameObject dotObj = new GameObject("Enemy Minimap Dot");
            dotObj.transform.SetParent(dotContainer, false);
            Image dotImage = dotObj.AddComponent<Image>();
            dotImage.sprite = CreateSpriteFromTexture(enemyDotTexture);
            dotImage.raycastTarget = false;

            RectTransform rt = dotObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(enemyDotRadius * 2f, enemyDotRadius * 2f);
        }

        for (int i = 0; i < enemies.Length && i < dotContainer.childCount; i++)
        {
            if (enemies[i] == null)
            {
                continue;
            }

            RectTransform dot = dotContainer.GetChild(i) as RectTransform;
            if (dot == null)
            {
                continue;
            }

            // Use WorldToViewportPoint for the most reliable mapping.
            // For an orthographic camera looking straight down:
            //   viewport.x = world X (0 = west/left, 1 = east/right)
            //   viewport.y = world Z (0 = south/bottom, 1 = north/top)
            // Convert from 0..1 to -1..1 relative to center.
            Vector3 viewportPos = minimapCamera.WorldToViewportPoint(enemies[i].transform.position);
            
            float normalizedX = (viewportPos.x - 0.5f) * 2f;
            float normalizedZ = (viewportPos.y - 0.5f) * 2f;

            float distanceFromCenter = Mathf.Sqrt(normalizedX * normalizedX + normalizedZ * normalizedZ);
            bool isOnEdge = distanceFromCenter > 0.95f;

            if (distanceFromCenter > 0.95f)
            {
                if (distanceFromCenter > 0.001f)
                {
                    normalizedX = (normalizedX / distanceFromCenter) * 0.95f;
                    normalizedZ = (normalizedZ / distanceFromCenter) * 0.95f;
                }
                else
                {
                    normalizedX = 0f;
                    normalizedZ = 0f;
                }
            }

            float halfSize = minimapRadius * 0.95f;
            dot.anchoredPosition = new Vector2(normalizedX * halfSize, normalizedZ * halfSize);

            if (isOnEdge)
            {
                Image dotImage = dot.GetComponent<Image>();
                if (dotImage != null)
                {
                    dotImage.color = edgeDotColor;
                }
            }
            else
            {
                Image dotImage = dot.GetComponent<Image>();
                if (dotImage != null)
                {
                    dotImage.color = Color.white;
                }
            }
        }
    }

    private void FindPlayerByTag()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            playerTarget = player.transform;
        }
    }

    private Sprite CreateSpriteFromTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return null;
        }

        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private void OnValidate()
    {
        if (minimapCamera == null)
        {
            minimapCamera = GetComponent<Camera>();
        }

        if (minimapCamera != null)
        {
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = cameraSize;
        }
    }
}