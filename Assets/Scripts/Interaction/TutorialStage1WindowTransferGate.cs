using UnityEngine;

public class TutorialStage1WindowTransferGate : MonoBehaviour, ITransferInteractionGate
{
    [SerializeField] private TutorialStage1Seesaw seesaw;
    [SerializeField] private float requiredTiltNormalized = 0.9f;
    [SerializeField] private bool requirePositiveTilt = true;
    [SerializeField] private string blockedMessage = "Seesaw is not high enough to reach the window.";

    public bool CanTransferInteraction(out string reason)
    {
        reason = string.Empty;
        if (seesaw == null)
        {
            reason = "Seesaw link is missing.";
            return false;
        }

        float maxAngle = Mathf.Max(0.01f, seesaw.MaxOperationalAngleZ);
        float currentAngle = seesaw.CurrentAngleZ;

        if (requirePositiveTilt && currentAngle <= 0f)
        {
            reason = blockedMessage;
            return false;
        }

        float normalized = Mathf.Abs(currentAngle) / maxAngle;
        if (normalized < Mathf.Clamp01(requiredTiltNormalized))
        {
            reason = blockedMessage;
            return false;
        }

        return true;
    }
}
