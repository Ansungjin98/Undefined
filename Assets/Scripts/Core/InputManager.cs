using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum PlayerPostureState
{
    Standing,
    Crouch,
    Prone
}

public enum MouseButtonType
{
    Left,
    Right
}

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Movement Tuning")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeedMultiplier = 1.5f;
    [SerializeField] private float crouchSpeedMultiplier = 0.5f;
    [SerializeField] private float proneSpeedMultiplier = 0.3f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float mouseSensitivity = 2f;

    [Header("Jump Hook")]
    [SerializeField] private float runningJumpDistanceBonusMultiplier = 1.15f;

    [Header("Input System")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private InputActionAsset inputActionsAsset;
#else
    [SerializeField] private UnityEngine.Object inputActionsAsset;
#endif
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string runActionName = "Run";
    [SerializeField] private string crouchActionName = "Crouch";
    [SerializeField] private string proneActionName = "Prone";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string primaryInteractActionName = "PrimaryInteract";
    [SerializeField] private string secondaryInteractActionName = "SecondaryInteract";
    [SerializeField] private string leftClickActionName = "LeftClick";
    [SerializeField] private string rightClickActionName = "RightClick";

    [Header("Optional Manager References")]
    [SerializeField] private MonoBehaviour gameManager;
    [SerializeField] private MonoBehaviour levelFlowManager;

    [Header("First Person Demo (Spec Visibility)")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float cameraFieldOfView = 95f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float lookPitchMin = -80f;
    [SerializeField] private float lookPitchMax = 80f;
    [SerializeField] private float standControllerHeight = 1.8f;
    [SerializeField] private float crouchControllerHeight = 1.2f;
    [SerializeField] private float proneControllerHeight = 0.7f;
    [SerializeField] private float standCameraLocalY = 0.75f;
    [SerializeField] private float crouchCameraLocalY = 0.45f;
    [SerializeField] private float proneCameraLocalY = 0.2f;
    [SerializeField] private float cameraHeightOffset = 0.5f;
    [SerializeField] private bool autoFindPlayerComponents = true;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool IsRunPressed { get; private set; }
    public PlayerPostureState CurrentPosture { get; private set; } = PlayerPostureState.Standing;
    public float CurrentMoveSpeed { get; private set; }
    public float LastJumpForceApplied { get; private set; }

    public event Action OnPrimaryInteractPressed;
    public event Action<bool> OnSecondaryInteractHeldChanged;
    public event Action<MouseButtonType> OnTransferClickPressed;
    public event Action<Vector2> OnSecondaryInteractDragDelta;
    public event Action<PlayerPostureState> OnPostureChanged;
    public event Action<float> OnJumpRequested;

#if ENABLE_INPUT_SYSTEM
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _runAction;
    private InputAction _crouchAction;
    private InputAction _proneAction;
    private InputAction _jumpAction;
    private InputAction _primaryInteractAction;
    private InputAction _secondaryInteractAction;
    private InputAction _leftClickAction;
    private InputAction _rightClickAction;
#endif

    private bool _secondaryInteractHeld;
    private bool _warnedNoInputSystem;
    private bool _actionsInitialized;
    private bool _fallbackSecondaryHeld;
    private bool _fallbackLeftHeld;
    private bool _fallbackRightHeld;
    private float _lookPitch;
    private float _verticalVelocity;
    private bool _jumpQueued;
    private float _queuedJumpVelocity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[InputManager] Duplicate instance detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        AutoAssignPlayerComponents();
        CurrentPosture = PlayerPostureState.Standing;
        RecalculateCurrentMoveSpeed();
        InitializeInputActions();
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        SetActionsEnabled(true);
#endif
    }

    private void Start()
    {
        AutoAssignPlayerComponents();
        ApplyPostureVisuals();
        ApplyCameraFieldOfView();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (!_actionsInitialized)
        {
            return;
        }

        MoveInput = ReadVector2(_moveAction);
        LookInput = ReadVector2(_lookAction) * mouseSensitivity;

        if (WasPressedThisFrame(_crouchAction))
        {
            TogglePosture(PlayerPostureState.Crouch);
        }

        if (WasPressedThisFrame(_proneAction))
        {
            TogglePosture(PlayerPostureState.Prone);
        }

        if (WasPressedThisFrame(_jumpAction))
        {
            TryJump();
        }

        IsRunPressed = CanRun() && IsPressed(_runAction);
        RecalculateCurrentMoveSpeed();
        ApplyLook();
        ApplyMovement();

        if (WasPressedThisFrame(_primaryInteractAction))
        {
            OnPrimaryInteractPressed?.Invoke();
        }

        bool secondaryHeldNow = IsPressed(_secondaryInteractAction);
        if (_secondaryInteractAction == null)
        {
            secondaryHeldNow = ReadSecondaryFallbackHeld();
        }
        if (secondaryHeldNow != _secondaryInteractHeld)
        {
            _secondaryInteractHeld = secondaryHeldNow;
            OnSecondaryInteractHeldChanged?.Invoke(_secondaryInteractHeld);
        }

        if (_secondaryInteractHeld && LookInput.sqrMagnitude > 0f)
        {
            OnSecondaryInteractDragDelta?.Invoke(LookInput);
        }

        bool leftPressed = WasPressedThisFrame(_leftClickAction);
        if (_leftClickAction == null)
        {
            leftPressed = ReadLeftClickFallbackPressed();
        }
        if (leftPressed)
        {
            OnTransferClickPressed?.Invoke(MouseButtonType.Left);
        }

        bool rightPressed = WasPressedThisFrame(_rightClickAction);
        if (_rightClickAction == null)
        {
            rightPressed = ReadRightClickFallbackPressed();
        }
        if (rightPressed)
        {
            OnTransferClickPressed?.Invoke(MouseButtonType.Right);
        }
#else
        if (!_warnedNoInputSystem)
        {
            _warnedNoInputSystem = true;
            Debug.LogWarning("[InputManager] ENABLE_INPUT_SYSTEM is disabled. InputManager is inactive.");
        }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        SetActionsEnabled(false);
#endif

        if (_secondaryInteractHeld)
        {
            _secondaryInteractHeld = false;
            OnSecondaryInteractHeldChanged?.Invoke(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool TrySetPosture(PlayerPostureState posture)
    {
        if (CurrentPosture == posture)
        {
            return true;
        }

        CurrentPosture = posture;
        RecalculateCurrentMoveSpeed();
        ApplyPostureVisuals();
        OnPostureChanged?.Invoke(CurrentPosture);
        return true;
    }

    public bool TryJump()
    {
        if (CurrentPosture == PlayerPostureState.Prone)
        {
            Debug.LogWarning("[InputManager] Jump blocked while prone.");
            return false;
        }

        if (!CanStartJump())
        {
            Debug.LogWarning("[InputManager] Jump blocked while airborne.");
            return false;
        }

        if (CurrentPosture == PlayerPostureState.Crouch)
        {
            // 명세: 앉은 상태 점프는 먼저 서기로 복귀 후 점프 시도.
            TrySetPosture(PlayerPostureState.Standing);
        }

        float jumpValue = jumpForce;
        if (IsRunPressed && MoveInput.sqrMagnitude > 0f)
        {
            jumpValue *= runningJumpDistanceBonusMultiplier;
        }

        LastJumpForceApplied = jumpValue;
        _queuedJumpVelocity = jumpValue;
        _jumpQueued = true;
        OnJumpRequested?.Invoke(jumpValue);
        return true;
    }

    private void TogglePosture(PlayerPostureState posture)
    {
        if (CurrentPosture == posture)
        {
            TrySetPosture(PlayerPostureState.Standing);
        }
        else
        {
            TrySetPosture(posture);
        }
    }

    private bool CanRun()
    {
        return CurrentPosture == PlayerPostureState.Standing;
    }

    private void RecalculateCurrentMoveSpeed()
    {
        float speed = walkSpeed;

        switch (CurrentPosture)
        {
            case PlayerPostureState.Crouch:
                speed *= crouchSpeedMultiplier;
                break;
            case PlayerPostureState.Prone:
                speed *= proneSpeedMultiplier;
                break;
            default:
                if (IsRunPressed && MoveInput.sqrMagnitude > 0f)
                {
                    speed *= runSpeedMultiplier;
                }
                break;
        }

        CurrentMoveSpeed = speed;
    }

    private void AutoAssignPlayerComponents()
    {
        if (!autoFindPlayerComponents)
        {
            return;
        }

        if (playerTransform == null)
        {
            playerTransform = transform;
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
    }

    private void ApplyLook()
    {
        if (playerTransform == null)
        {
            return;
        }

        playerTransform.Rotate(Vector3.up, LookInput.x * Time.deltaTime, Space.Self);

        if (playerCamera == null)
        {
            return;
        }

        _lookPitch -= LookInput.y * Time.deltaTime;
        _lookPitch = Mathf.Clamp(_lookPitch, lookPitchMin, lookPitchMax);

        Vector3 localEuler = playerCamera.transform.localEulerAngles;
        localEuler.x = _lookPitch;
        localEuler.y = 0f;
        localEuler.z = 0f;
        playerCamera.transform.localEulerAngles = localEuler;
    }

    private void ApplyMovement()
    {
        if (characterController == null || playerTransform == null)
        {
            return;
        }

        bool isGrounded = characterController.isGrounded;
        if (isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f;
        }

        if (_jumpQueued)
        {
            if (isGrounded)
            {
                _verticalVelocity = _queuedJumpVelocity;
            }

            _jumpQueued = false;
            _queuedJumpVelocity = 0f;
        }

        Vector3 move = (playerTransform.forward * MoveInput.y) + (playerTransform.right * MoveInput.x);
        move = Vector3.ClampMagnitude(move, 1f) * CurrentMoveSpeed;

        _verticalVelocity += gravity * Time.deltaTime;
        move.y = _verticalVelocity;

        characterController.Move(move * Time.deltaTime);
    }

    private bool CanStartJump()
    {
        if (characterController == null)
        {
            return true;
        }

        return characterController.isGrounded;
    }

    private void ApplyPostureVisuals()
    {
        if (characterController != null)
        {
            float targetHeight = standControllerHeight;
            switch (CurrentPosture)
            {
                case PlayerPostureState.Crouch:
                    targetHeight = crouchControllerHeight;
                    break;
                case PlayerPostureState.Prone:
                    targetHeight = proneControllerHeight;
                    break;
            }

            characterController.height = targetHeight;
            characterController.center = new Vector3(0f, targetHeight * 0.5f, 0f);
        }

        if (playerCamera != null)
        {
            Vector3 localPos = playerCamera.transform.localPosition;
            switch (CurrentPosture)
            {
                case PlayerPostureState.Crouch:
                    localPos.y = crouchCameraLocalY + cameraHeightOffset;
                    break;
                case PlayerPostureState.Prone:
                    localPos.y = proneCameraLocalY + cameraHeightOffset;
                    break;
                default:
                    localPos.y = standCameraLocalY + cameraHeightOffset;
                    break;
            }

            playerCamera.transform.localPosition = localPos;
        }
    }

    private void ApplyCameraFieldOfView()
    {
        if (playerCamera == null)
        {
            return;
        }

        playerCamera.fieldOfView = Mathf.Clamp(cameraFieldOfView, 75f, 110f);
    }

#if ENABLE_INPUT_SYSTEM
    private void InitializeInputActions()
    {
        if (inputActionsAsset == null)
        {
            Debug.LogWarning("[InputManager] InputActionAsset is not assigned. Input features are disabled.");
            _actionsInitialized = false;
            return;
        }

        _moveAction = FindAction(moveActionName, "Player/Move");
        _lookAction = FindAction(lookActionName, "Player/Look");
        _runAction = FindAction(runActionName, "Player/Run", "Player/Sprint");
        _crouchAction = FindAction(crouchActionName, "Player/Crouch");
        _proneAction = FindAction(proneActionName, "Player/Prone");
        _jumpAction = FindAction(jumpActionName, "Player/Jump");
        _primaryInteractAction = FindAction(primaryInteractActionName, "Player/PrimaryInteract", "Player/Interact", "Interact");
        _secondaryInteractAction = FindAction(
            secondaryInteractActionName,
            "Player/SecondaryInteract",
            "SecondaryInteract",
            "Player/Secondary",
            "Secondary",
            "Player/UseSecondary",
            "UseSecondary",
            "Player/AltInteract",
            "AltInteract");
        // Support older/current project input assets where transfer click is stored as Attack(UI Click) instead of LeftClick.
        _leftClickAction = FindAction(
            leftClickActionName,
            "Player/LeftClick",
            "LeftClick",
            "Player/Fire",
            "Player/Attack",
            "Attack",
            "UI/Click",
            "Click");
        _rightClickAction = FindAction(
            rightClickActionName,
            "Player/RightClick",
            "RightClick",
            "Player/AltFire",
            "AltFire",
            "UI/RightClick");

        _actionsInitialized = _moveAction != null && _lookAction != null;
        if (!_actionsInitialized)
        {
            Debug.LogWarning("[InputManager] Required actions (Move/Look) not found in InputActionAsset.");
        }

        if (_leftClickAction == null)
        {
            Debug.LogWarning("[InputManager] Left transfer click action not found. LMB transfer will not work.");
        }

        if (_rightClickAction == null)
        {
            Debug.LogWarning("[InputManager] Right transfer click action not found. RMB transfer will not work.");
        }

        if (_secondaryInteractAction == null)
        {
            Debug.LogWarning("[InputManager] SecondaryInteract action not found. F+drag interactions will not work.");
        }
    }

    private InputAction FindAction(params string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            InputAction action = inputActionsAsset.FindAction(candidate, false);
            if (action != null)
            {
                return action;
            }
        }

        return null;
    }

    private void SetActionsEnabled(bool enabled)
    {
        if (!_actionsInitialized)
        {
            return;
        }

        ToggleAction(_moveAction, enabled);
        ToggleAction(_lookAction, enabled);
        ToggleAction(_runAction, enabled);
        ToggleAction(_crouchAction, enabled);
        ToggleAction(_proneAction, enabled);
        ToggleAction(_jumpAction, enabled);
        ToggleAction(_primaryInteractAction, enabled);
        ToggleAction(_secondaryInteractAction, enabled);
        ToggleAction(_leftClickAction, enabled);
        ToggleAction(_rightClickAction, enabled);
    }

    private static void ToggleAction(InputAction action, bool enabled)
    {
        if (action == null)
        {
            return;
        }

        if (enabled)
        {
            action.Enable();
        }
        else
        {
            action.Disable();
        }
    }

    private static Vector2 ReadVector2(InputAction action)
    {
        return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }

    private static bool IsPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    private static bool WasPressedThisFrame(InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }

    private bool ReadSecondaryFallbackHeld()
    {
        bool held = Input.GetKey(KeyCode.F);
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            held |= Keyboard.current.fKey.isPressed;
        }
#endif
        return held;
    }

    private bool ReadLeftClickFallbackPressed()
    {
        bool held = Input.GetMouseButton(0);
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            held |= Mouse.current.leftButton.isPressed;
        }
#endif
        bool pressed = held && !_fallbackLeftHeld;
        _fallbackLeftHeld = held;
        return pressed;
    }

    private bool ReadRightClickFallbackPressed()
    {
        bool held = Input.GetMouseButton(1);
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            held |= Mouse.current.rightButton.isPressed;
        }
#endif
        bool pressed = held && !_fallbackRightHeld;
        _fallbackRightHeld = held;
        return pressed;
    }
#else
    private void InitializeInputActions()
    {
        _actionsInitialized = false;
    }
#endif
}
