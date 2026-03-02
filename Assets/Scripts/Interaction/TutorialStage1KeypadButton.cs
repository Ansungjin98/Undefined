using UnityEngine;

public class TutorialStage1KeypadButton : MonoBehaviour
{
    [SerializeField] private string digit = "1";
    [SerializeField] private TutorialStage1DoorKeypad doorKeypad;
    [SerializeField] public bool BlockTransferClick = true;
    [SerializeField] private string promptText = "Click - Press button";

    public string GetInteractionPrompt()
    {
        return promptText;
    }

    public void HandleClick(MouseButtonType buttonType)
    {
        if (doorKeypad == null)
        {
            Debug.LogWarning($"[TutorialStage1KeypadButton] Door keypad reference missing on {name}");
            return;
        }

        doorKeypad.InputDigit(digit);
    }

    public void HandlePrimaryInteraction()
    {
        if (doorKeypad == null)
        {
            Debug.LogWarning($"[TutorialStage1KeypadButton] Door keypad reference missing on {name}");
            return;
        }

        doorKeypad.InputDigit(digit);
    }
}
