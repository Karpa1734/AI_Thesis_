using UnityEngine;
using TMPro;

public class ExperimentSetupUI : MonoBehaviour
{
    [Header("Participant ID")]
    [SerializeField]
    private TMP_InputField subjectIdInput;

    [Header("Warning")]
    [SerializeField]
    private TextMeshProUGUI warningText;

    public bool IsInputFieldFocused()
    {
        return subjectIdInput != null && subjectIdInput.isFocused;
    }

    // 参加者IDを登録する処理
    public bool SetParticipantId()
    {
        if (subjectIdInput == null)
        {
            Debug.LogError("参加者ID入力欄が設定されていません。");
            return false;
        }

        string subjectId = subjectIdInput.text.Trim();

        if (string.IsNullOrEmpty(subjectId))
        {
            if (warningText != null)
                warningText.text = "参加者IDを入力してください。";

            return false;
        }

        if (warningText != null)
            warningText.text = "";

        if (BattleMetricsManager.Instance == null)
        {
            Debug.LogError("BattleMetricsManagerが見つかりません。");
            return false;
        }

        bool success =
            BattleMetricsManager.Instance.ConfigureTrial(subjectId);

        if (success)
        {
            Debug.Log($"参加者IDの登録成功：{subjectId}");
            return true;
        }

        Debug.LogError("参加者IDの登録に失敗しました。");
        return false;
    }

    // UnityのButtonから呼び出すためのメソッド
    public void OnClickSetParticipantId()
    {
        SetParticipantId();
    }
}