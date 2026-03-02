using UnityEngine;

public class ManagerSpecRuntimeHUD : MonoBehaviour
{
    [Header("Manager References")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private InteractionManager interactionManager;
    [SerializeField] private TransferSystem transferSystem;

    [Header("Display")]
    [SerializeField] private bool showCrosshair = true;
    [SerializeField] private bool forceCrosshairVisible = true;
    [SerializeField] private bool showStatusPanel = true;
    [SerializeField] private int fontSize = 14;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color shadowColor = Color.black;

    private string _prompt;
    private string _transferFeedbackLeft = "-";
    private string _transferFeedbackRight = "-";
    private bool _leftChanneling;
    private bool _rightChanneling;
    private string _clickNotice = string.Empty;
    private float _clickNoticeUntil;
    private bool _hasDropPreview;
    private Vector3 _dropPreviewStartWorld;
    private Vector3 _dropPreviewEndWorld;
    private GUIStyle _labelStyle;
    private GUIStyle _shadowStyle;
    private Texture2D _solidTexture;

    private void OnEnable()
    {
        if (FindFirstObjectByType<SimpleCrosshairOverlay>() == null)
        {
            showCrosshair = true;
        }

        if (inputManager == null)
        {
            inputManager = InputManager.Instance;
        }

        if (interactionManager == null)
        {
            interactionManager = FindFirstObjectByType<InteractionManager>();
        }

        if (transferSystem == null)
        {
            transferSystem = FindFirstObjectByType<TransferSystem>();
        }

        if (interactionManager != null)
        {
            interactionManager.OnInteractionPromptChanged += HandlePromptChanged;
            interactionManager.OnDropPreviewUpdated += HandleDropPreviewUpdated;
        }

        if (inputManager != null)
        {
            inputManager.OnTransferClickPressed += HandleTransferClickPressed;
        }

        if (transferSystem != null)
        {
            transferSystem.OnTransferFeedbackRequested += HandleTransferFeedback;
            transferSystem.OnChannelingStateChanged += HandleChannelingStateChanged;
        }
    }

    private void OnDisable()
    {
        if (interactionManager != null)
        {
            interactionManager.OnInteractionPromptChanged -= HandlePromptChanged;
            interactionManager.OnDropPreviewUpdated -= HandleDropPreviewUpdated;
        }

        if (inputManager != null)
        {
            inputManager.OnTransferClickPressed -= HandleTransferClickPressed;
        }

        if (transferSystem != null)
        {
            transferSystem.OnTransferFeedbackRequested -= HandleTransferFeedback;
            transferSystem.OnChannelingStateChanged -= HandleChannelingStateChanged;
        }

        _hasDropPreview = false;
    }

    private void OnGUI()
    {
        EnsureStyles();
        EnsureRuntimeReferences();

        if (showCrosshair || forceCrosshairVisible)
        {
            DrawCrosshair();
        }

        if (showStatusPanel)
        {
            DrawStatusPanel();
        }

        DrawChannelingBar();
        DrawDropPreviewMarker();
    }

    private void EnsureRuntimeReferences()
    {
        if (interactionManager == null)
        {
            interactionManager = FindFirstObjectByType<InteractionManager>();
            if (interactionManager != null)
            {
                interactionManager.OnInteractionPromptChanged += HandlePromptChanged;
                interactionManager.OnDropPreviewUpdated += HandleDropPreviewUpdated;
            }
        }

        if (inputManager == null)
        {
            inputManager = InputManager.Instance;
            if (inputManager != null)
            {
                inputManager.OnTransferClickPressed += HandleTransferClickPressed;
            }
        }

        if (transferSystem == null)
        {
            transferSystem = FindFirstObjectByType<TransferSystem>();
            if (transferSystem != null)
            {
                transferSystem.OnTransferFeedbackRequested += HandleTransferFeedback;
                transferSystem.OnChannelingStateChanged += HandleChannelingStateChanged;
            }
        }
    }

    private void EnsureStyles()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = fontSize;
            _labelStyle.alignment = TextAnchor.UpperLeft;
            _labelStyle.wordWrap = true;
            _labelStyle.normal.textColor = textColor;
        }

        if (_shadowStyle == null)
        {
            _shadowStyle = new GUIStyle(_labelStyle);
            _shadowStyle.normal.textColor = shadowColor;
        }

        if (_solidTexture == null)
        {
            _solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _solidTexture.SetPixel(0, 0, Color.white);
            _solidTexture.Apply();
        }
    }

    private void DrawCrosshair()
    {
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        const float arm = 8f;
        const float gap = 5f;
        const float thickness = 2.5f;

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        DrawLine(new Vector2(cx - gap - arm, cy + 1f), new Vector2(cx - gap, cy + 1f), thickness + 1f);
        DrawLine(new Vector2(cx + gap, cy + 1f), new Vector2(cx + gap + arm, cy + 1f), thickness + 1f);
        DrawLine(new Vector2(cx + 1f, cy - gap - arm), new Vector2(cx + 1f, cy - gap), thickness + 1f);
        DrawLine(new Vector2(cx + 1f, cy + gap), new Vector2(cx + 1f, cy + gap + arm), thickness + 1f);

        GUI.color = Color.white;
        DrawLine(new Vector2(cx - gap - arm, cy), new Vector2(cx - gap, cy), thickness);
        DrawLine(new Vector2(cx + gap, cy), new Vector2(cx + gap + arm, cy), thickness);
        DrawLine(new Vector2(cx, cy - gap - arm), new Vector2(cx, cy - gap), thickness);
        DrawLine(new Vector2(cx, cy + gap), new Vector2(cx, cy + gap + arm), thickness);
        DrawCircle(new Vector2(cx, cy), 1.8f, 1.8f);
        GUI.color = prev;
    }

    private void DrawStatusPanel()
    {
        string inputText = BuildInputText();
        string interactionText = BuildInteractionText();
        string transferText = BuildTransferText();

        Rect panelRect = new Rect(12f, 12f, 560f, 180f);
        GUI.Box(panelRect, "SPEC Runtime HUD");

        float x = panelRect.x + 10f;
        float y = panelRect.y + 24f;
        DrawShadowLabel(new Rect(x, y, panelRect.width - 20f, 60f), inputText);
        y += 54f;
        DrawShadowLabel(new Rect(x, y, panelRect.width - 20f, 42f), interactionText);
        y += 36f;
        DrawShadowLabel(new Rect(x, y, panelRect.width - 20f, 72f), transferText);

        if (!string.IsNullOrEmpty(_prompt))
        {
            Rect promptRect = new Rect((Screen.width * 0.5f) - 200f, Screen.height - 120f, 400f, 30f);
            GUI.Label(new Rect(promptRect.x + 1, promptRect.y + 1, promptRect.width, promptRect.height), _prompt, _shadowStyle);
            GUI.Label(promptRect, _prompt, _labelStyle);
        }

        DrawTransferClickGuide();
    }

    private void DrawShadowLabel(Rect rect, string text)
    {
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _shadowStyle);
        GUI.Label(rect, text, _labelStyle);
    }

    private string BuildInputText()
    {
        if (inputManager == null)
        {
            return "InputManager: (없음)";
        }

        return string.Format(
            "Input | Move:{0} Look:{1} Run:{2} Posture:{3} Speed:{4:0.00} Jump:{5:0.00}",
            inputManager.MoveInput,
            inputManager.LookInput,
            inputManager.IsRunPressed,
            inputManager.CurrentPosture,
            inputManager.CurrentMoveSpeed,
            inputManager.LastJumpForceApplied);
    }

    private string BuildInteractionText()
    {
        if (interactionManager == null)
        {
            return "InteractionManager: (없음)";
        }

        string targetName = interactionManager.CurrentTargetObject != null ? interactionManager.CurrentTargetObject.name : "(none)";
        string heldName = interactionManager.HeldObject != null ? interactionManager.HeldObject.name : "(none)";
        return $"Interaction | Target:{targetName} Held:{heldName} Prompt:{_prompt}";
    }

    private string BuildTransferText()
    {
        if (transferSystem == null)
        {
            return "TransferSystem: (없음)";
        }

        HandSlotState left = transferSystem.LeftHandState;
        HandSlotState right = transferSystem.RightHandState;

        return string.Format(
            "Transfer | L(has:{0}, id:{1}, ch:{2}) R(has:{3}, id:{4}, ch:{5})\nL msg:{6}\nR msg:{7}",
            left.HasProperty,
            SafeValue(left.PropertyId),
            _leftChanneling,
            right.HasProperty,
            SafeValue(right.PropertyId),
            _rightChanneling,
            _transferFeedbackLeft,
            _transferFeedbackRight);
    }

    private static string SafeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void DrawTransferClickGuide()
    {
        if (transferSystem == null)
        {
            return;
        }

        HandSlotState left = transferSystem.LeftHandState;
        HandSlotState right = transferSystem.RightHandState;
        string leftAction = left.HasProperty ? "주입" : "추출";
        string rightAction = right.HasProperty ? "주입" : "추출";

        string guideLine1 = $"좌클릭 = 왼손 / 정적 성질 / {leftAction}";
        string guideLine2 = $"우클릭 = 오른손 / 동적 성질 / {rightAction}";

        Rect guideRect = new Rect((Screen.width * 0.5f) - 240f, Screen.height - 88f, 480f, 52f);
        DrawShadowLabel(new Rect(guideRect.x, guideRect.y, guideRect.width, 24f), guideLine1);
        DrawShadowLabel(new Rect(guideRect.x, guideRect.y + 22f, guideRect.width, 24f), guideLine2);

        if (!string.IsNullOrEmpty(_clickNotice) && Time.time <= _clickNoticeUntil)
        {
            Rect noticeRect = new Rect((Screen.width * 0.5f) - 260f, Screen.height - 155f, 520f, 28f);
            DrawShadowLabel(noticeRect, _clickNotice);
        }
    }

    private void DrawChannelingBar()
    {
        if (transferSystem == null || !transferSystem.IsChanneling)
        {
            return;
        }

        float progress = transferSystem.CurrentChannelProgress01;
        string label = transferSystem.CurrentChannelActionKind == TransferActionKind.Extract ? "Extracting" : "Injecting";
        string hand = transferSystem.CurrentChannelHandSide == HandSide.Left ? "LMB/Static" : "RMB/Dynamic";

        Rect bg = new Rect((Screen.width * 0.5f) - 220f, (Screen.height * 0.5f) + 110f, 440f, 24f);
        Rect fill = new Rect(bg.x + 2f, bg.y + 2f, (bg.width - 4f) * progress, bg.height - 4f);
        GUI.Box(bg, GUIContent.none);

        Color prev = GUI.color;
        GUI.color = transferSystem.CurrentChannelHandSide == HandSide.Left
            ? new Color(1f, 0.9f, 0.15f, 1f)
            : new Color(0.2f, 1f, 0.25f, 1f);
        GUI.Box(fill, GUIContent.none);
        GUI.color = prev;

        DrawShadowLabel(new Rect(bg.x, bg.y - 20f, bg.width, 18f), $"{hand} {label} {(progress * 100f):0}%");
    }

    private void HandlePromptChanged(GameObject target, string prompt)
    {
        _prompt = prompt;
    }

    private void HandleTransferFeedback(HandSide hand, string message)
    {
        if (hand == HandSide.Left)
        {
            _transferFeedbackLeft = "[좌클릭/정적] " + message;
        }
        else
        {
            _transferFeedbackRight = "[우클릭/동적] " + message;
        }
    }

    private void HandleChannelingStateChanged(HandSide hand, bool isChanneling)
    {
        if (hand == HandSide.Left)
        {
            _leftChanneling = isChanneling;
        }
        else
        {
            _rightChanneling = isChanneling;
        }
    }

    private void HandleTransferClickPressed(MouseButtonType button)
    {
        if (transferSystem == null)
        {
            return;
        }

        bool isLeft = button == MouseButtonType.Left;
        HandSlotState hand = isLeft ? transferSystem.LeftHandState : transferSystem.RightHandState;
        string handLabel = isLeft ? "좌클릭(왼손/정적)" : "우클릭(오른손/동적)";
        string action = hand.HasProperty ? "주입 시도" : "추출 시도";
        _clickNotice = $"{handLabel} -> {action}";
        _clickNoticeUntil = Time.time + 1.2f;
    }

    private void HandleDropPreviewUpdated(Vector3 start, Vector3 end, bool valid)
    {
        _dropPreviewStartWorld = start;
        _dropPreviewEndWorld = end;
        _hasDropPreview = valid;
    }

    private void DrawDropPreviewMarker()
    {
        if (interactionManager == null || !interactionManager.IsHoldingObject)
        {
            return;
        }

        Vector3 startWorld = _dropPreviewStartWorld;
        Vector3 endWorld = _dropPreviewEndWorld;
        bool valid = _hasDropPreview;
        if (!valid)
        {
            valid = TryBuildDropPreviewFromHeldObject(interactionManager.HeldObject, out startWorld, out endWorld);
        }

        if (!valid)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 startScreen = cam.WorldToScreenPoint(startWorld);
        Vector3 endScreen = cam.WorldToScreenPoint(endWorld);
        if (startScreen.z <= 0f || endScreen.z <= 0f)
        {
            return;
        }

        Vector2 s = new Vector2(startScreen.x, Screen.height - startScreen.y);
        Vector2 e = new Vector2(endScreen.x, Screen.height - endScreen.y);

        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        DrawDottedLine(s, e, 5f, 5f, 2f);
        DrawCircle(e, 12f, 2.6f);
        DrawCircle(e, 4.5f, 4.5f);
        GUI.color = prev;
    }

    private static bool TryBuildDropPreviewFromHeldObject(GameObject heldObject, out Vector3 start, out Vector3 end)
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

        Vector3 rayOrigin = new Vector3(start.x, start.y + 2.0f, start.z);
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

    private void DrawDottedLine(Vector2 from, Vector2 to, float dashLength, float gapLength, float thickness)
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
            DrawLine(a, b, thickness);
            travelled += dashLength + gapLength;
        }
    }

    private void DrawCircle(Vector2 center, float radius, float thickness)
    {
        const int segments = 28;
        float step = (Mathf.PI * 2f) / segments;
        Vector2 prev = center + new Vector2(Mathf.Cos(0f), Mathf.Sin(0f)) * radius;
        for (int i = 1; i <= segments; i++)
        {
            float t = i * step;
            Vector2 next = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
            DrawLine(prev, next, thickness);
            prev = next;
        }
    }

    private void DrawLine(Vector2 from, Vector2 to, float thickness)
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
        GUI.DrawTexture(new Rect(from.x, from.y - (thickness * 0.5f), length, thickness), _solidTexture);
        GUI.matrix = matrix;
    }
}
