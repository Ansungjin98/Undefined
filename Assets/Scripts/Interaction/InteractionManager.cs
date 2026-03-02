using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public enum DragActionType
{
    None,
    Throw,
    Shake,
    VerticalDummy
}

public class InteractionManager : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private float dragThreshold = 0.5f;
    [SerializeField] private float raycastInterval = 0f;

    [Header("Hold / Drag")]
    [SerializeField] private Transform holdAnchor;
    [SerializeField] private float holdDistanceFromCamera = 2.8f;
    [SerializeField] private bool preservePickupDistance = true;
    [SerializeField] private float minHoldDistance = 2.2f;
    [SerializeField] private float maxHoldDistance = 4.2f;
    [SerializeField] private float holdLerpSpeed = 14f;
    [SerializeField] private float throwForce = 8f;

    [Header("Optional Manager References")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private MonoBehaviour uiManager;
    [SerializeField] private MonoBehaviour transferSystem;

    [Header("Always-On Overlay")]
    [SerializeField] private bool forceOverlayFromInteractionManager = true;
    [SerializeField] private Color overlayColor = Color.white;

    public Transform CurrentTargetTransform { get; private set; }
    public GameObject CurrentTargetObject { get; private set; }
    public bool IsHoldingObject => _heldObject != null;
    public GameObject HeldObject => _heldObject;

    public event Action<GameObject, string> OnInteractionPromptChanged;
    public event Action<DragActionType> OnDragActionTriggered;
    public event Action<Vector3, Vector3, bool> OnDropPreviewUpdated;

    private Camera _mainCamera;
    private GameObject _heldObject;
    private Rigidbody _heldRigidbody;
    private bool _heldOriginalUseGravity;
    private bool _heldOriginalIsKinematic;
    private CollisionDetectionMode _heldOriginalCollisionMode;
    private RigidbodyInterpolation _heldOriginalInterpolation;
    private float _nextRaycastTime;
    private GameObject _lastPromptTarget;
    private string _lastPromptText;

    private bool _isDragTracking;
    private bool _hasTriggeredDragActionThisHold;
    private Vector2 _dragAccumulatedDelta;
    private float _currentHoldDistance;
    private Texture2D _overlayPixel;
    private TransferSystem _cachedTransferSystem;
    private GameObject _runtimeCrosshairCanvas;

    private void Awake()
    {
        EnsureOverlayComponents();
        EnsureRuntimeCrosshairCanvas();
        holdDistanceFromCamera = Mathf.Max(2.2f, holdDistanceFromCamera);
        minHoldDistance = Mathf.Max(2.0f, minHoldDistance);
        maxHoldDistance = Mathf.Max(minHoldDistance + 0.5f, maxHoldDistance);
        _mainCamera = Camera.main;
        _currentHoldDistance = holdDistanceFromCamera;
        if (_mainCamera == null)
        {
            Debug.LogWarning("[InteractionManager] Main Camera not found on Awake. Will retry later.");
        }
    }

    private void OnEnable()
    {
        SubscribeInputEvents();
    }

    private void Start()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (_cachedTransferSystem == null)
        {
            _cachedTransferSystem = transferSystem as TransferSystem;
            if (_cachedTransferSystem == null)
            {
                _cachedTransferSystem = FindFirstObjectByType<TransferSystem>();
            }
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                return;
            }
        }

        if (raycastInterval <= 0f || Time.time >= _nextRaycastTime)
        {
            RefreshInteractionTarget();
            _nextRaycastTime = Time.time + Mathf.Max(0f, raycastInterval);
        }
    }

    private void OnGUI()
    {
        if (!forceOverlayFromInteractionManager)
        {
            return;
        }

        EnsureOverlayPixel();
        DrawCrosshairOverlay();
        DrawTransferLoadingBarOverlay();
        DrawDropPointOverlay();
    }

    private void LateUpdate()
    {
        if (_heldObject == null)
        {
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                return;
            }
        }

        Vector3 targetPosition = holdAnchor != null
            ? holdAnchor.position
            : _mainCamera.transform.position + (_mainCamera.transform.forward * _currentHoldDistance);
        Quaternion targetRotation = holdAnchor != null ? holdAnchor.rotation : _mainCamera.transform.rotation;

        if (_heldRigidbody != null && !_heldRigidbody.isKinematic)
        {
            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
            _heldRigidbody.MovePosition(Vector3.Lerp(_heldRigidbody.position, targetPosition, Time.deltaTime * holdLerpSpeed));
            _heldRigidbody.MoveRotation(Quaternion.Slerp(_heldRigidbody.rotation, targetRotation, Time.deltaTime * holdLerpSpeed));
        }
        else
        {
            _heldObject.transform.position = Vector3.Lerp(_heldObject.transform.position, targetPosition, Time.deltaTime * holdLerpSpeed);
            _heldObject.transform.rotation = Quaternion.Slerp(_heldObject.transform.rotation, targetRotation, Time.deltaTime * holdLerpSpeed);
        }

        UpdateDropPreview();
    }

    private void OnDisable()
    {
        UnsubscribeInputEvents();
        ResetDragTracking();
    }

    public bool TryHandlePrimaryInteraction()
    {
        // Spec behavior for the test flow: E acts as a hold/drop toggle first.
        if (IsHoldingObject)
        {
            DropHeldObject();
            return true;
        }

        if (CurrentTargetObject == null)
        {
            return false;
        }

        bool hasExplicitData;
        bool isHoldable = IsHoldable(CurrentTargetObject, out hasExplicitData);
        if (!IsHoldingObject && isHoldable)
        {
            PickupObject(CurrentTargetObject);
            return true;
        }

        if (!hasExplicitData && !IsHoldingObject)
        {
            Debug.LogWarning("[InteractionManager] holdenable metadata not found. Falling back to component-based interaction.");
        }

        CurrentTargetObject.SendMessage("HandlePrimaryInteraction", null, SendMessageOptions.DontRequireReceiver);
        CurrentTargetObject.SendMessage("OpenDoor", null, SendMessageOptions.DontRequireReceiver);
        CurrentTargetObject.SendMessage("ClimbLadder", null, SendMessageOptions.DontRequireReceiver);
        return true;
    }

    public bool TryGetTransferTarget(out GameObject target)
    {
        target = CurrentTargetObject;
        return target != null;
    }

    public void HandleSecondaryInteractHeld(bool isHeld)
    {
        if (!IsHoldingObject)
        {
            if (isHeld)
            {
                Debug.LogWarning("[InteractionManager] Drag input ignored because no object is held.");
            }

            ResetDragTracking();
            return;
        }

        if (isHeld)
        {
            _isDragTracking = true;
            _hasTriggeredDragActionThisHold = false;
            _dragAccumulatedDelta = Vector2.zero;
        }
        else
        {
            ResetDragTracking();
        }
    }

    public void HandleSecondaryInteractDragDelta(Vector2 delta)
    {
        if (!_isDragTracking || _heldObject == null || _hasTriggeredDragActionThisHold)
        {
            return;
        }

        _dragAccumulatedDelta += delta;
        if (_dragAccumulatedDelta.magnitude < dragThreshold)
        {
            return;
        }

        DragActionType action = ClassifyDragAction(_dragAccumulatedDelta);
        ExecuteDragAction(action);
        _hasTriggeredDragActionThisHold = true;
        OnDragActionTriggered?.Invoke(action);
    }

    private void SubscribeInputEvents()
    {
        if (inputManager == null)
        {
            inputManager = InputManager.Instance;
        }

        if (inputManager == null)
        {
            Debug.LogWarning("[InteractionManager] InputManager reference missing. Event subscription skipped.");
            return;
        }

        inputManager.OnPrimaryInteractPressed += HandlePrimaryInteractInput;
        inputManager.OnSecondaryInteractHeldChanged += HandleSecondaryInteractHeld;
        inputManager.OnSecondaryInteractDragDelta += HandleSecondaryInteractDragDelta;
    }

    private void UnsubscribeInputEvents()
    {
        if (inputManager == null)
        {
            return;
        }

        inputManager.OnPrimaryInteractPressed -= HandlePrimaryInteractInput;
        inputManager.OnSecondaryInteractHeldChanged -= HandleSecondaryInteractHeld;
        inputManager.OnSecondaryInteractDragDelta -= HandleSecondaryInteractDragDelta;
    }

    private void HandlePrimaryInteractInput()
    {
        TryHandlePrimaryInteraction();
    }

    private void RefreshInteractionTarget()
    {
        Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayer, QueryTriggerInteraction.Ignore))
        {
            GameObject resolvedTarget = ResolveInteractionTarget(hit.transform);
            CurrentTargetTransform = resolvedTarget != null ? resolvedTarget.transform : hit.transform;
            CurrentTargetObject = resolvedTarget != null ? resolvedTarget : hit.transform.gameObject;
        }
        else
        {
            CurrentTargetTransform = null;
            CurrentTargetObject = null;
        }

        EmitPromptIfChanged();
    }

    private void EmitPromptIfChanged()
    {
        string prompt = BuildPrompt(CurrentTargetObject);
        if (_lastPromptTarget == CurrentTargetObject && string.Equals(_lastPromptText, prompt, StringComparison.Ordinal))
        {
            return;
        }

        _lastPromptTarget = CurrentTargetObject;
        _lastPromptText = prompt;
        OnInteractionPromptChanged?.Invoke(CurrentTargetObject, prompt);
    }

    private string BuildPrompt(GameObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        if (IsHoldable(target, out _))
        {
            return "E - 물건 들기";
        }

        string reflectedPrompt = TryGetStringFromComponents(target, "GetInteractionPrompt", "InteractionPrompt", "PromptText");
        if (!string.IsNullOrWhiteSpace(reflectedPrompt))
        {
            return reflectedPrompt;
        }

        return "E - 상호작용";
    }

    private bool IsHoldable(GameObject target, out bool hasExplicitData)
    {
        hasExplicitData = false;
        if (target == null)
        {
            return false;
        }

        if (TryGetBoolFromComponents(target, out bool explicitValue, out hasExplicitData, "holdenable", "Holdenable", "isHoldable", "IsHoldable"))
        {
            return explicitValue;
        }

        if (TryGetIntFromComponents(target, out int holdableState, out hasExplicitData, "holdableState", "HoldableState", "Holdable01", "IsHoldable01"))
        {
            return holdableState != 0;
        }

        return target.GetComponent<Rigidbody>() != null;
    }

    private void PickupObject(GameObject target)
    {
        _heldObject = target;
        _heldRigidbody = target.GetComponent<Rigidbody>();
        if (_heldRigidbody != null)
        {
            _heldOriginalUseGravity = _heldRigidbody.useGravity;
            _heldOriginalIsKinematic = _heldRigidbody.isKinematic;
            _heldOriginalCollisionMode = _heldRigidbody.collisionDetectionMode;
            _heldOriginalInterpolation = _heldRigidbody.interpolation;

            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
            _heldRigidbody.useGravity = false;
            _heldRigidbody.isKinematic = true;
            _heldRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _heldRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        _currentHoldDistance = holdDistanceFromCamera;
        if (holdAnchor == null && preservePickupDistance && _mainCamera != null)
        {
            float rawDistance = Vector3.Distance(_mainCamera.transform.position, target.transform.position);
            _currentHoldDistance = Mathf.Clamp(rawDistance, minHoldDistance, maxHoldDistance);
        }
    }

    private void DropHeldObject()
    {
        if (_heldObject == null)
        {
            return;
        }

        if (_heldRigidbody != null)
        {
            TryResolveDropPenetration(_heldObject);

            _heldRigidbody.isKinematic = _heldOriginalIsKinematic;
            _heldRigidbody.useGravity = _heldOriginalUseGravity;
            _heldRigidbody.collisionDetectionMode = _heldOriginalCollisionMode;
            _heldRigidbody.interpolation = _heldOriginalInterpolation;
            _heldRigidbody.linearVelocity = Vector3.zero;
            _heldRigidbody.angularVelocity = Vector3.zero;
            _heldRigidbody.WakeUp();
        }
        else
        {
            TryResolveDropPenetration(_heldObject);
        }

        _heldObject = null;
        _heldRigidbody = null;
        OnDropPreviewUpdated?.Invoke(Vector3.zero, Vector3.zero, false);
        ResetDragTracking();
    }

    private DragActionType ClassifyDragAction(Vector2 totalDelta)
    {
        if (Mathf.Abs(totalDelta.x) >= Mathf.Abs(totalDelta.y))
        {
            return totalDelta.x < 0f ? DragActionType.Throw : DragActionType.Shake;
        }

        return DragActionType.VerticalDummy;
    }

    private void ExecuteDragAction(DragActionType action)
    {
        if (_heldObject == null)
        {
            return;
        }

        switch (action)
        {
            case DragActionType.Throw:
                ThrowHeldObject();
                break;
            case DragActionType.Shake:
                _heldObject.SendMessage("HandleShakeInteraction", null, SendMessageOptions.DontRequireReceiver);
                break;
            case DragActionType.VerticalDummy:
                _heldObject.SendMessage("HandleVerticalDragDummy", null, SendMessageOptions.DontRequireReceiver);
                break;
        }
    }

    private void ThrowHeldObject()
    {
        Rigidbody rb = _heldRigidbody;
        GameObject thrown = _heldObject;

        DropHeldObject();

        if (rb == null)
        {
            Debug.LogWarning("[InteractionManager] Throw requested but held object has no Rigidbody.");
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        Vector3 throwDirection = _mainCamera != null ? _mainCamera.transform.forward : transform.forward;
        rb.linearVelocity = throwDirection.normalized * throwForce;
        thrown.SendMessage("HandleThrown", null, SendMessageOptions.DontRequireReceiver);
    }

    private void ResetDragTracking()
    {
        _isDragTracking = false;
        _hasTriggeredDragActionThisHold = false;
        _dragAccumulatedDelta = Vector2.zero;
    }

    private void UpdateDropPreview()
    {
        if (_heldObject == null)
        {
            OnDropPreviewUpdated?.Invoke(Vector3.zero, Vector3.zero, false);
            return;
        }

        Collider col = _heldObject.GetComponent<Collider>();
        if (col == null)
        {
            col = _heldObject.GetComponentInChildren<Collider>();
        }

        if (col == null)
        {
            OnDropPreviewUpdated?.Invoke(Vector3.zero, Vector3.zero, false);
            return;
        }

        Bounds b = col.bounds;
        Vector3 start = b.center;
        Vector3 rayOrigin = new Vector3(start.x, start.y + 2.0f, start.z);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            OnDropPreviewUpdated?.Invoke(Vector3.zero, Vector3.zero, false);
            return;
        }

        float targetMinY = hit.point.y + 0.02f;
        float dy = targetMinY - b.min.y;
        Vector3 end = _heldObject.transform.position + Vector3.up * Mathf.Max(0f, dy);
        end.x = _heldObject.transform.position.x;
        end.z = _heldObject.transform.position.z;
        OnDropPreviewUpdated?.Invoke(start, end, true);
    }

    private static void TryResolveDropPenetration(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        Collider col = obj.GetComponent<Collider>();
        if (col == null)
        {
            col = obj.GetComponentInChildren<Collider>();
        }

        if (col == null)
        {
            return;
        }

        Bounds b = col.bounds;
        Vector3 rayOrigin = b.center + Vector3.up * 2.0f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            float desiredMinY = hit.point.y + 0.02f;
            float delta = desiredMinY - b.min.y;
            if (delta > 0f)
            {
                obj.transform.position += Vector3.up * delta;
            }
        }
    }

    private void EnsureOverlayPixel()
    {
        if (_overlayPixel != null)
        {
            return;
        }

        _overlayPixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _overlayPixel.SetPixel(0, 0, Color.white);
        _overlayPixel.Apply();
    }

    private void DrawCrosshairOverlay()
    {
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        const float arm = 8f;
        const float gap = 5f;
        const float thickness = 2.5f;

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        DrawOverlayLine(new Vector2(cx - gap - arm, cy + 1f), new Vector2(cx - gap, cy + 1f), thickness + 1f);
        DrawOverlayLine(new Vector2(cx + gap, cy + 1f), new Vector2(cx + gap + arm, cy + 1f), thickness + 1f);
        DrawOverlayLine(new Vector2(cx + 1f, cy - gap - arm), new Vector2(cx + 1f, cy - gap), thickness + 1f);
        DrawOverlayLine(new Vector2(cx + 1f, cy + gap), new Vector2(cx + 1f, cy + gap + arm), thickness + 1f);

        GUI.color = overlayColor;
        DrawOverlayLine(new Vector2(cx - gap - arm, cy), new Vector2(cx - gap, cy), thickness);
        DrawOverlayLine(new Vector2(cx + gap, cy), new Vector2(cx + gap + arm, cy), thickness);
        DrawOverlayLine(new Vector2(cx, cy - gap - arm), new Vector2(cx, cy - gap), thickness);
        DrawOverlayLine(new Vector2(cx, cy + gap), new Vector2(cx, cy + gap + arm), thickness);
        DrawOverlayCircle(new Vector2(cx, cy), 1.8f, 1.8f);
        GUI.color = prev;
    }

    private void DrawTransferLoadingBarOverlay()
    {
        if (_cachedTransferSystem == null || !_cachedTransferSystem.IsChanneling)
        {
            return;
        }

        float progress = _cachedTransferSystem.CurrentChannelProgress01;
        string label = _cachedTransferSystem.CurrentChannelActionKind == TransferActionKind.Extract ? "Extracting..." : "Injecting...";

        Rect bg = new Rect((Screen.width * 0.5f) - 220f, (Screen.height * 0.5f) + 110f, 440f, 24f);
        Rect fill = new Rect(bg.x + 2f, bg.y + 2f, (bg.width - 4f) * progress, bg.height - 4f);
        DrawOverlayRect(bg, new Color(0f, 0f, 0f, 0.7f));
        DrawOverlayRect(fill, _cachedTransferSystem.CurrentChannelHandSide == HandSide.Left
            ? new Color(1f, 0.9f, 0.15f, 1f)
            : new Color(0.2f, 1f, 0.25f, 1f));

        GUIStyle style = GUI.skin.label;
        Color prev = style.normal.textColor;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(bg.x, bg.y - 20f, bg.width, 18f), $"{label} {(progress * 100f):0}%");
        style.normal.textColor = prev;
    }

    private void DrawDropPointOverlay()
    {
        if (!IsHoldingObject || _mainCamera == null)
        {
            return;
        }

        if (!TryGetDropPreviewPoints(_heldObject, out Vector3 startWorld, out Vector3 endWorld))
        {
            return;
        }

        Vector3 startScreen = _mainCamera.WorldToScreenPoint(startWorld);
        Vector3 endScreen = _mainCamera.WorldToScreenPoint(endWorld);
        if (startScreen.z <= 0f || endScreen.z <= 0f)
        {
            return;
        }

        Vector2 s = new Vector2(startScreen.x, Screen.height - startScreen.y);
        Vector2 e = new Vector2(endScreen.x, Screen.height - endScreen.y);

        Color prev = GUI.color;
        GUI.color = Color.white;
        DrawOverlayDottedLine(s, e, 5f, 5f, 2f);
        DrawOverlayCircle(e, 12f, 2.6f);
        DrawOverlayCircle(e, 4.5f, 4.5f);
        GUI.color = prev;
    }

    private bool TryGetDropPreviewPoints(GameObject heldObject, out Vector3 start, out Vector3 end)
    {
        start = Vector3.zero;
        end = Vector3.zero;
        if (heldObject == null)
        {
            return false;
        }

        Collider col = heldObject.GetComponent<Collider>();
        if (col == null)
        {
            col = heldObject.GetComponentInChildren<Collider>();
        }

        if (col == null)
        {
            return false;
        }

        Bounds b = col.bounds;
        start = b.center;

        Vector3 rayOrigin = new Vector3(start.x, start.y + 2f, start.z);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        float targetMinY = hit.point.y + 0.02f;
        float dy = targetMinY - b.min.y;
        end = heldObject.transform.position + Vector3.up * Mathf.Max(0f, dy);
        end.x = heldObject.transform.position.x;
        end.z = heldObject.transform.position.z;
        return true;
    }

    private void DrawOverlayRect(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, _overlayPixel);
        GUI.color = prev;
    }

    private void DrawOverlayDottedLine(Vector2 from, Vector2 to, float dashLength, float gapLength, float thickness)
    {
        Vector2 dir = to - from;
        float distance = dir.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        dir /= distance;
        float travelled = 0f;
        while (travelled < distance)
        {
            float seg = Mathf.Min(dashLength, distance - travelled);
            Vector2 a = from + (dir * travelled);
            Vector2 b = from + (dir * (travelled + seg));
            DrawOverlayLine(a, b, thickness);
            travelled += dashLength + gapLength;
        }
    }

    private void DrawOverlayCircle(Vector2 center, float radius, float thickness)
    {
        const int segments = 28;
        float step = (Mathf.PI * 2f) / segments;
        Vector2 prev = center + new Vector2(Mathf.Cos(0f), Mathf.Sin(0f)) * radius;
        for (int i = 1; i <= segments; i++)
        {
            float t = i * step;
            Vector2 next = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
            DrawOverlayLine(prev, next, thickness);
            prev = next;
        }
    }

    private void DrawOverlayLine(Vector2 from, Vector2 to, float thickness)
    {
        Vector2 delta = to - from;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float length = delta.magnitude;
        if (length <= 0.001f)
        {
            return;
        }

        Matrix4x4 matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, from);
        GUI.DrawTexture(new Rect(from.x, from.y - (thickness * 0.5f), length, thickness), _overlayPixel);
        GUI.matrix = matrix;
    }

    private void EnsureRuntimeCrosshairCanvas()
    {
        if (_runtimeCrosshairCanvas != null)
        {
            return;
        }

        GameObject existing = GameObject.Find("RuntimeCrosshairCanvas");
        if (existing != null)
        {
            _runtimeCrosshairCanvas = existing;
            return;
        }

        GameObject canvasGo = new GameObject("RuntimeCrosshairCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGo);
        _runtimeCrosshairCanvas = canvasGo;

        RectTransform root = canvasGo.GetComponent<RectTransform>();
        CreateCrosshairBar(root, new Vector2(-11f, 0f), new Vector2(8f, 2f));
        CreateCrosshairBar(root, new Vector2(11f, 0f), new Vector2(8f, 2f));
        CreateCrosshairBar(root, new Vector2(0f, -11f), new Vector2(2f, 8f));
        CreateCrosshairBar(root, new Vector2(0f, 11f), new Vector2(2f, 8f));
        CreateCrosshairBar(root, Vector2.zero, new Vector2(2f, 2f));
    }

    private static void CreateCrosshairBar(RectTransform parent, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject("CrosshairPart");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = Color.white;
    }

    private static void EnsureOverlayComponents()
    {
        ManagerSpecRuntimeHUD hud = FindFirstObjectByType<ManagerSpecRuntimeHUD>();
        SimpleCrosshairOverlay crosshair = FindFirstObjectByType<SimpleCrosshairOverlay>();

        GameObject host = GameObject.Find("RuntimeOverlays");
        if (host == null)
        {
            host = new GameObject("RuntimeOverlays");
        }

        if (hud == null)
        {
            hud = host.AddComponent<ManagerSpecRuntimeHUD>();
        }

        if (crosshair == null)
        {
            crosshair = host.AddComponent<SimpleCrosshairOverlay>();
        }

        if (hud != null)
        {
            hud.enabled = true;
        }

        if (crosshair != null)
        {
            crosshair.enabled = true;
        }
    }

    private static bool TryGetBoolFromComponents(GameObject target, out bool value, out bool foundField, params string[] memberNames)
    {
        value = false;
        foundField = false;

        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            for (int m = 0; m < memberNames.Length; m++)
            {
                string member = memberNames[m];
                FieldInfo field = type.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(bool))
                {
                    value = (bool)field.GetValue(component);
                    foundField = true;
                    return true;
                }

                PropertyInfo property = type.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(bool) && property.CanRead)
                {
                    value = (bool)property.GetValue(component, null);
                    foundField = true;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetIntFromComponents(GameObject target, out int value, out bool foundField, params string[] memberNames)
    {
        value = 0;
        foundField = false;

        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            for (int m = 0; m < memberNames.Length; m++)
            {
                string member = memberNames[m];
                FieldInfo field = type.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(int))
                {
                    value = (int)field.GetValue(component);
                    foundField = true;
                    return true;
                }

                PropertyInfo property = type.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(int) && property.CanRead)
                {
                    value = (int)property.GetValue(component, null);
                    foundField = true;
                    return true;
                }
            }
        }

        return false;
    }

    private static GameObject ResolveInteractionTarget(Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return null;
        }

        Transform current = hitTransform;
        while (current != null)
        {
            GameObject candidate = current.gameObject;
            if (HasInteractionMetadata(candidate) || candidate.GetComponent<Rigidbody>() != null)
            {
                return candidate;
            }

            current = current.parent;
        }

        return hitTransform.gameObject;
    }

    private static bool HasInteractionMetadata(GameObject target)
    {
        bool found;
        bool _;
        if (TryGetBoolFromComponents(target, out _, out found, "holdenable", "Holdenable", "isHoldable", "IsHoldable"))
        {
            return found;
        }

        int intValue;
        if (TryGetIntFromComponents(target, out intValue, out found, "holdableState", "HoldableState", "Holdable01", "IsHoldable01"))
        {
            return found;
        }

        string prompt = TryGetStringFromComponents(target, "GetInteractionPrompt", "InteractionPrompt", "PromptText");
        return !string.IsNullOrWhiteSpace(prompt);
    }

    private static string TryGetStringFromComponents(GameObject target, params string[] members)
    {
        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            for (int m = 0; m < members.Length; m++)
            {
                string member = members[m];

                MethodInfo method = type.GetMethod(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (method != null && method.ReturnType == typeof(string))
                {
                    return (string)method.Invoke(component, null);
                }

                PropertyInfo property = type.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(string) && property.CanRead)
                {
                    return (string)property.GetValue(component, null);
                }

                FieldInfo field = type.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(string))
                {
                    return (string)field.GetValue(component);
                }
            }
        }

        return null;
    }
}
