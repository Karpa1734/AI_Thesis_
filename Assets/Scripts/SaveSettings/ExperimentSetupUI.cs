using UnityEngine;
using TMPro;

public class ExperimentSetupUI : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField subjectIdInput;

    public void SetParticipantId()
    {
        string subjectId = subjectIdInput.text.Trim();

        if (BattleMetricsManager.Instance == null)
        {
            Debug.LogError("BattleMetricsManager‚ªŒ©‚Â‚©‚è‚Ü‚¹‚ñ");
            return;
        }

        bool success =
            BattleMetricsManager.Instance.ConfigureTrial(subjectId);

        if (success)
        {
            Debug.Log($"Q‰ÁÒID‚Ì“o˜^¬Œ÷F{subjectId}");
        }
    }
}