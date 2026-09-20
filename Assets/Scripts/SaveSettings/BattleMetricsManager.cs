using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BattleMetricsManager : MonoBehaviour
{
    public static BattleMetricsManager Instance { get; private set; }

    [System.Serializable]
    public class CombatStats
    {
        public string entityName;
        public float totalDamageDealt;   // 与えた総ダメージ
        public float totalDamageTaken;   // 受けた総ダメージ（被ダメージ）
        public int hitCountDealt;        // 攻撃ヒット回数
        public int damageTakenCount;     // 被弾回数
        public float combatDuration;     // 戦闘経過時間（秒）

        public float peakDPS;            // 🌟 瞬間最大DPS（1秒間あたりの最大与ダメージ）

        // 平均DPS
        public float AverageDPS => combatDuration > 0f ? totalDamageDealt / combatDuration : 0f;
    }

    public CombatStats playerStats = new CombatStats { entityName = "Player_1P" };
    public CombatStats enemyStats = new CombatStats { entityName = "Boss_2P" };

    // 瞬間最大DPS計測用の内部バッファ
    private float _playerCurrentSecondDamage = 0f;
    private float _enemyCurrentSecondDamage = 0f;
    private float _secondTimer = 0f;

    private bool isTracking = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartTracking()
    {
        playerStats = new CombatStats { entityName = "Player_1P" };
        enemyStats = new CombatStats { entityName = "Boss_2P" };
        _playerCurrentSecondDamage = 0f;
        _enemyCurrentSecondDamage = 0f;
        _secondTimer = 0f;
        isTracking = true;
        Debug.Log("📊 [BattleMetrics] 戦闘データの計測を開始しました。");
    }

    private void Update()
    {
        if (!isTracking) return;

        if (PlayerMove.CanShoot)
        {
            float dt = Time.deltaTime;
            playerStats.combatDuration += dt;
            enemyStats.combatDuration += dt;

            _secondTimer += dt;
            if (_secondTimer >= 1.0f)
            {
                // 1秒ごとの区切りで瞬間最大DPS（Peak DPS）を更新・比較
                if (_playerCurrentSecondDamage > playerStats.peakDPS)
                    playerStats.peakDPS = _playerCurrentSecondDamage;

                if (_enemyCurrentSecondDamage > enemyStats.peakDPS)
                    enemyStats.peakDPS = _enemyCurrentSecondDamage;

                // 次の1秒分の計測へリセット
                _playerCurrentSecondDamage = 0f;
                _enemyCurrentSecondDamage = 0f;
                _secondTimer = 0f;
            }
        }
    }

    /// <summary>
    /// ダメージ発生時に PlayerStatusManager から呼び出される中継窓口
    /// </summary>
    /// <param name="isPlayerHit">trueなら1Pが被弾（2Pが与えた）、falseなら2Pが被弾（1Pが与えた）</param>
    /// <param name="damage">ダメージ量</param>
    public void RecordDamageEvent(bool isPlayerHit, float damage)
    {
        if (!isTracking) return;

        if (isPlayerHit)
        {
            // 1P（自機）がダメージを受けた
            playerStats.totalDamageTaken += damage;
            playerStats.damageTakenCount++;

            // 2P（敵）がダメージを与えた
            enemyStats.totalDamageDealt += damage;
            enemyStats.hitCountDealt++;
            _enemyCurrentSecondDamage += damage;
        }
        else
        {
            // 2P（敵）がダメージを受けた
            enemyStats.totalDamageTaken += damage;
            enemyStats.damageTakenCount++;

            // 1P（自機）がダメージを与えた
            playerStats.totalDamageDealt += damage;
            playerStats.hitCountDealt++;
            _playerCurrentSecondDamage += damage;
        }
    }

    public void EndTrackingAndExport()
    {
        if (!isTracking) return;
        isTracking = false;

        Debug.Log($"=== 📊 戦闘データ結果 ===");
        Debug.Log($"【1P プレイヤー】 与ダメージ: {playerStats.totalDamageDealt} | 被ダメージ: {playerStats.totalDamageTaken} | 平均DPS: {playerStats.AverageDPS:F2} | 瞬間最大DPS: {playerStats.peakDPS:F2} | 被弾回数: {playerStats.damageTakenCount}");
        Debug.Log($"【2P ボス】 与ダメージ: {enemyStats.totalDamageDealt} | 被ダメージ: {enemyStats.totalDamageTaken} | 平均DPS: {enemyStats.AverageDPS:F2} | 瞬間最大DPS: {enemyStats.peakDPS:F2} | 被弾回数: {enemyStats.damageTakenCount}");

        ExportToJson();
    }

    private void ExportToJson()
    {
        BattleReportWrapper report = new BattleReportWrapper
        {
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            player = playerStats,
            enemy = enemyStats
        };

        string jsonString = JsonUtility.ToJson(report, true);

        // プロジェクト直下（画像と同じ階層）に出力したい場合はこちら
        string filePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BattleReport.json");

        // 🚨 修正：try と catch を正しく対にして記述する
        try
        {
            File.WriteAllText(filePath, jsonString);
            Debug.Log($"💾 [BattleMetrics] JSON出力完了: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [BattleMetrics] 出力失敗: {e.Message}");
        }
    }

    [System.Serializable]
    private class BattleReportWrapper
    {
        public string timestamp;
        public CombatStats player;
        public CombatStats enemy;
    }
}