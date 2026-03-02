using UnityEngine;

public class ManagerDebugCheckLogger : MonoBehaviour
{
    [Header("Target Managers (Optional)")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private InteractionManager interactionManager;
    [SerializeField] private TransferSystem transferSystem;

    [Header("Logging")]
    [SerializeField] private bool logInput = true;
    [SerializeField] private bool logInteraction = true;
    [SerializeField] private bool logTransfer = true;

    private void OnEnable()
    {
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

        if (inputManager != null)
        {
            inputManager.OnPrimaryInteractPressed += HandlePrimaryInteractPressed;
            inputManager.OnSecondaryInteractHeldChanged += HandleSecondaryInteractHeldChanged;
            inputManager.OnTransferClickPressed += HandleTransferClickPressed;
            inputManager.OnSecondaryInteractDragDelta += HandleSecondaryDragDelta;
            inputManager.OnPostureChanged += HandlePostureChanged;
            inputManager.OnJumpRequested += HandleJumpRequested;
        }

        if (interactionManager != null)
        {
            interactionManager.OnInteractionPromptChanged += HandlePromptChanged;
            interactionManager.OnDragActionTriggered += HandleDragActionTriggered;
        }

        if (transferSystem != null)
        {
            transferSystem.OnChannelingStateChanged += HandleChannelingStateChanged;
            transferSystem.OnTransferFeedbackRequested += HandleTransferFeedbackRequested;
            transferSystem.OnHandStateChanged += HandleHandStateChanged;
        }
    }

    private void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.OnPrimaryInteractPressed -= HandlePrimaryInteractPressed;
            inputManager.OnSecondaryInteractHeldChanged -= HandleSecondaryInteractHeldChanged;
            inputManager.OnTransferClickPressed -= HandleTransferClickPressed;
            inputManager.OnSecondaryInteractDragDelta -= HandleSecondaryDragDelta;
            inputManager.OnPostureChanged -= HandlePostureChanged;
            inputManager.OnJumpRequested -= HandleJumpRequested;
        }

        if (interactionManager != null)
        {
            interactionManager.OnInteractionPromptChanged -= HandlePromptChanged;
            interactionManager.OnDragActionTriggered -= HandleDragActionTriggered;
        }

        if (transferSystem != null)
        {
            transferSystem.OnChannelingStateChanged -= HandleChannelingStateChanged;
            transferSystem.OnTransferFeedbackRequested -= HandleTransferFeedbackRequested;
            transferSystem.OnHandStateChanged -= HandleHandStateChanged;
        }
    }

    private void HandlePrimaryInteractPressed()
    {
        if (logInput)
        {
            Debug.Log("[CHECK][Input] E pressed");
        }
    }

    private void HandleSecondaryInteractHeldChanged(bool isHeld)
    {
        if (logInput)
        {
            Debug.Log($"[CHECK][Input] F hold changed: {isHeld}");
        }
    }

    private void HandleTransferClickPressed(MouseButtonType button)
    {
        if (logInput)
        {
            Debug.Log($"[CHECK][Input] Transfer click: {button}");
        }
    }

    private void HandleSecondaryDragDelta(Vector2 delta)
    {
        if (logInput)
        {
            Debug.Log($"[CHECK][Input] Drag delta: {delta}");
        }
    }

    private void HandlePostureChanged(PlayerPostureState posture)
    {
        if (logInput)
        {
            Debug.Log($"[CHECK][Input] Posture: {posture}");
        }
    }

    private void HandleJumpRequested(float jumpValue)
    {
        if (logInput)
        {
            Debug.Log($"[CHECK][Input] Jump requested: {jumpValue}");
        }
    }

    private void HandlePromptChanged(GameObject target, string prompt)
    {
        if (logInteraction)
        {
            string targetName = target != null ? target.name : "(none)";
            Debug.Log($"[CHECK][Interaction] Prompt target={targetName}, prompt={prompt}");
        }
    }

    private void HandleDragActionTriggered(DragActionType action)
    {
        if (logInteraction)
        {
            Debug.Log($"[CHECK][Interaction] Drag action: {action}");
        }
    }

    private void HandleChannelingStateChanged(HandSide hand, bool isChanneling)
    {
        if (logTransfer)
        {
            Debug.Log($"[CHECK][Transfer] Channeling {hand}: {isChanneling}");
        }
    }

    private void HandleTransferFeedbackRequested(HandSide hand, string message)
    {
        if (logTransfer)
        {
            Debug.Log($"[CHECK][Transfer] {hand}: {message}");
        }
    }

    private void HandleHandStateChanged(HandSide hand)
    {
        if (!logTransfer || transferSystem == null)
        {
            return;
        }

        HandSlotState state = hand == HandSide.Left ? transferSystem.LeftHandState : transferSystem.RightHandState;
        Debug.Log($"[CHECK][Transfer] Hand state changed {hand}: has={state.HasProperty}, id={state.PropertyId}");
    }
}
