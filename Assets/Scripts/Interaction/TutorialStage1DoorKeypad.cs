using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TutorialStage1DoorKeypad : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Transform doorPanel;
    [SerializeField] private Vector3 openedLocalOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] private float openLerpSpeed = 6f;

    [Header("Keypad")]
    [SerializeField] private string correctCode = "1234";
    [SerializeField] private int maxInputLength = 4;
    [SerializeField] private bool autoClearOnFailure = true;
    [SerializeField] private string promptText = "E - Use keypad";
    [SerializeField] private TextMesh statusText;

    [Header("Runtime (Debug)")]
    [SerializeField] private string currentInput = string.Empty;
    [SerializeField] private bool isOpened;
    [SerializeField] private bool isSessionActive;

    private Vector3 _doorClosedLocalPos;
    private CursorLockMode _savedLockMode;
    private bool _savedCursorVisible;
    private GameObject _runtimeCanvasRoot;
    private Text _runtimeInputText;

    public string CurrentInput => currentInput;
    public bool IsOpened => isOpened;

    private void Awake()
    {
        if (doorPanel == null)
        {
            doorPanel = transform;
        }

        _doorClosedLocalPos = doorPanel.localPosition;
        RefreshStatus();
    }

    private void Update()
    {
        if (doorPanel == null)
        {
            return;
        }

        Vector3 target = isOpened ? (_doorClosedLocalPos + openedLocalOffset) : _doorClosedLocalPos;
        doorPanel.localPosition = Vector3.Lerp(doorPanel.localPosition, target, Time.deltaTime * openLerpSpeed);
    }

    public string GetInteractionPrompt()
    {
        return promptText;
    }

    public void BeginInputSession()
    {
        if (isSessionActive)
        {
            return;
        }

        EnsureRuntimeUi();
        currentInput = string.Empty;
        isSessionActive = true;
        if (_runtimeCanvasRoot != null)
        {
            _runtimeCanvasRoot.SetActive(true);
        }
        RefreshRuntimeInputText();
        _savedLockMode = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void HandlePrimaryInteraction()
    {
        BeginInputSession();
    }

    public void InputDigit(string digit)
    {
        if (string.IsNullOrEmpty(digit) || isOpened)
        {
            return;
        }

        if (digit.Length != 1 || digit[0] < '0' || digit[0] > '9')
        {
            return;
        }

        currentInput += digit;
        if (currentInput.Length > maxInputLength)
        {
            currentInput = currentInput.Substring(currentInput.Length - maxInputLength, maxInputLength);
        }

        if (currentInput.Length < maxInputLength)
        {
            return;
        }

        bool isCorrect = string.Equals(currentInput, correctCode, System.StringComparison.Ordinal);
        isOpened = isCorrect;
        if (!isCorrect && autoClearOnFailure)
        {
            currentInput = string.Empty;
        }

        RefreshStatus();
        EndInputSession();
    }

    public void ClearInput()
    {
        currentInput = string.Empty;
        RefreshStatus();
    }

    private void EndInputSession()
    {
        if (!isSessionActive)
        {
            return;
        }

        isSessionActive = false;
        if (_runtimeCanvasRoot != null)
        {
            _runtimeCanvasRoot.SetActive(false);
        }
        Cursor.lockState = _savedLockMode;
        Cursor.visible = _savedCursorVisible;
    }

    private void OnDisable()
    {
        EndInputSession();
    }

    private void EnsureRuntimeUi()
    {
        if (_runtimeCanvasRoot == null)
        {
            _runtimeCanvasRoot = GameObject.Find("KeypadRuntimeCanvas");
        }

        if (_runtimeCanvasRoot != null && _runtimeInputText != null)
        {
            return;
        }

        if (_runtimeCanvasRoot == null)
        {
            GameObject canvasGo = new GameObject("KeypadRuntimeCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            Object.DontDestroyOnLoad(canvasGo);
            _runtimeCanvasRoot = canvasGo;
        }
        else
        {
            Canvas existingCanvas = _runtimeCanvasRoot.GetComponent<Canvas>();
            if (existingCanvas == null)
            {
                existingCanvas = _runtimeCanvasRoot.AddComponent<Canvas>();
            }
            existingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            existingCanvas.sortingOrder = 1200;
            if (_runtimeCanvasRoot.GetComponent<CanvasScaler>() == null)
            {
                _runtimeCanvasRoot.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }
            if (_runtimeCanvasRoot.GetComponent<GraphicRaycaster>() == null)
            {
                _runtimeCanvasRoot.AddComponent<GraphicRaycaster>();
            }
        }

        EventSystem eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (eventSystem == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Object.DontDestroyOnLoad(es);
        }

        for (int i = _runtimeCanvasRoot.transform.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(_runtimeCanvasRoot.transform.GetChild(i).gameObject);
        }

        RectTransform rootRt = _runtimeCanvasRoot.GetComponent<RectTransform>();
        GameObject panel = CreateUiObject("Panel", rootRt, new Vector2(520f, 520f), Vector2.zero);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

        Text title = CreateText("Title", panel.transform as RectTransform, "DOOR KEYPAD", 32, new Vector2(0f, 220f), new Vector2(500f, 48f));
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;

        _runtimeInputText = CreateText("Input", panel.transform as RectTransform, "----", 30, new Vector2(0f, 176f), new Vector2(500f, 40f));
        _runtimeInputText.alignment = TextAnchor.MiddleCenter;
        _runtimeInputText.color = new Color(0.9f, 0.95f, 1f, 1f);

        float bw = 120f;
        float bh = 80f;
        float gx = 22f;
        float gy = 18f;
        float startX = -142f;
        float startY = 90f;

        CreateDigitButton(panel.transform as RectTransform, "1", new Vector2(startX + ((bw + gx) * 0), startY - ((bh + gy) * 0)), bw, bh);
        CreateDigitButton(panel.transform as RectTransform, "2", new Vector2(startX + ((bw + gx) * 1), startY - ((bh + gy) * 0)), bw, bh);
        CreateDigitButton(panel.transform as RectTransform, "3", new Vector2(startX + ((bw + gx) * 2), startY - ((bh + gy) * 0)), bw, bh);
        CreateDigitButton(panel.transform as RectTransform, "4", new Vector2(startX + ((bw + gx) * 0), startY - ((bh + gy) * 1)), bw, bh);
        CreateDigitButton(panel.transform as RectTransform, "5", new Vector2(startX + ((bw + gx) * 1), startY - ((bh + gy) * 1)), bw, bh);
        CreateDigitButton(panel.transform as RectTransform, "6", new Vector2(startX + ((bw + gx) * 2), startY - ((bh + gy) * 1)), bw, bh);
        CreateDigitButton(panel.transform as RectTransform, "7", new Vector2(startX + ((bw + gx) * 0), startY - ((bh + gy) * 2)), bw, bh);
        CreateDigitButton(panel.transform as RectTransform, "8", new Vector2(startX + ((bw + gx) * 1), startY - ((bh + gy) * 2)), bw, bh);
        CreateDigitButton(panel.transform as RectTransform, "9", new Vector2(startX + ((bw + gx) * 2), startY - ((bh + gy) * 2)), bw, bh);
        CreateDigitButton(panel.transform as RectTransform, "0", new Vector2(startX + ((bw + gx) * 1), startY - ((bh + gy) * 3)), bw, bh);

        CreateActionButton(panel.transform as RectTransform, "CLEAR", new Vector2(-110f, -230f), new Vector2(170f, 44f), () =>
        {
            currentInput = string.Empty;
            RefreshRuntimeInputText();
        });

        CreateActionButton(panel.transform as RectTransform, "CANCEL", new Vector2(110f, -230f), new Vector2(170f, 44f), EndInputSession);
        _runtimeCanvasRoot.SetActive(false);
    }

    private static GameObject CreateUiObject(string name, RectTransform parent, Vector2 size, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return go;
    }

    private Text CreateText(string name, RectTransform parent, string value, int fontSize, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = CreateUiObject(name, parent, size, anchoredPos);
        Text text = go.AddComponent<Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.color = Color.white;
        return text;
    }

    private void CreateDigitButton(RectTransform parent, string digit, Vector2 anchoredPos, float width, float height)
    {
        GameObject go = CreateUiObject("Btn_" + digit, parent, new Vector2(width, height), anchoredPos);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.24f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            InputDigit(digit);
        });

        Text text = CreateText("Label", go.transform as RectTransform, digit, 30, Vector2.zero, new Vector2(width, height));
        text.alignment = TextAnchor.MiddleCenter;
    }

    private void CreateActionButton(RectTransform parent, string label, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction callback)
    {
        GameObject go = CreateUiObject("Btn_" + label, parent, size, anchoredPos);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.18f, 0.18f, 0.21f, 1f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(callback);
        Text text = CreateText("Label", go.transform as RectTransform, label, 20, Vector2.zero, size);
        text.alignment = TextAnchor.MiddleCenter;
    }

    private void RefreshRuntimeInputText()
    {
        if (_runtimeInputText == null)
        {
            return;
        }

        _runtimeInputText.text = currentInput.PadRight(maxInputLength, '-');
    }

    private void RefreshStatus()
    {
        if (statusText == null)
        {
            return;
        }

        if (isOpened)
        {
            statusText.text = "OPEN";
            statusText.color = new Color(0.35f, 1f, 0.45f, 1f);
            return;
        }

        statusText.text = "CLOSE";
        statusText.color = new Color(1f, 0.4f, 0.35f, 1f);
    }

    private void LateUpdate()
    {
        if (isSessionActive)
        {
            EnsureRuntimeUi();
            if (_runtimeCanvasRoot != null && !_runtimeCanvasRoot.activeSelf)
            {
                _runtimeCanvasRoot.SetActive(true);
            }
            RefreshRuntimeInputText();
        }
        else if (_runtimeCanvasRoot != null && _runtimeCanvasRoot.activeSelf)
        {
            _runtimeCanvasRoot.SetActive(false);
        }
    }
}
