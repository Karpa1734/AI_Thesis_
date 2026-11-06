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
        public int trialNumber = 1;
        public string characterId = "";

        // 試行全体の制限時間
        public float timeLimit = 180f;

        // フェーズ構成
        public int phaseCount = 1;
        public float phaseTimeLimit = 180f;

        public string timestamp;
    }
    [System.Serializable]
    public class BattleResultData
    {
        public string result = "Victory";
        public float duration;

        public float enemyRemainingHP;
        public float enemyRemainingHPRatio;

        public float playerRemainingHP;
        public float playerRemainingHPRatio;
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
        // DefensiveFieldによって消去した敵弾数
        public int enemyBulletsClearedByField;

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

    private string currentMode = "Proposed";
    private int currentTrialNumber = 1;
    private string currentCharacterId = "";

    private float currentTimeLimit = 180f;
    private int currentPhaseCount = 1;
    private float currentPhaseTimeLimit = 180f;

    private static readonly CultureInfo CsvCulture =
        CultureInfo.InvariantCulture;

    // 移動ログ
    private float movementSampleTimer = 0f;
    private Vector2 lastMovementPosition;
    private bool hasPreviousMovementPosition = false;

    private const float MovementSampleInterval = 0.1f;
    private string snapshotLogPath;

    private float lastSnapshotTime = 0f;
    private const float SnapshotSampleInterval = 10f;
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

    public bool ConfigureTrial(
        string subjectId,
        string mode = "Proposed",
        int trialNumber = 1,
        string characterId = "",
        float timeLimit = 180f,
        int phaseCount = 1,
        float phaseTimeLimit = 180f)
    {
        if (isTracking)
        {
            Debug.LogWarning("[BattleMetrics] 計測中は設定を変更できません。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            Debug.LogError("[BattleMetrics] 参加者IDが入力されていません。");
            return false;
        }

        subjectId = subjectId.Trim();

        if (!Regex.IsMatch(subjectId, @"^[A-Za-z0-9_-]+$"))
        {
            Debug.LogError("[BattleMetrics] 参加者IDが不正です。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(mode) ||
            trialNumber < 1 ||
            timeLimit <= 0f ||
            phaseCount < 1 ||
            phaseTimeLimit <= 0f)
        {
            Debug.LogError("[BattleMetrics] 実験条件が不正です。");
            return false;
        }

        currentSubjectId = subjectId;
        currentMode = mode;
        currentTrialNumber = trialNumber;
        currentCharacterId = characterId;
        currentTimeLimit = timeLimit;
        currentPhaseCount = phaseCount;
        currentPhaseTimeLimit = phaseTimeLimit;

        Debug.Log($"[BattleMetrics] 実験設定: {subjectId}, {mode}, Trial {trialNumber}");

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
        report.trialMeta.mode = currentMode;
        report.trialMeta.trialNumber = currentTrialNumber;
        report.trialMeta.characterId = currentCharacterId;

        report.trialMeta.timeLimit = currentTimeLimit;
        report.trialMeta.phaseCount = currentPhaseCount;
        report.trialMeta.phaseTimeLimit = currentPhaseTimeLimit;

        report.trialMeta.timestamp =
            DateTimeOffset.Now.ToString("o");

        snapshotLogPath = Path.Combine(
    logDirectory,
    $"{currentSubjectId}_{currentMode}_Trial{currentTrialNumber}_BattleSnapshotLog.csv"
);

        // ファイルパス設定
        reportFilePath = Path.Combine(
            logDirectory,
            $"{currentSubjectId}_{report.trialMeta.mode}_BattleReport.csv"
        );

        eventFilePath = Path.Combine(
            logDirectory,
            $"{currentSubjectId}_{report.trialMeta.mode}_EventLog.csv"
        );

        movementLogPath = Path.Combine(
            logDirectory,
            $"{currentSubjectId}_{report.trialMeta.mode}_MovementLog.csv"
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

        lastSnapshotTime = 0f;

        try
        {
            File.WriteAllText(
                snapshotLogPath,
                "SubjectId,Mode,TrialNumber,ElapsedTime," +
                "PlayerHP,PlayerHPRatio," +
                "EnemyHP,EnemyHPRatio," +
                "TotalDamageDealt,TotalDamageTaken," +
                "HitCountTaken\n",
                new UTF8Encoding(true)
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[BattleMetrics] SnapshotLog初期化失敗: {e.Message}"
            );

            snapshotLogPath = null;
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

    /// <summary>
    /// 10秒間隔で戦闘状況を記録する
    /// </summary>
    public void RecordBattleSnapshot(
        float playerCurrentHP,
        float playerMaxHP,
        float enemyCurrentHP,
        float enemyMaxHP)
    {
        if (!isTracking) return;
        if (string.IsNullOrEmpty(snapshotLogPath)) return;

        if (matchDurationTimer - lastSnapshotTime < SnapshotSampleInterval)
            return;

        lastSnapshotTime = matchDurationTimer;

        float playerRatio = playerMaxHP > 0f
            ? Mathf.Clamp01(playerCurrentHP / playerMaxHP)
            : 0f;

        float enemyRatio = enemyMaxHP > 0f
            ? Mathf.Clamp01(enemyCurrentHP / enemyMaxHP)
            : 0f;

        var p = report.playerPerformance;

        string line = string.Join(",",
            EscapeCsv(currentSubjectId),
            EscapeCsv(currentMode),
            currentTrialNumber,
            matchDurationTimer.ToString("F2", CsvCulture),
            playerCurrentHP.ToString("F2", CsvCulture),
            playerRatio.ToString("F4", CsvCulture),
            enemyCurrentHP.ToString("F2", CsvCulture),
            enemyRatio.ToString("F4", CsvCulture),
            p.totalDamageDealt.ToString("F2", CsvCulture),
            p.totalDamageTaken.ToString("F2", CsvCulture),
            p.hitCountTaken,
            p.grazeCount
        );

        try
        {
            File.AppendAllText(
                snapshotLogPath,
                line + "\n",
                new UTF8Encoding(true)
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[BattleMetrics] SnapshotLog保存失敗: {e.Message}"
            );
        }
    }

    /// <summary>
    /// DefensiveFieldによって敵弾を消去したときの記録
    /// </summary>
    public void RecordEnemyBulletClearedByField()
    {
        if (!isTracking) return;

        // 敵弾消去数
        report.playerPerformance.enemyBulletsClearedByField++;

        // Vスキルの防御成功回数
        RecordSkillBlocked("V");

        // EventLogにも記録
        RecordEvent("EnemyBulletClearedByField", "V");
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
                    report.trialMeta.mode,
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

    // スキルを発動したとき
    public void RecordSkillUsage(string skillKey)
    {
        if (!isTracking) return;

        SkillStat stat = GetSkillStat(skillKey);
        if (stat == null) return;

        stat.used++;

        RecordEvent("SkillUse", skillKey);
    }

    // スキルが敵に命中・効果を与えたとき
    public void RecordSkillHit(string skillKey, int count = 1)
    {
        if (!isTracking || count <= 0) return;

        SkillStat stat = GetSkillStat(skillKey);
        if (stat == null) return;

        stat.hits += count;

        RecordEvent("SkillHit", skillKey, count);
    }

    // Vなどの防御スキルで攻撃を防いだとき
    public void RecordSkillBlocked(string skillKey, int count = 1)
    {
        if (!isTracking || count <= 0) return;

        SkillStat stat = GetSkillStat(skillKey);
        if (stat == null) return;

        stat.blockedHits += count;

        RecordEvent("SkillBlocked", skillKey, count);
    }

    private SkillStat GetSkillStat(string skillKey)
    {
        switch (skillKey)
        {
            case "Z":
                return report.playerPerformance.skillUsage.skillZ_Aim;

            case "X":
                return report.playerPerformance.skillUsage.skillX_Trap;

            case "C":
                return report.playerPerformance.skillUsage.skillC_Special;

            case "V":
                return report.playerPerformance.skillUsage.skillV_Guard;

            default:
                Debug.LogWarning(
                    $"[BattleMetrics] 未定義のスキル: {skillKey}");
                return null;
        }
    }

    // ==========================================
    // グレイズ記録
    // ==========================================


    // ==========================================
    // 戦闘終了・保存
    // ==========================================

    public void EndTrackingAndExport(
        bool isVictory,
        float enemyCurrentHP,
        float enemyMaxHP,
        float playerCurrentHP,
        float playerMaxHP)
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

        report.battleResult.playerRemainingHP =
            Mathf.Max(0f, playerCurrentHP);

        report.battleResult.playerRemainingHPRatio =
            playerMaxHP > 0f
                ? Mathf.Clamp01(playerCurrentHP / playerMaxHP)
                : 0f;

        report.playerPerformance.averageDPS =
            matchDurationTimer > 0f
                ? report.playerPerformance.totalDamageDealt / matchDurationTimer
                : 0f;

        report.enemyPerformance.totalDamageDealt =
            report.playerPerformance.totalDamageTaken;

        RecordEvent(isVictory ? "Victory" : "TimeUpDefeat");

        isTracking = false;

        ExportToCsv();

        Debug.Log($"[BattleMetrics] 実験終了: {currentSubjectId}");
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
                "Timestamp,SubjectId,Mode,TrialNumber,CharacterId," +
                "TimeLimit,PhaseCount,PhaseTimeLimit,Result,Duration," +
                "EnemyRemainingHP,EnemyRemainingHPRatio," +
                "PlayerRemainingHP,PlayerRemainingHPRatio," +
                "TotalDamageDealt,TotalDamageTaken,HitCountTaken," +
                "EnemyBulletsClearedByField," +
                "AverageDPS," +
                "SkillZ_Used,SkillZ_Hits," +
                "SkillX_Used,SkillX_Hits," +
                "SkillC_Used,SkillC_Hits," +
                "SkillV_Used,SkillV_Blocked";

            var p = report.playerPerformance;
            var s = p.skillUsage;
            var b = report.battleResult;
            var t = report.trialMeta;

            string row = string.Join(",",
                EscapeCsv(t.timestamp),
                EscapeCsv(t.subjectId),
                EscapeCsv(t.mode),
                t.trialNumber,
                EscapeCsv(t.characterId),

                t.timeLimit.ToString("F2", CsvCulture),
                t.phaseCount,
                t.phaseTimeLimit.ToString("F2", CsvCulture),

                EscapeCsv(b.result),
                b.duration.ToString("F2", CsvCulture),

                b.enemyRemainingHP.ToString("F2", CsvCulture),
                b.enemyRemainingHPRatio.ToString("F4", CsvCulture),

                b.playerRemainingHP.ToString("F2", CsvCulture),
                b.playerRemainingHPRatio.ToString("F4", CsvCulture),

                p.totalDamageDealt.ToString("F2", CsvCulture),
                p.totalDamageTaken.ToString("F2", CsvCulture),
                p.hitCountTaken,

                p.enemyBulletsClearedByField,

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
            report.trialMeta.mode,
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