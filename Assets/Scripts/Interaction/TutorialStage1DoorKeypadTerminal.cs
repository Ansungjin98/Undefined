using UnityEngine;

public class TutorialStage1DoorKeypadTerminal : MonoBehaviour
{
    [SerializeField] private TutorialStage1DoorKeypad doorKeypad;
    [SerializeField] private string promptText = "E - Use keypad";
    [SerializeField] public bool BlockTransferClick = true;

    public string GetInteractionPrompt()
    {
        return promptText;
    }

    public void HandlePrimaryInteraction()
    {
        if (doorKeypad == null)
        {
            Debug.LogWarning("[DoorKeypadTerminal] Door keypad reference missing.");
            return;
        }

        doorKeypad.BeginInputSession();
    }

    public void HandleClick(MouseButtonType buttonType)
    {
        HandlePrimaryInteraction();
    }
}
