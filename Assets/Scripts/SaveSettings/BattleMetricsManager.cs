using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public class BattleMetricsManager : MonoBehaviour
{
    public static BattleMetricsManager Instance { get; private set; }

    // ==========================================
    // 実験情報
    // ==========================================

    [System.Serializable]
    public class TrialMeta
    {
        public string subjectId = "";
        public string mode = "Proposed";
        public float timeLimit = 180f;
        public string timestamp;
    }

    [System.Serializable]
    public class BattleResultData
    {
        public string result = "Victory";
        public float duration;
        public float enemyRemainingHP;
        public float enemyRemainingHPRatio;
    }

    [System.Serializable]
    public class SkillStat
    {
        public int used;
        public int hits;
        public int blockedHits;
    }

    [System.Serializable]
    public class SkillUsageData
    {
        public SkillStat skillZ_Aim = new SkillStat();
        public SkillStat skillX_Trap = new SkillStat();
        public SkillStat skillC_Special = new SkillStat();
        public SkillStat skillV_Guard = new SkillStat();
    }

    [System.Serializable]
    public class PlayerPerformanceData
    {
        public float totalDamageDealt;
        public float totalDamageTaken;
        public int hitCountTaken;
        public int grazeCount;

        // DefensiveFieldによって消去した自機弾数
        public int playerBulletsClearedByField;

        public float averageDPS;
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

    // ==========================================
    // 内部変数
    // ==========================================

    private bool isTracking = false;

    private float matchDurationTimer = 0f;

    private string currentSubjectId = "";

    private string logDirectory;
    private string reportFilePath;
    private string eventFilePath;
    private string movementLogPath;

    private bool eventHeaderWritten = false;

    private const string EXPERIMENT_MODE = "Proposed";
    private const float TIME_LIMIT = 180f;

    private static readonly CultureInfo CsvCulture =
        CultureInfo.InvariantCulture;

    // 移動ログ
    private float movementSampleTimer = 0f;
    private Vector2 lastMovementPosition;
    private bool hasPreviousMovementPosition = false;

    private const float MovementSampleInterval = 0.1f;

    // ==========================================
    // Singleton / シーン遷移対応
    // ==========================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ==========================================
    // 参加者ID設定
    // ==========================================

    public bool ConfigureTrial(string subjectId)
    {
        if (isTracking)
        {
            Debug.LogWarning(
                "[BattleMetrics] 計測中はIDを変更できません。");

            return false;
        }

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            Debug.LogError(
                "[BattleMetrics] 参加者IDが入力されていません。");

            return false;
        }

        subjectId = subjectId.Trim();

        if (!Regex.IsMatch(subjectId, @"^[A-Za-z0-9_-]+$"))
        {
            Debug.LogError(
                "[BattleMetrics] 参加者IDが不正です。");

            return false;
        }

        currentSubjectId = subjectId;

        Debug.Log(
            $"[BattleMetrics] 参加者ID設定: {currentSubjectId}");

        return true;
    }

    // ==========================================
    // 計測開始
    // ==========================================

    public void StartTracking()
    {
        if (isTracking)
        {
            Debug.LogWarning(
                "[BattleMetrics] 既に計測中です。");

            return;
        }

        if (string.IsNullOrWhiteSpace(currentSubjectId))
        {
            Debug.LogError(
                "[BattleMetrics] 参加者IDが設定されていません。");

            return;
        }

        // 保存先ディレクトリを初期化
        logDirectory = GetLogDirectory();

        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[BattleMetrics] 保存先作成失敗: {e.Message}");

            return;
        }

        // レポート初期化
        report = new BattleReportRoot();

        report.trialMeta.subjectId = currentSubjectId;
        report.trialMeta.mode = EXPERIMENT_MODE;
        report.trialMeta.timeLimit = TIME_LIMIT;

        report.trialMeta.timestamp =
            DateTimeOffset.Now.ToString("o");

        // ファイルパス設定
        reportFilePath = Path.Combine(
            logDirectory,
            $"{currentSubjectId}_{EXPERIMENT_MODE}_BattleReport.csv"
        );

        eventFilePath = Path.Combine(
            logDirectory,
            $"{currentSubjectId}_{EXPERIMENT_MODE}_EventLog.csv"
        );

        movementLogPath = Path.Combine(
            logDirectory,
            $"{currentSubjectId}_{EXPERIMENT_MODE}_MovementLog.csv"
        );

        // 時間・移動情報初期化
        matchDurationTimer = 0f;

        movementSampleTimer = 0f;
        hasPreviousMovementPosition = false;

        eventHeaderWritten = false;

        // 移動ログ初期化
        try
        {
            File.WriteAllText(
                movementLogPath,
                "SubjectId,Mode,Time,X,Y,Speed,InputH,InputV,Slow\n",
                new UTF8Encoding(true)
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[BattleMetrics] 移動ログ初期化失敗: {e.Message}");

            movementLogPath = null;
            return;
        }

        isTracking = true;

        Debug.Log(
            $"[BattleMetrics] 実験ログ保存先: {logDirectory}");

        Debug.Log(
            $"[BattleMetrics] BattleReport: {reportFilePath}");

        Debug.Log(
            $"[BattleMetrics] EventLog: {eventFilePath}");

        Debug.Log(
            $"[BattleMetrics] MovementLog: {movementLogPath}");

        RecordEvent("GameStart");

        Debug.Log(
            $"[BattleMetrics] 提案モード計測開始: {currentSubjectId}");
    }

    private void Update()
    {
        if (!isTracking) return;

        // ポーズ中は時間を計測しない
        matchDurationTimer += Time.deltaTime;
    }
    // ==========================================
    // イベントログ
    // ==========================================
    // ==========================================
    // 自機弾消去記録（DefensiveField）
    // ==========================================

    public void RecordPlayerBulletClearedByField()
    {
        if (!isTracking) return;

        report.playerPerformance.playerBulletsClearedByField++;

        // EventLogにも記録
        RecordEvent("PlayerBulletClearedByField");
    }
    public void RecordEvent(
        string eventType,
        string skillType = "",
        float value = 0f)
    {
        if (!isTracking) return;

        try
        {
            bool fileExists = File.Exists(eventFilePath);

            using (StreamWriter writer = new StreamWriter(
                eventFilePath,
                true,
                new UTF8Encoding(true)))
            {
                if (!fileExists && !eventHeaderWritten)
                {
                    writer.WriteLine(
                        "SubjectId,Mode,Time,EventType,SkillType,Value");

                    eventHeaderWritten = true;
                }

                string row = string.Join(",",
                    EscapeCsv(currentSubjectId),
                    EXPERIMENT_MODE,
                    matchDurationTimer.ToString("F3", CsvCulture),
                    EscapeCsv(eventType),
                    EscapeCsv(skillType),
                    value.ToString("F2", CsvCulture)
                );

                writer.WriteLine(row);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[BattleMetrics] EventLog保存失敗: {e.Message}");
        }
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ==========================================
    // ダメージ記録
    // ==========================================

    public void RecordDamageEvent(bool isPlayerHit, float damage)
    {
        if (!isTracking) return;

        if (isPlayerHit)
        {
            report.playerPerformance.totalDamageTaken += damage;
            report.playerPerformance.hitCountTaken++;

            report.enemyPerformance.totalDamageDealt += damage;

            RecordEvent("DamageTaken", "", damage);
        }
        else
        {
            report.playerPerformance.totalDamageDealt += damage;
            report.enemyPerformance.totalDamageTaken += damage;

            RecordEvent("DamageDealt", "", damage);
        }
    }

    // ==========================================
    // スキル使用記録
    // ==========================================

    public void RecordSkillUsage(
        string skillKey,
        bool isHit = false)
    {
        if (!isTracking) return;

        switch (skillKey)
        {
            case "Z":
                report.playerPerformance.skillUsage.skillZ_Aim.used++;

                if (isHit)
                    report.playerPerformance.skillUsage.skillZ_Aim.hits++;

                break;

            case "X":
                report.playerPerformance.skillUsage.skillX_Trap.used++;

                if (isHit)
                    report.playerPerformance.skillUsage.skillX_Trap.hits++;

                break;

            case "C":
                report.playerPerformance.skillUsage.skillC_Special.used++;

                if (isHit)
                    report.playerPerformance.skillUsage.skillC_Special.hits++;

                break;

            case "V":
                report.playerPerformance.skillUsage.skillV_Guard.used++;

                if (isHit)
                {
                    report.playerPerformance.skillUsage.skillV_Guard.blockedHits++;
                }

                break;

            default:
                Debug.LogWarning(
                    $"[BattleMetrics] 未定義のスキル: {skillKey}");

                return;
        }

        RecordEvent("SkillUse", skillKey, isHit ? 1f : 0f);
    }

    // ==========================================
    // グレイズ記録
    // ==========================================

    public void RecordGraze()
    {
        if (!isTracking) return;

        report.playerPerformance.grazeCount++;

        RecordEvent("Graze");
    }

    // ==========================================
    // 戦闘終了・保存
    // ==========================================

    public void EndTrackingAndExport(
        bool isVictory,
        float enemyCurrentHP,
        float enemyMaxHP)
    {
        if (!isTracking) return;

        report.battleResult.duration = matchDurationTimer;

        report.battleResult.result =
            isVictory ? "Victory" : "TimeUpDefeat";

        report.battleResult.enemyRemainingHP =
            Mathf.Max(0f, enemyCurrentHP);

        report.battleResult.enemyRemainingHPRatio =
            enemyMaxHP > 0f
                ? Mathf.Clamp01(enemyCurrentHP / enemyMaxHP)
                : 0f;

        report.playerPerformance.averageDPS =
            matchDurationTimer > 0f
                ? report.playerPerformance.totalDamageDealt /
                  matchDurationTimer
                : 0f;

        report.enemyPerformance.totalDamageDealt =
            report.playerPerformance.totalDamageTaken;

        RecordEvent(
            isVictory ? "Victory" : "TimeUpDefeat"
        );

        isTracking = false;

        ExportToCsv();

        Debug.Log(
            $"[BattleMetrics] 提案モード実験終了: {currentSubjectId}");
    }

    // ==========================================
    // BattleReport CSV出力
    // ==========================================

    private void ExportToCsv()
    {
        try
        {
            bool fileExists = File.Exists(reportFilePath);

            string header =
                "Timestamp,SubjectId,Mode,TimeLimit,Result,Duration,EnemyRemainingHP,EnemyRemainingHPRatio," +
                "TotalDamageDealt,TotalDamageTaken,HitCountTaken,GrazeCount," +
                "EnemyBulletsClearedByField,AverageDPS," +
                "SkillZ_Used,SkillZ_Hits,SkillX_Used,SkillX_Hits," +
                "SkillC_Used,SkillC_Hits,SkillV_Used,SkillV_Blocked";

            var p = report.playerPerformance;
            var s = p.skillUsage;
            var b = report.battleResult;
            var t = report.trialMeta;

            string row = string.Join(",",
                EscapeCsv(t.timestamp),
                EscapeCsv(t.subjectId),
                EXPERIMENT_MODE,
                t.timeLimit.ToString("F2", CsvCulture),
                EscapeCsv(b.result),
                b.duration.ToString("F2", CsvCulture),
                b.enemyRemainingHP.ToString("F2", CsvCulture),
                b.enemyRemainingHPRatio.ToString("F4", CsvCulture),

                p.totalDamageDealt.ToString("F2", CsvCulture),
                p.totalDamageTaken.ToString("F2", CsvCulture),
                p.hitCountTaken,
                p.grazeCount,

                // DefensiveFieldによる自機弾消去数
                p.playerBulletsClearedByField,

                p.averageDPS.ToString("F2", CsvCulture),

                s.skillZ_Aim.used,
                s.skillZ_Aim.hits,
                s.skillX_Trap.used,
                s.skillX_Trap.hits,
                s.skillC_Special.used,
                s.skillC_Special.hits,
                s.skillV_Guard.used,
                s.skillV_Guard.blockedHits
            );

            using (StreamWriter writer = new StreamWriter(
                reportFilePath,
                true,
                new UTF8Encoding(true)))
            {
                if (!fileExists)
                    writer.WriteLine(header);

                writer.WriteLine(row);
            }

            Debug.Log(
                $"[BattleMetrics] BattleReport保存成功: {reportFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[BattleMetrics] BattleReport保存失敗: {e}");
        }
    }

    // ==========================================
    // 移動ログ
    // ==========================================

    /// <summary>
    /// プレイヤーの移動情報を0.1秒間隔で記録する
    /// </summary>
    public void RecordMovement(
        Vector2 position,
        float inputH,
        float inputV,
        bool slow)
    {
        if (!IsTracking()) return;
        if (string.IsNullOrEmpty(movementLogPath)) return;
        movementSampleTimer += Time.deltaTime;

        if (movementSampleTimer < MovementSampleInterval)
            return;

        float elapsed = movementSampleTimer;
        movementSampleTimer = 0f;

        float speed = 0f;

        if (hasPreviousMovementPosition)
        {
            speed = Vector2.Distance(
                position,
                lastMovementPosition
            ) / elapsed;
        }

        lastMovementPosition = position;
        hasPreviousMovementPosition = true;

        string line = string.Join(",",
            EscapeCsv(currentSubjectId),
            EXPERIMENT_MODE,
            matchDurationTimer.ToString("F2", CsvCulture),
            position.x.ToString("F4", CsvCulture),
            position.y.ToString("F4", CsvCulture),
            speed.ToString("F4", CsvCulture),
            inputH.ToString("F2", CsvCulture),
            inputV.ToString("F2", CsvCulture),
            slow ? "1" : "0"
        );

        try
        {
            File.AppendAllText(
                movementLogPath,
                line + "\n",
                new UTF8Encoding(true)
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[BattleMetrics] MovementLog保存失敗: {e.Message}");
        }
    }

    // ==========================================
    // 保存先取得
    // ==========================================

    /// <summary>
    /// 従来モードと共通の保存先
    /// </summary>
    public string GetLogDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ExperimentLogs"
        );
    }

    public bool IsTracking()
    {
        return isTracking;
    }
}