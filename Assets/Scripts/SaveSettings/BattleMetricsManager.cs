using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BattleMetricsManager : MonoBehaviour
{
    public static BattleMetricsManager Instance { get; private set; }

    [System.Serializable]
    public class TrialMeta
    {
        public string subjectId = "P01";
        public string mode = "Proposed"; // "Proposed" (提案モード) または "Comparative" (比較モード)
        public int playOrder = 1;
        public float timeLimit = 180.0f;
        public string timestamp;
    }

    [System.Serializable]
    public class BattleResultData
    {
        public string result = "Victory"; // "Victory" (敵撃破) または "TimeUpDefeat" (時間切れ敗北)
        public float duration;            // 決着までの実秒数
        public float enemyRemainingHP;    // 敵の残りHP（タイムアップ時は残量が入る）
        public float enemyRemainingHPRatio; // 0.0 〜 1.0
    }

    [System.Serializable]
    public class SkillStat
    {
        public int used;
        public int hits;
        public int blockedHits; // 回避・防御系用
    }

    [System.Serializable]
    public class SkillUsageData
    {
        public SkillStat skillZ_Aim = new SkillStat();      // Z: 自機狙い
        public SkillStat skillX_Trap = new SkillStat();     // X: 自機外し
        public SkillStat skillC_Special = new SkillStat();  // C: 必殺技
        public SkillStat skillV_Guard = new SkillStat();    // V: 防御・回避
    }

    [System.Serializable]
    public class PlayerPerformanceData
    {
        public float totalDamageDealt;   // 敵に与えた総ダメージ
        public float totalDamageTaken;   // 自機が受けた総被ダメージ（記録用）
        public int hitCountTaken;        // 自機の総被弾回数
        public int grazeCount;           // 総グレイズ数
        public float averageDPS;         // 平均DPS
        public SkillUsageData skillUsage = new SkillUsageData();
    }

    [System.Serializable]
    public class EnemyPerformanceData
    {
        public float totalDamageDealt;
        public float totalDamageTaken;
        public int hitCountDealt;
    }

    [System.Serializable]
    public class BattleReportRoot
    {
        public TrialMeta trialMeta = new TrialMeta();
        public BattleResultData battleResult = new BattleResultData();
        public PlayerPerformanceData playerPerformance = new PlayerPerformanceData();
        public EnemyPerformanceData enemyPerformance = new EnemyPerformanceData();
    }

    public BattleReportRoot report = new BattleReportRoot();

    private bool isTracking = false;
    private float matchDurationTimer = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartTracking()
    {
        report = new BattleReportRoot();
        report.trialMeta.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        matchDurationTimer = 0f;
        isTracking = true;
        Debug.Log("📊 [BattleMetrics] 実験用ログの計測を開始しました。");
    }

    private void Update()
    {
        if (!isTracking) return;

        if (PlayerMove.CanShoot)
        {
            matchDurationTimer += Time.deltaTime;
        }
    }

    /// <summary>
    /// ダメージ発生時に PlayerStatusManager から呼び出される中継窓口
    /// </summary>
    public void RecordDamageEvent(bool isPlayerHit, float damage)
    {
        if (!isTracking) return;

        if (isPlayerHit)
        {
            // 自機（1P）が被弾
            report.playerPerformance.totalDamageTaken += damage;
            report.playerPerformance.hitCountTaken++;

            report.enemyPerformance.totalDamageDealt += damage;
            report.enemyPerformance.hitCountDealt++;
        }
        else
        {
            // 敵（2P）がダメージを受けた（自機が与えた）
            report.playerPerformance.totalDamageDealt += damage;
            report.enemyPerformance.totalDamageTaken += damage;
        }
    }

    /// <summary>
    /// スキルの使用回数やヒット数を記録する
    /// </summary>
    /// <param name="skillKey">"Z", "X", "C", "V"</param>
    /// <param name="isHit">命中したか</param>
    public void RecordSkillUsage(string skillKey, bool isHit = false)
    {
        if (!isTracking) return;

        switch (skillKey)
        {
            case "Z":
                report.playerPerformance.skillUsage.skillZ_Aim.used++;
                if (isHit) report.playerPerformance.skillUsage.skillZ_Aim.hits++;
                break;
            case "X":
                report.playerPerformance.skillUsage.skillX_Trap.used++;
                if (isHit) report.playerPerformance.skillUsage.skillX_Trap.hits++;
                break;
            case "C":
                report.playerPerformance.skillUsage.skillC_Special.used++;
                if (isHit) report.playerPerformance.skillUsage.skillC_Special.hits++;
                break;
            case "V":
                report.playerPerformance.skillUsage.skillV_Guard.used++;
                if (isHit) report.playerPerformance.skillUsage.skillV_Guard.blockedHits++;
                break;
        }
    }

    /// <summary>
    /// 戦闘終了時（敵撃破 or 時間切れ）に呼び出し、データをJSONに出力する
    /// </summary>
    /// <param name="isVictory">true: 敵撃破クリア, false: 時間切れ敗北</param>
    /// <param name="enemyCurrentHP">終了時の敵のHP</param>
    /// <param name="enemyMaxHP">敵の最大HP</param>
    public void EndTrackingAndExport(bool isVictory, float enemyCurrentHP, float enemyMaxHP)
    {
        if (!isTracking) return;
        isTracking = false;

        // 決着データの格納
        report.battleResult.duration = matchDurationTimer;
        report.battleResult.result = isVictory ? "Victory" : "TimeUpDefeat";
        report.battleResult.enemyRemainingHP = Mathf.Max(0f, enemyCurrentHP);
        report.battleResult.enemyRemainingHPRatio = enemyMaxHP > 0f ? Mathf.Clamp01(enemyCurrentHP / enemyMaxHP) : 0f;

        // プレイヤーパフォーマンスの集計
        report.playerPerformance.averageDPS = matchDurationTimer > 0f ? report.playerPerformance.totalDamageDealt / matchDurationTimer : 0f;
        report.enemyPerformance.totalDamageDealt = report.playerPerformance.totalDamageTaken;

        Debug.Log($"=== 📊 実験用戦闘データ結果 [{report.battleResult.result}] ===");
        Debug.Log($"クリアタイム: {report.battleResult.duration:F2}秒 | 敵残りHP率: {report.battleResult.enemyRemainingHPRatio * 100f:F1}%");

        ExportToJson();
    }

    private void ExportToJson()
    {
        string jsonString = JsonUtility.ToJson(report, true);
        string filePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BattleReport.json");

        try
        {
            File.WriteAllText(filePath, jsonString);
            Debug.Log($"💾 [BattleMetrics] 実験用JSON出力完了: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [BattleMetrics] 出力失敗: {e.Message}");
        }
    }
}