using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class ManagerCheckSceneBuilder
{
    private const string PlaceholderAssetPath = "Assets/Resources/Debug/TestPropertyDatabaseCheckPlaceholder.asset";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string PaperBoxPrefabPath = "Assets/Cardboard Box (Rigged)/Prefabs/Cardboard Box (Closed).prefab";

    [MenuItem("Tools/Undefined/Create Manager Check Scene")]
    public static void CreateManagerCheckScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject player = CreatePlayerRig(out Camera mainCamera, out CharacterController controller);
        EnsureDirectionalLight();

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;

        GameObject paperBox = CreatePaperBoxSource(new Vector3(-1.5f, 0f, 4f));
        GameObject heavySphere = CreateHeavySphereTarget(new Vector3(1.5f, 0.75f, 4f));

        GameObject managers = new GameObject("Managers");
        InputManager inputManager = player.AddComponent<InputManager>();
        InteractionManager interactionManager = managers.AddComponent<InteractionManager>();
        TransferSystem transferSystem = managers.AddComponent<TransferSystem>();
        ManagerDebugCheckLogger logger = managers.AddComponent<ManagerDebugCheckLogger>();
        ManagerSpecRuntimeHUD hud = managers.AddComponent<ManagerSpecRuntimeHUD>();

        ConfigureInputManager(inputManager, controller, mainCamera);
        ConfigureInteractionManager(interactionManager, inputManager);
        ConfigureTransferSystem(transferSystem, inputManager, interactionManager);
        ConfigureLogger(logger, inputManager, interactionManager, transferSystem);
        ConfigureHud(hud, inputManager, interactionManager, transferSystem);

        Selection.activeGameObject = managers;
        EditorSceneManager.MarkSceneDirty(managers.scene);

        Debug.Log("[CHECK_SETUP] Manager check scene created. Press Play, click Game view, test WASD/Mouse/C/LCtrl/Space/E/F+Drag/LMB/RMB.");
    }

    private static GameObject CreatePlayerRig(out Camera camera, out CharacterController controller)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        Object.DestroyImmediate(player.GetComponent<Collider>());
        controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.radius = 0.3f;
        player.transform.position = new Vector3(0f, 0f, -4f);

        Transform cameraRoot = new GameObject("CameraRoot").transform;
        cameraRoot.SetParent(player.transform);
        cameraRoot.localPosition = Vector3.zero;
        cameraRoot.localRotation = Quaternion.identity;

        GameObject cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        camera = cameraGo.AddComponent<Camera>();
        cameraGo.AddComponent<AudioListener>();
        cameraGo.transform.SetParent(cameraRoot);
        cameraGo.transform.localPosition = Vector3.zero;
        cameraGo.transform.localRotation = Quaternion.identity;

        camera.transform.localPosition = Vector3.zero;
        camera.transform.localRotation = Quaternion.identity;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1000f;
        camera.gameObject.tag = "MainCamera";

        return player;
    }

    private static void EnsureDirectionalLight()
    {
        Light existing = Object.FindFirstObjectByType<Light>();
        if (existing != null && existing.type == LightType.Directional)
        {
            return;
        }

        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void ConfigureInputManager(InputManager inputManager, CharacterController controller, Camera camera)
    {
        SerializedObject so = new SerializedObject(inputManager);
#if ENABLE_INPUT_SYSTEM
        InputActionAsset actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

        if (actionAsset != null)
        {
            so.FindProperty("inputActionsAsset").objectReferenceValue = actionAsset;
        }
        else
        {
            Debug.LogWarning("[CHECK_SETUP] InputActionAsset not found at Assets/InputSystem_Actions.inputactions. Assign it manually on InputManager.");
        }
#endif
        so.FindProperty("characterController").objectReferenceValue = controller;
        so.FindProperty("playerTransform").objectReferenceValue = inputManager.transform;
        so.FindProperty("playerCamera").objectReferenceValue = camera;
        so.FindProperty("cameraFieldOfView").floatValue = 95f;
        so.FindProperty("autoFindPlayerComponents").boolValue = true;
        so.FindProperty("mouseSensitivity").floatValue = 180f;
        so.FindProperty("standControllerHeight").floatValue = 1.8f;
        so.FindProperty("crouchControllerHeight").floatValue = 1.2f;
        so.FindProperty("proneControllerHeight").floatValue = 0.7f;
        so.FindProperty("standCameraLocalY").floatValue = 1.55f;
        so.FindProperty("crouchCameraLocalY").floatValue = 0.95f;
        so.FindProperty("proneCameraLocalY").floatValue = 0.35f;
        so.FindProperty("crouchSpeedMultiplier").floatValue = 0.5f;
        so.FindProperty("proneSpeedMultiplier").floatValue = 0.3f;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureInteractionManager(InteractionManager interactionManager, InputManager inputManager)
    {
        SerializedObject so = new SerializedObject(interactionManager);
        so.FindProperty("inputManager").objectReferenceValue = inputManager;
        so.FindProperty("interactionDistance").floatValue = 5f;
        so.FindProperty("holdDistanceFromCamera").floatValue = 2.8f;
        so.FindProperty("minHoldDistance").floatValue = 2.2f;
        so.FindProperty("maxHoldDistance").floatValue = 4.2f;
        so.FindProperty("dragThreshold").floatValue = 0.5f;
        so.FindProperty("raycastInterval").floatValue = 0f;
        so.FindProperty("interactableLayer").intValue = ~0;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureTransferSystem(TransferSystem transferSystem, InputManager inputManager, InteractionManager interactionManager)
    {
        PropertyDatabaseCheckPlaceholder placeholder = EnsurePlaceholderAsset();

        SerializedObject so = new SerializedObject(transferSystem);
        so.FindProperty("inputManager").objectReferenceValue = inputManager;
        so.FindProperty("interactionManager").objectReferenceValue = interactionManager;
        so.FindProperty("propertyDatabase").objectReferenceValue = placeholder;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureLogger(ManagerDebugCheckLogger logger, InputManager inputManager, InteractionManager interactionManager, TransferSystem transferSystem)
    {
        SerializedObject so = new SerializedObject(logger);
        so.FindProperty("inputManager").objectReferenceValue = inputManager;
        so.FindProperty("interactionManager").objectReferenceValue = interactionManager;
        so.FindProperty("transferSystem").objectReferenceValue = transferSystem;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureHud(ManagerSpecRuntimeHUD hud, InputManager inputManager, InteractionManager interactionManager, TransferSystem transferSystem)
    {
        SerializedObject so = new SerializedObject(hud);
        so.FindProperty("inputManager").objectReferenceValue = inputManager;
        so.FindProperty("interactionManager").objectReferenceValue = interactionManager;
        so.FindProperty("transferSystem").objectReferenceValue = transferSystem;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateTransferTestCube(string objectName, Vector3 position, string promptText, Color baseColor)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.position = position;
        cube.transform.localScale = Vector3.one;

        DebugInteractableCheckTarget target = cube.AddComponent<DebugInteractableCheckTarget>();

        SerializedObject so = new SerializedObject(target);
        so.FindProperty("holdenable").boolValue = false;
        so.FindProperty("PromptText").stringValue = promptText;
        so.FindProperty("CanReceiveInjectedProperty").boolValue = true;
        so.FindProperty("IsDestructible").boolValue = true;
        so.FindProperty("IsInjectedProperty").boolValue = false;
        so.FindProperty("CurrentPropertyVisual").stringValue = "Default";
        so.FindProperty("baseColor").colorValue = baseColor;
        so.ApplyModifiedPropertiesWithoutUndo();

        return cube;
    }

    private static GameObject CreatePaperBoxSource(Vector3 position)
    {
        GameObject instance = TryInstantiatePaperBoxPrefab();
        if (instance == null)
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = "PaperBoxSource_Fallback";
            instance.transform.localScale = new Vector3(1f, 0.8f, 1f);
        }
        else
        {
            instance.name = "PaperBoxSource";
        }

        EnsureDynamicColliderCompatibility(instance);
        PlaceAboveGround(instance, position);

        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = instance.AddComponent<Rigidbody>();
        }
        rb.mass = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        DebugInteractableCheckTarget target = instance.GetComponent<DebugInteractableCheckTarget>();
        if (target == null)
        {
            target = instance.AddComponent<DebugInteractableCheckTarget>();
        }

        SerializedObject so = new SerializedObject(target);
        so.FindProperty("holdenable").boolValue = true;
        so.FindProperty("PromptText").stringValue = "E - 종이박스 들기 (가벼움 추출 가능)";
        so.FindProperty("ExtractStaticPropertyId").stringValue = "static:lightweight";
        so.FindProperty("ExtractDynamicPropertyId").stringValue = "dynamic:paper_motion";
        so.FindProperty("ExtractStaticDisplayName").stringValue = "가벼움";
        so.FindProperty("ExtractDynamicDisplayName").stringValue = "종이 흔들림";
        so.FindProperty("baseColor").colorValue = new Color(0.82f, 0.67f, 0.45f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();

        return instance;
    }

    private static GameObject CreateHeavySphereTarget(Vector3 position)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "HeavySphereTarget";
        sphere.transform.position = position;
        sphere.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

        Rigidbody rb = sphere.AddComponent<Rigidbody>();
        rb.mass = 30f;

        DebugInteractableCheckTarget target = sphere.AddComponent<DebugInteractableCheckTarget>();
        SerializedObject so = new SerializedObject(target);
        so.FindProperty("holdenable").boolValue = false;
        so.FindProperty("PromptText").stringValue = "E - 무거운 구 (기본 들기 불가)";
        so.FindProperty("ExtractStaticPropertyId").stringValue = "static:heavy";
        so.FindProperty("ExtractDynamicPropertyId").stringValue = "dynamic:inertia";
        so.FindProperty("ExtractStaticDisplayName").stringValue = "무거움";
        so.FindProperty("ExtractDynamicDisplayName").stringValue = "관성";
        so.FindProperty("requireKeywordForHoldableUnlock").boolValue = true;
        so.FindProperty("UnlockHoldableOnInjectKeyword").stringValue = "lightweight";
        so.FindProperty("revertHoldableOnExpire").boolValue = true;
        so.FindProperty("injectedPromptOverride").stringValue = "E - 무거운 구 (가벼움 주입됨, 들기 가능)";
        so.FindProperty("baseColor").colorValue = new Color(0.35f, 0.35f, 0.4f, 1f);
        so.FindProperty("injectedColor").colorValue = new Color(0.25f, 0.9f, 0.6f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();

        return sphere;
    }

    private static GameObject TryInstantiatePaperBoxPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PaperBoxPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[CHECK_SETUP] Paper box prefab not found. Using fallback cube source.");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        return instance;
    }

    private static void EnsureDynamicColliderCompatibility(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        if (colliders == null || colliders.Length == 0)
        {
            AddFallbackBoxCollider(root);
            return;
        }

        bool hasUsableCollider = false;
        foreach (Collider collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            collider.enabled = true;
            hasUsableCollider = true;

            MeshCollider meshCollider = collider as MeshCollider;
            if (meshCollider != null)
            {
                // Dynamic rigidbodies require convex mesh colliders.
                meshCollider.convex = true;
            }
        }

        if (!hasUsableCollider)
        {
            AddFallbackBoxCollider(root);
        }
    }

    private static void AddFallbackBoxCollider(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            root.AddComponent<BoxCollider>();
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        BoxCollider box = root.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = root.AddComponent<BoxCollider>();
        }

        box.center = root.transform.InverseTransformPoint(bounds.center);
        box.size = root.transform.InverseTransformVector(bounds.size);
    }

    private static void PlaceAboveGround(GameObject instance, Vector3 desiredXZPosition)
    {
        instance.transform.position = desiredXZPosition;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            instance.transform.position = desiredXZPosition + new Vector3(0f, 0.5f, 0f);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float groundY = 0f;
        float lift = (groundY - bounds.min.y) + 0.02f;
        instance.transform.position += new Vector3(0f, lift, 0f);
    }

    private static PropertyDatabaseCheckPlaceholder EnsurePlaceholderAsset()
    {
        PropertyDatabaseCheckPlaceholder asset = AssetDatabase.LoadAssetAtPath<PropertyDatabaseCheckPlaceholder>(PlaceholderAssetPath);
        if (asset != null)
        {
            return asset;
        }

        string directory = Path.GetDirectoryName(PlaceholderAssetPath);
        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
        {
            EnsureFolderRecursive(directory);
        }

        asset = ScriptableObject.CreateInstance<PropertyDatabaseCheckPlaceholder>();
        AssetDatabase.CreateAsset(asset, PlaceholderAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return asset;
    }

    private static void EnsureFolderRecursive(string targetFolder)
    {
        string normalized = targetFolder.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
