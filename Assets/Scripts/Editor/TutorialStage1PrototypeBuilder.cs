using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class TutorialStage1PrototypeBuilder
{
    private const string PlaceholderAssetPath = "Assets/Resources/Debug/TestPropertyDatabaseCheckPlaceholder.asset";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string PaperBoxPrefabPath = "Assets/Cardboard Box (Rigged)/Prefabs/Cardboard Box (Closed).prefab";

    private const float RoomWidth = 24f;
    private const float RoomLength = 18f;
    private const float RoomHeight = 7f;
    private const float HighFloorTopY = 1.6f;
    private const float FloorThickness = 0.2f;
    private const float WallThickness = 0.2f;

    [MenuItem("Tools/Undefined/Create Tutorial Stage1 Prototype")]
    public static void CreateTutorialStage1Prototype()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject player = CreatePlayerRig(out Camera mainCamera, out CharacterController controller);
        EnsureDirectionalLight();
        BuildRoomGeometry();

        GameObject managers = CreateManagers(player, controller, mainCamera, out InputManager inputManager, out InteractionManager interactionManager, out TransferSystem transferSystem);

        CreateDoorWithKeypad(new Vector3(0f, HighFloorTopY, (RoomLength * 0.5f) - (WallThickness * 0.5f)));
        CreateSafe(new Vector3(-(RoomWidth * 0.5f) + 1.6f, HighFloorTopY + 0.8f, (RoomLength * 0.5f) - 1.6f));
        CreatePaperBox(new Vector3(-3.8f, 0f, -5.8f));
        TutorialStage1Seesaw seesaw = CreateSeesaw(new Vector3(6.0f, 0f, -4.8f));
        CreateWindow(new Vector3((RoomWidth * 0.5f) - (WallThickness * 0.5f), 5.5f, -4.8f), seesaw);

        Selection.activeGameObject = managers;
        EditorSceneManager.MarkSceneDirty(managers.scene);

        Debug.Log("[TUTORIAL_SETUP] Tutorial Stage1 prototype scene created.");
    }

    private static GameObject CreateManagers(GameObject player, CharacterController controller, Camera camera, out InputManager inputManager, out InteractionManager interactionManager, out TransferSystem transferSystem)
    {
        GameObject managers = new GameObject("Managers");
        inputManager = player.AddComponent<InputManager>();
        interactionManager = managers.AddComponent<InteractionManager>();
        transferSystem = managers.AddComponent<TransferSystem>();
        ManagerDebugCheckLogger logger = managers.AddComponent<ManagerDebugCheckLogger>();
        ManagerSpecRuntimeHUD hud = managers.AddComponent<ManagerSpecRuntimeHUD>();
        SimpleCrosshairOverlay crosshair = managers.AddComponent<SimpleCrosshairOverlay>();

        ConfigureInputManager(inputManager, controller, camera);
        ConfigureInteractionManager(interactionManager, inputManager);
        ConfigureTransferSystem(transferSystem, inputManager, interactionManager);
        ConfigureLogger(logger, inputManager, interactionManager, transferSystem);
        ConfigureHud(hud, inputManager, interactionManager, transferSystem);

        SerializedObject hudSo = new SerializedObject(hud);
        SerializedProperty hudCrosshair = hudSo.FindProperty("showCrosshair");
        if (hudCrosshair != null)
        {
            hudCrosshair.boolValue = true;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedObject crosshairSo = new SerializedObject(crosshair);
        SerializedProperty crosshairSize = crosshairSo.FindProperty("crosshairSize");
        if (crosshairSize != null)
        {
            crosshairSize.intValue = 44;
        }
        SerializedProperty crosshairThickness = crosshairSo.FindProperty("lineThickness");
        if (crosshairThickness != null)
        {
            crosshairThickness.intValue = 5;
        }
        SerializedProperty crosshairGap = crosshairSo.FindProperty("gap");
        if (crosshairGap != null)
        {
            crosshairGap.intValue = 7;
        }
        crosshairSo.ApplyModifiedPropertiesWithoutUndo();

        return managers;
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

        player.transform.position = new Vector3(0f, 0f, 0f);
        player.transform.rotation = Quaternion.identity;

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
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;

        return player;
    }

    private static void BuildRoomGeometry()
    {
        float halfW = RoomWidth * 0.5f;
        float halfL = RoomLength * 0.5f;

        CreateCube("Floor_Low", new Vector3(0f, -FloorThickness * 0.5f, -halfL * 0.5f), new Vector3(RoomWidth, FloorThickness, RoomLength * 0.5f), new Color(0.32f, 0.34f, 0.36f, 1f));
        CreateCube("Floor_High", new Vector3(0f, HighFloorTopY - (FloorThickness * 0.5f), halfL * 0.5f), new Vector3(RoomWidth, FloorThickness, RoomLength * 0.5f), new Color(0.38f, 0.40f, 0.42f, 1f));
        CreateCube("Floor_SeamStep", new Vector3(0f, HighFloorTopY * 0.5f, 0f), new Vector3(RoomWidth, HighFloorTopY, 0.08f), new Color(0.28f, 0.30f, 0.32f, 1f));
        CreateCube("Ceiling", new Vector3(0f, RoomHeight + (FloorThickness * 0.5f), 0f), new Vector3(RoomWidth, FloorThickness, RoomLength), new Color(0.45f, 0.46f, 0.48f, 1f));

        CreateCube("Wall_North", new Vector3(0f, RoomHeight * 0.5f, halfL), new Vector3(RoomWidth, RoomHeight, WallThickness), new Color(0.72f, 0.74f, 0.78f, 1f));
        CreateCube("Wall_South", new Vector3(0f, RoomHeight * 0.5f, -halfL), new Vector3(RoomWidth, RoomHeight, WallThickness), new Color(0.72f, 0.74f, 0.78f, 1f));
        CreateCube("Wall_East", new Vector3(halfW, RoomHeight * 0.5f, 0f), new Vector3(WallThickness, RoomHeight, RoomLength), new Color(0.72f, 0.74f, 0.78f, 1f));
        CreateCube("Wall_West", new Vector3(-halfW, RoomHeight * 0.5f, 0f), new Vector3(WallThickness, RoomHeight, RoomLength), new Color(0.72f, 0.74f, 0.78f, 1f));
    }

    private static TutorialStage1DoorKeypad CreateDoorWithKeypad(Vector3 centerPos)
    {
        GameObject doorRoot = new GameObject("DoorWithKeypad");
        doorRoot.transform.position = centerPos;

        GameObject doorPanel = CreateCube("DoorPanel", centerPos + new Vector3(0f, 1.1f, -0.08f), new Vector3(1.4f, 2.2f, 0.12f), new Color(0.25f, 0.30f, 0.36f, 1f));
        doorPanel.transform.SetParent(doorRoot.transform, true);

        GameObject keypadTerminal = CreateCube("KeypadTerminal", centerPos + new Vector3(0.52f, 1.18f, -0.2f), new Vector3(0.28f, 0.42f, 0.05f), new Color(0.10f, 0.10f, 0.12f, 1f));
        keypadTerminal.transform.SetParent(doorRoot.transform, true);

        GameObject displayObj = new GameObject("DoorLockStatus");
        displayObj.transform.SetParent(doorRoot.transform, true);
        displayObj.transform.position = centerPos + new Vector3(0.0f, 1.74f, -0.18f);
        TextMesh statusText = displayObj.AddComponent<TextMesh>();
        statusText.text = "CLOSE";
        statusText.fontSize = 110;
        statusText.characterSize = 0.03f;
        statusText.anchor = TextAnchor.MiddleCenter;
        statusText.alignment = TextAlignment.Center;
        statusText.color = new Color(1f, 0.4f, 0.35f, 1f);

        TutorialStage1DoorKeypad keypad = doorRoot.AddComponent<TutorialStage1DoorKeypad>();
        SerializedObject keypadSo = new SerializedObject(keypad);
        SetObjectRef(keypadSo, "doorPanel", doorPanel.transform);
        SetString(keypadSo, "correctCode", "1234");
        SetObjectRef(keypadSo, "statusText", statusText);
        SetInt(keypadSo, "maxInputLength", 4);
        SetBool(keypadSo, "autoClearOnFailure", true);
        keypadSo.ApplyModifiedPropertiesWithoutUndo();

        TutorialStage1DoorKeypadTerminal terminal = keypadTerminal.AddComponent<TutorialStage1DoorKeypadTerminal>();
        SerializedObject terminalSo = new SerializedObject(terminal);
        SetObjectRef(terminalSo, "doorKeypad", keypad);
        terminalSo.ApplyModifiedPropertiesWithoutUndo();

        return keypad;
    }

    private static void CreateSafe(Vector3 pos)
    {
        GameObject safe = CreateCube("Safe", pos, new Vector3(1.0f, 1.6f, 0.8f), new Color(0.24f, 0.27f, 0.32f, 1f));
        TutorialStage1Safe safeLogic = safe.AddComponent<TutorialStage1Safe>();
        Rigidbody safeRb = safe.AddComponent<Rigidbody>();
        safeRb.mass = 8f;
        safeRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        safeRb.interpolation = RigidbodyInterpolation.Interpolate;
        GameObject noteInSafe = CreateCube("Safe_Note", pos + new Vector3(0f, 0.35f, 0.05f), new Vector3(0.26f, 0.14f, 0.02f), new Color(0.95f, 0.92f, 0.75f, 1f));
        noteInSafe.transform.SetParent(safe.transform, true);
        noteInSafe.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        GameObject codeText = new GameObject("Safe_Note_CodeText");
        codeText.transform.SetParent(noteInSafe.transform, false);
        codeText.transform.localPosition = new Vector3(0f, 0f, -0.012f);
        TextMesh textMesh = codeText.AddComponent<TextMesh>();
        textMesh.text = "1234";
        textMesh.fontSize = 140;
        textMesh.characterSize = 0.03f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        noteInSafe.SetActive(false);

        DebugInteractableCheckTarget debug = safe.AddComponent<DebugInteractableCheckTarget>();
        debug.holdenable = false;
        debug.holdableState = 0;
        debug.PromptText = "E - Safe";
        debug.ExtractStaticPropertyId = "static:heavy";
        debug.ExtractStaticDisplayName = "Heavy";
        debug.CanReceiveInjectedProperty = true;
        debug.IsDestructible = true;

        ObjectPropertySet prop = safe.AddComponent<ObjectPropertySet>();
        SerializedObject so = new SerializedObject(prop);
        SetString(so, "baseStaticPropertyId", "static:heavy");
        SetInt(so, "defaultHoldableState", 0);
        SetInt(so, "holdableState", 0);
        SetString(so, "PromptText", "E - Safe");
        so.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject safeSo = new SerializedObject(safeLogic);
        SetObjectRef(safeSo, "hiddenNoteObject", noteInSafe);
        safeSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreatePaperBox(Vector3 basePos)
    {
        GameObject box = TryInstantiatePaperBoxPrefab();
        if (box == null)
        {
            box = CreateCube("SafeBox", basePos + new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), new Color(0.82f, 0.67f, 0.45f, 1f));
        }
        else
        {
            box.name = "SafeBox";
            EnsureDynamicColliderCompatibility(box);
            PlaceAboveGround(box, basePos);
        }

        if (box.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = box.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        DebugInteractableCheckTarget debug = box.GetComponent<DebugInteractableCheckTarget>();
        if (debug == null)
        {
            debug = box.AddComponent<DebugInteractableCheckTarget>();
        }
        debug.holdenable = true;
        debug.holdableState = 1;
        debug.PromptText = "E - Pick up box";
        debug.ExtractStaticPropertyId = "static:lightweight";
        debug.ExtractStaticDisplayName = "Lightweight";
        debug.CanReceiveInjectedProperty = true;

        ObjectPropertySet prop = box.GetComponent<ObjectPropertySet>();
        if (prop == null)
        {
            prop = box.AddComponent<ObjectPropertySet>();
        }
        SerializedObject so = new SerializedObject(prop);
        SetString(so, "baseStaticPropertyId", "static:lightweight");
        SetInt(so, "defaultHoldableState", 1);
        SetInt(so, "holdableState", 1);
        SetString(so, "PromptText", "E - Pick up box");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TutorialStage1Seesaw CreateSeesaw(Vector3 pos)
    {
        GameObject root = new GameObject("Seesaw");
        root.transform.position = pos;
        TutorialStage1Seesaw seesaw = root.AddComponent<TutorialStage1Seesaw>();

        // Lever geometry (physical intent):
        // - Fulcrum fixed to floor.
        // - At target tilt angle, one end nearly touches floor and the opposite end rises.
        float boardLength = 7.2f;
        float boardThickness = 0.16f;
        float targetTiltDeg = 35f;
        float halfLength = boardLength * 0.5f;
        float centerYWhenEndTouchesFloor =
            (halfLength * Mathf.Sin(targetTiltDeg * Mathf.Deg2Rad)) + (boardThickness * 0.5f);

        GameObject pivot = CreateCube("SeesawPivot", Vector3.zero, new Vector3(0.28f, centerYWhenEndTouchesFloor, 0.28f), new Color(0.35f, 0.28f, 0.22f, 1f));
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = new Vector3(0f, centerYWhenEndTouchesFloor * 0.5f, 0f);

        GameObject board = CreateCube("SeesawBoard", Vector3.zero, new Vector3(boardLength, boardThickness, 0.8f), new Color(0.55f, 0.43f, 0.28f, 1f));
        board.transform.SetParent(root.transform, false);
        board.transform.localPosition = new Vector3(0f, centerYWhenEndTouchesFloor, 0f);

        SerializedObject seesawSo = new SerializedObject(seesaw);
        SetObjectRef(seesawSo, "board", board.transform);
        SetFloat(seesawSo, "maxTiltAngleZ", targetTiltDeg);
        SetFloat(seesawSo, "loadToTorque", 110f);
        SetFloat(seesawSo, "restoreStrength", 6f);
        SetFloat(seesawSo, "angularDamping", 6f);
        seesawSo.ApplyModifiedPropertiesWithoutUndo();

        CreateSeesawSideTrigger(root.transform, seesaw, "SeesawWeightTrigger_Left", SeesawSide.Left, new Vector3(-(halfLength - 0.55f), centerYWhenEndTouchesFloor + 0.2f, 0f));
        CreateSeesawSideTrigger(root.transform, seesaw, "SeesawWeightTrigger_Right", SeesawSide.Right, new Vector3((halfLength - 0.55f), centerYWhenEndTouchesFloor + 0.2f, 0f));

        // Helper step for boarding seesaw.
        // Keep low enough so step + jump cannot reach window directly.
        GameObject helperStep = CreateCube("Seesaw_HelperStep", Vector3.zero, new Vector3(1.2f, 0.45f, 1.2f), new Color(0.42f, 0.42f, 0.46f, 1f));
        helperStep.transform.SetParent(root.transform, false);
        helperStep.transform.localPosition = new Vector3(-(halfLength - 0.85f), 0.225f, -0.95f);

        return seesaw;
    }

    private static void CreateSeesawSideTrigger(Transform parent, TutorialStage1Seesaw seesaw, string name, SeesawSide side, Vector3 localPos)
    {
        GameObject trigger = new GameObject(name);
        trigger.transform.SetParent(parent, false);
        trigger.transform.localPosition = localPos;

        BoxCollider triggerCol = trigger.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(2.4f, 1.2f, 1.2f);

        TutorialStage1SeesawWeightTrigger triggerScript = trigger.AddComponent<TutorialStage1SeesawWeightTrigger>();
        SerializedObject triggerSo = new SerializedObject(triggerScript);
        SetObjectRef(triggerSo, "seesaw", seesaw);
        SetInt(triggerSo, "side", (int)side);
        triggerSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateWindow(Vector3 pos, TutorialStage1Seesaw seesaw)
    {
        // Keep window above simple stack height (safe + box), but reachable via seesaw high side.
        GameObject window = CreateCube("Window", pos + new Vector3(-0.08f, 0f, 0f), new Vector3(0.12f, 1.2f, 1.3f), new Color(0.45f, 0.75f, 0.9f, 0.85f));
        DebugInteractableCheckTarget debug = window.AddComponent<DebugInteractableCheckTarget>();
        debug.holdenable = false;
        debug.holdableState = 0;
        debug.PromptText = "E - Window";
        debug.ExtractStaticPropertyId = "static:transparent";
        debug.ExtractStaticDisplayName = "Transparent";
        debug.CanReceiveInjectedProperty = false;

        ObjectPropertySet prop = window.AddComponent<ObjectPropertySet>();
        SerializedObject so = new SerializedObject(prop);
        SetString(so, "baseStaticPropertyId", "static:transparent");
        SetInt(so, "defaultHoldableState", 0);
        SetInt(so, "holdableState", 0);
        SetString(so, "PromptText", "E - Window");
        SetBool(so, "CanReceiveInjectedProperty", false);
        so.ApplyModifiedPropertiesWithoutUndo();

        TutorialStage1WindowTransferGate gate = window.AddComponent<TutorialStage1WindowTransferGate>();
        SerializedObject gateSo = new SerializedObject(gate);
        SetObjectRef(gateSo, "seesaw", seesaw);
        SetFloat(gateSo, "requiredTiltNormalized", 0.9f);
        SetBool(gateSo, "requirePositiveTilt", true);
        gateSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateCube(string name, Vector3 center, Vector3 size, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = center;
        cube.transform.localScale = size;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material source = renderer.sharedMaterial;
            if (source != null)
            {
                Material mat = new Material(source);
                mat.color = color;
                renderer.sharedMaterial = mat;
            }
        }

        return cube;
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
            SetObjectRef(so, "inputActionsAsset", actionAsset);
        }
#endif
        SetObjectRef(so, "characterController", controller);
        SetObjectRef(so, "playerTransform", inputManager.transform);
        SetObjectRef(so, "playerCamera", camera);
        SetFloat(so, "cameraFieldOfView", 95f);
        SetBool(so, "autoFindPlayerComponents", true);
        SetFloat(so, "mouseSensitivity", 180f);
        SetFloat(so, "jumpForce", 8.5f);
        SetFloat(so, "standControllerHeight", 1.8f);
        SetFloat(so, "crouchControllerHeight", 1.2f);
        SetFloat(so, "proneControllerHeight", 0.7f);
        SetFloat(so, "standCameraLocalY", 1.55f);
        SetFloat(so, "crouchCameraLocalY", 0.95f);
        SetFloat(so, "proneCameraLocalY", 0.35f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureInteractionManager(InteractionManager interactionManager, InputManager inputManager)
    {
        SerializedObject so = new SerializedObject(interactionManager);
        SetObjectRef(so, "inputManager", inputManager);
        SetFloat(so, "interactionDistance", 6f);
        SetBool(so, "forceOverlayFromInteractionManager", true);
        SetFloat(so, "holdDistanceFromCamera", 2.8f);
        SetFloat(so, "minHoldDistance", 2.2f);
        SetFloat(so, "maxHoldDistance", 4.2f);
        SetFloat(so, "dragThreshold", 0.18f);
        SetFloat(so, "raycastInterval", 0f);
        SetInt(so, "interactableLayer", ~0);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureTransferSystem(TransferSystem transferSystem, InputManager inputManager, InteractionManager interactionManager)
    {
        PropertyDatabaseCheckPlaceholder placeholder = EnsurePlaceholderAsset();
        SerializedObject so = new SerializedObject(transferSystem);
        SetObjectRef(so, "inputManager", inputManager);
        SetObjectRef(so, "interactionManager", interactionManager);
        SetObjectRef(so, "propertyDatabase", placeholder);
        SetFloat(so, "defaultExtractTime", 1.5f);
        SetFloat(so, "defaultInjectTime", 1.5f);
        SetFloat(so, "defaultInjectedDuration", 30f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureLogger(ManagerDebugCheckLogger logger, InputManager inputManager, InteractionManager interactionManager, TransferSystem transferSystem)
    {
        SerializedObject so = new SerializedObject(logger);
        SetObjectRef(so, "inputManager", inputManager);
        SetObjectRef(so, "interactionManager", interactionManager);
        SetObjectRef(so, "transferSystem", transferSystem);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureHud(ManagerSpecRuntimeHUD hud, InputManager inputManager, InteractionManager interactionManager, TransferSystem transferSystem)
    {
        SerializedObject so = new SerializedObject(hud);
        SetObjectRef(so, "inputManager", inputManager);
        SetObjectRef(so, "interactionManager", interactionManager);
        SetObjectRef(so, "transferSystem", transferSystem);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject TryInstantiatePaperBoxPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PaperBoxPrefabPath);
        if (prefab == null)
        {
            return null;
        }

        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    private static void EnsureDynamicColliderCompatibility(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            root.AddComponent<BoxCollider>();
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] is MeshCollider meshCollider)
            {
                meshCollider.convex = true;
            }
        }
    }

    private static void PlaceAboveGround(GameObject instance, Vector3 groundPos)
    {
        instance.transform.position = groundPos;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            instance.transform.position += new Vector3(0f, 0.5f, 0f);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float lift = (0f - bounds.min.y) + 0.02f;
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

    private static void SetString(SerializedObject so, string propertyName, string value)
    {
        SerializedProperty p = so.FindProperty(propertyName);
        if (p != null)
        {
            p.stringValue = value;
        }
    }

    private static void SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty p = so.FindProperty(propertyName);
        if (p != null)
        {
            p.intValue = value;
        }
    }

    private static void SetFloat(SerializedObject so, string propertyName, float value)
    {
        SerializedProperty p = so.FindProperty(propertyName);
        if (p != null)
        {
            p.floatValue = value;
        }
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty p = so.FindProperty(propertyName);
        if (p != null)
        {
            p.boolValue = value;
        }
    }

    private static void SetObjectRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty p = so.FindProperty(propertyName);
        if (p != null)
        {
            p.objectReferenceValue = value;
        }
    }
}
