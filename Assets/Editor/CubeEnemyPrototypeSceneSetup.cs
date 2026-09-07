using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class CubeEnemyPrototypeSceneSetup
{
    private const string RootName = "Cube Enemy Prototype";
    private const string MaterialFolder = "Assets/Materials/Prototypes";

    [MenuItem("Tools/Setup Cube Enemy Prototype")]
    public static void SetupCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("No valid active scene found for Cube Enemy Prototype setup.");
            return;
        }

        EnsureMaterialFolder();

        Material bodyMaterial = CreateOrLoadMaterial("M_CubeEnemy_Body_DarkNavy", new Color(0.05f, 0.08f, 0.16f, 1f), 0.25f, 0.75f);
        Material armorMaterial = CreateOrLoadMaterial("M_CubeEnemy_Armor_Cyan", new Color(0.0f, 0.75f, 1f, 1f), 0.15f, 0.35f);
        Material headMaterial = CreateOrLoadMaterial("M_CubeEnemy_Head_Charcoal", new Color(0.11f, 0.12f, 0.15f, 1f), 0.22f, 0.7f);
        Material visorMaterial = CreateOrLoadMaterial("M_CubeEnemy_Visor_Magenta", new Color(1f, 0.05f, 0.55f, 1f), 0.05f, 0.2f);
        Material weakPointMaterial = CreateOrLoadMaterial("M_CubeEnemy_WeakPoint_Orange", new Color(1f, 0.42f, 0.04f, 1f), 0.08f, 0.28f);

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            root.transform.position = GetSpawnPosition();
            root.transform.rotation = Quaternion.identity;
        }

        Health health = root.GetComponent<Health>();
        if (health == null)
        {
            health = root.AddComponent<Health>();
        }

        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = root.AddComponent<NavMeshAgent>();
        }

        agent.speed = 3.8f;
        agent.angularSpeed = 540f;
        agent.acceleration = 18f;
        agent.stoppingDistance = 1.2f;
        agent.radius = 0.55f;
        agent.height = 2.35f;

        EnemyChaseAttack chaseAttack = root.GetComponent<EnemyChaseAttack>();
        if (chaseAttack == null)
        {
            chaseAttack = root.AddComponent<EnemyChaseAttack>();
        }

        Component visualFx = EnsureRuntimeComponent(root, "CubeEnemyVisualFx");

        GameObject visualRootObject = FindOrCreateChild(root.transform, "Visual Root");
        visualRootObject.transform.localPosition = Vector3.zero;
        visualRootObject.transform.localRotation = Quaternion.identity;
        visualRootObject.transform.localScale = Vector3.one;

        SerializedObject healthObject = new SerializedObject(health);
        healthObject.FindProperty("maxHealth").floatValue = 100f;
        healthObject.FindProperty("destroyOnDeath").boolValue = false;
        healthObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject body = CreateCubePart(visualRootObject.transform, "Body Hitbox", new Vector3(0f, 1.05f, 0f), new Vector3(1.05f, 1.45f, 0.55f), bodyMaterial);
        ConfigureHitbox(body, health, 1f, false);

        GameObject head = CreateCubePart(visualRootObject.transform, "Head Critical Hitbox", new Vector3(0f, 2.05f, 0f), new Vector3(0.72f, 0.62f, 0.62f), headMaterial);
        ConfigureHitbox(head, health, 2f, true);

        CreateCubePart(visualRootObject.transform, "Chest Cyan Armor Plate", new Vector3(0f, 1.18f, -0.295f), new Vector3(0.82f, 0.58f, 0.06f), armorMaterial, false);
        CreateCubePart(visualRootObject.transform, "Lower Armor Belt", new Vector3(0f, 0.52f, -0.305f), new Vector3(1.12f, 0.16f, 0.07f), armorMaterial, false);
        CreateCubePart(visualRootObject.transform, "Left Shoulder Pad", new Vector3(-0.68f, 1.55f, 0f), new Vector3(0.28f, 0.38f, 0.68f), armorMaterial, false);
        CreateCubePart(visualRootObject.transform, "Right Shoulder Pad", new Vector3(0.68f, 1.55f, 0f), new Vector3(0.28f, 0.38f, 0.68f), armorMaterial, false);
        CreateCubePart(visualRootObject.transform, "Magenta Visor", new Vector3(0f, 2.09f, -0.335f), new Vector3(0.56f, 0.16f, 0.07f), visorMaterial, false);
        CreateCubePart(visualRootObject.transform, "Orange Core Weak Glow", new Vector3(0f, 1.18f, -0.345f), new Vector3(0.28f, 0.28f, 0.08f), weakPointMaterial, false);

        GameObject firePointObject = FindOrCreateChild(root.transform, "Fire Point");
        firePointObject.transform.localPosition = new Vector3(0f, 1.78f, -0.58f);
        firePointObject.transform.localRotation = Quaternion.identity;

        ParticleSystem fireChargeFx = CreateFireChargeFx(visualRootObject.transform);

        Renderer[] flashRenderers = root.GetComponentsInChildren<Renderer>(true);
        if (visualFx != null)
        {
            SerializedObject visualFxObject = new SerializedObject(visualFx);
            visualFxObject.FindProperty("visualRoot").objectReferenceValue = visualRootObject.transform;
            visualFxObject.FindProperty("rollingParts").arraySize = 2;
            visualFxObject.FindProperty("rollingParts").GetArrayElementAtIndex(0).objectReferenceValue = body.transform;
            visualFxObject.FindProperty("rollingParts").GetArrayElementAtIndex(1).objectReferenceValue = head.transform;
            visualFxObject.FindProperty("flashRenderers").arraySize = flashRenderers.Length;
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                visualFxObject.FindProperty("flashRenderers").GetArrayElementAtIndex(i).objectReferenceValue = flashRenderers[i];
            }
            visualFxObject.FindProperty("fireChargeFx").objectReferenceValue = fireChargeFx;
            visualFxObject.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedObject chaseObject = new SerializedObject(chaseAttack);
        chaseObject.FindProperty("firePoint").objectReferenceValue = firePointObject.transform;
        chaseObject.FindProperty("eyeOffset").vector3Value = new Vector3(0f, 2.12f, 0f);
        chaseObject.FindProperty("targetAimOffset").vector3Value = new Vector3(0f, 1.2f, 0f);
        chaseObject.FindProperty("attackDamage").floatValue = 8f;
        chaseObject.FindProperty("fireRate").floatValue = 1.65f;
        chaseObject.FindProperty("detectionRadius").floatValue = 26f;
        chaseObject.FindProperty("preferredCombatDistance").floatValue = 11f;
        chaseObject.FindProperty("tooCloseDistance").floatValue = 5f;
        chaseObject.FindProperty("cubeVisualFx").objectReferenceValue = visualFx;
        chaseObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        Debug.Log($"Cube Enemy Prototype created/updated at {root.transform.position}. Body = normal hit, Head = critical x2 hitbox.");
    }

    private static Vector3 GetSpawnPosition()
    {
        Camera sceneCamera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
        if (sceneCamera != null)
        {
            Vector3 forward = sceneCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
            {
                return sceneCamera.transform.position + forward.normalized * 8f;
            }
        }

        AdvancedFpsController player = Object.FindAnyObjectByType<AdvancedFpsController>();
        if (player != null)
        {
            return player.transform.position + player.transform.forward * 12f;
        }

        return new Vector3(0f, 0f, 8f);
    }

    private static GameObject CreateCubePart(Transform parent, string partName, Vector3 localPosition, Vector3 localScale, Material material, bool colliderEnabled = true)
    {
        Transform existing = parent.Find(partName);
        GameObject part = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;

        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = colliderEnabled;
        }

        return part;
    }

    private static GameObject FindOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static Component EnsureRuntimeComponent(GameObject target, string typeName)
    {
        System.Type type = System.Type.GetType($"{typeName}, Assembly-CSharp");
        if (type == null)
        {
            Debug.LogWarning($"Could not find runtime component type: {typeName}. Unity may need to finish compiling scripts first.");
            return null;
        }

        Component component = target.GetComponent(type);
        if (component == null)
        {
            component = target.AddComponent(type);
        }

        return component;
    }

    private static ParticleSystem CreateFireChargeFx(Transform parent)
    {
        GameObject fxObject = FindOrCreateChild(parent, "Golden Fire Charge FX");
        fxObject.transform.localPosition = new Vector3(0f, 1.25f, -0.48f);
        fxObject.transform.localRotation = Quaternion.identity;

        ParticleSystem particles = fxObject.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = fxObject.AddComponent<ParticleSystem>();
        }

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.22f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.08f, 0.95f), new Color(1f, 0.32f, 0.02f, 0.85f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.18f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        return particles;
    }

    private static void ConfigureHitbox(GameObject part, Health health, float multiplier, bool critical)
    {
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = true;
        }

        DamageHitbox hitbox = part.GetComponent<DamageHitbox>();
        if (hitbox == null)
        {
            hitbox = part.AddComponent<DamageHitbox>();
        }

        SerializedObject hitboxObject = new SerializedObject(hitbox);
        hitboxObject.FindProperty("damageableRoot").objectReferenceValue = health;
        hitboxObject.FindProperty("damageMultiplier").floatValue = multiplier;
        hitboxObject.FindProperty("criticalHitbox").boolValue = critical;
        hitboxObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Prototypes");
        }
    }

    private static Material CreateOrLoadMaterial(string materialName, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        EditorUtility.SetDirty(material);
        return material;
    }
}