using KanKikuchi.AudioManager;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AppUI.UI;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// プレイヤーのスキル設定に基づき、実際に弾幕を生成・射出するクラス
/// 1vs1対戦対応：奇数弾は自機狙い、偶数弾は自機外しを自動計算
/// </summary>
public class PlayerDanmakuEmitter : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("攻撃対象（相手）のタグ")]
    public string targetTag = "Player";

    protected GameObject _rootOwner;
    protected bool _isArcReversed = false;
    protected int _activeSkillCoroutines = 0;
    public bool IsAnySkillActive => _activeSkillCoroutines > 0;
    protected bool _isEXSkillActive = false;

    public bool IsUltimateSkillActive => _isEXSkillActive;

    // 🌲 ストーリーモード中の連射制御用タイマー
    private float _storyFireTimer = 0f;

    private static int _smallOrderCounter = 5000;
    private static int _mediumOrderCounter = 10000;
    private static int _largeOrderCounter = 15000;

    private void Awake()
    {
        _rootOwner = transform.root.gameObject;
    }

    private void Update()
    {
        // 🌲 ストーリー用の連射タイマーを毎フレーム 0 に向かって減衰させる
        if (_storyFireTimer > 0f)
        {
            _storyFireTimer -= Time.deltaTime;
        }
    }

    protected float GetAngleToTarget()
    {
        Transform target = null;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != _rootOwner)
            {
                target = p.transform;
                break;
            }
        }
        if (target != null)
        {
            Vector3 dir = target.position - transform.position;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus != null && myStatus.playerId == 2) return 180f;
        return 0f;
    }

    protected float GetAngleToTarget(Vector3 fromPos)
    {
        Transform target = null;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != _rootOwner)
            {
                target = p.transform;
                break;
            }
        }
        if (target != null)
        {
            Vector3 dir = target.position - fromPos;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus != null && myStatus.playerId == 2) return 180f;
        return 0f;
    }

    public void Fire(PlayerSkillData.SkillSettings s)
    {
        if (!enabled)
        {
            PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
            foreach (var emitter in allEmitters)
            {
                if (emitter != null && emitter.enabled)
                {
                    emitter.Fire(s);
                    return;
                }
            }
            return;
        }

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (!PlayerMove.CanShoot) return;
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;
        if (s.bulletData == null || s.bulletData.bulletPrefab == null) return;

        // =========================================================================
        // 🌲【ストーリーモード時】：Zスキルのみ、0.05秒間隔の超高速連射で直進ショットを撃つ
        // =========================================================================
        if (GameModeManager.IsStoryMode)
        {
            if (_storyFireTimer > 0f) return;
            _storyFireTimer = 0.05f;

            Vector3 spawnPos = transform.position + new Vector3(0.3f, -0.2f, 0f);
            float straightAngle = 0f;
            for (int i = 0; i < 2; i++)
            {
                CreateShot(
                    data: s.bulletData,
                    pos: spawnPos,
                    speed: s.speed > 0f ? s.speed : 10f,
                    angle: straightAngle,
                    delay: 1.0f,
                    isConverge: false,
                    accel: 0f,
                    maxSpeed: 0f,
                    customMaterial: null,
                    customScale: 1.0f,
                    isIndestructible: false
                );
                spawnPos += new Vector3(0, 0.4f, 0f);
            }
            PlaySkillSE(s.sePath);
            return;
        }
        // =========================================================================

        if (_isEXSkillActive && s.patternType != SkillPatternType.Line) return;

        var myStatusMgr = GetComponentInParent<PlayerStatusManager>();
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();

        bool isMyVjtActive = (myStatusMgr != null && myStatusMgr.isSpellCardActive);

        if (myMove != null && myStatusMgr != null && !isMyVjtActive && s.skillName != myStatusMgr.characterData.skillZ.skillName)
        {
            float finalGain = s.ultimateGain;
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            if (myStatus != null && myStatus.isOverheated)
            {
                finalGain *= 0.5f;
            }
            myMove.AddUltimateEnergy(finalGain);
        }

        if (!_isEXSkillActive && s.moveSpeedMultiplier < 1.0f)
        {
            StartCoroutine(TemporarySlow(s.moveSpeedMultiplier, 0.2f));
        }

        // =========================================================================
        // ⚔️【VSモード（対戦等）時】：SkillPatternTypeに応じた自走パターン分岐生成
        // =========================================================================
        float baseTargetAngle = GetAngleToTarget() + s.angleOffset;
        float bulletSpeed = s.speed > 0f ? s.speed : 8f;
        Vector3 pos = transform.position;

        switch (s.patternType)
        {
            case SkillPatternType.Round:
                // ⭕ 全方位弾（RoundShot）
                int roundCount = s.count > 0 ? s.count : 8;
                float roundStep = 360f / roundCount;
                for (int i = 0; i < roundCount; i++)
                {
                    float angle = baseTargetAngle + (roundStep * i);
                    ExecuteSubShot(s.bulletData, pos, bulletSpeed, angle, 0f, bulletSpeed, targetTag, gameObject.layer, s.delay);
                }
                break;

            case SkillPatternType.nWay:
                // 📐 扇形・N-Way弾（WideShot / 自機狙い外しなど）
                int wayCount = s.count > 0 ? s.count : 3;
                float wideAngle = s.wideAngle > 0f ? s.wideAngle : 45f;

                if (wayCount == 1)
                {
                    ExecuteSubShot(s.bulletData, pos, bulletSpeed, baseTargetAngle, 0f, bulletSpeed, targetTag, gameObject.layer, s.delay);
                }
                else
                {
                    float startAngle = baseTargetAngle - (wideAngle / 2f);
                    float stepAngle = wideAngle / (wayCount - 1);
                    for (int i = 0; i < wayCount; i++)
                    {
                        float angle = startAngle + (stepAngle * i);
                        ExecuteSubShot(s.bulletData, pos, bulletSpeed, angle, 0f, bulletSpeed, targetTag, gameObject.layer, s.delay);
                    }
                }
                break;

            case SkillPatternType.Line:
                // 📏 直線連射弾（LineShot）
                int lineCount = s.count > 0 ? s.count : 3;
                for (int i = 0; i < lineCount; i++)
                {
                    float currentSpeed = bulletSpeed + (0.3f * i);
                    ExecuteSubShot(s.bulletData, pos, currentSpeed, baseTargetAngle, 0f, currentSpeed, targetTag, gameObject.layer, s.delay);
                }
                break;

            case SkillPatternType.Standard:
            default:
                // 🎯 標準：一発の自機狙い弾（または自機外しオフセット付き）
                ExecuteSubShot(
                    data: s.bulletData,
                    pos: pos,
                    speed: bulletSpeed,
                    angle: baseTargetAngle,
                    accel: 0f,
                    maxSpeed: 0f,
                    tag: targetTag,
                    layer: gameObject.layer,
                    delay_: s.delay > 0f ? s.delay : 1.0f
                );
                break;
            case SkillPatternType.Saiki:
                StartCoroutine(ChargeAndExecuteDefensiveField(s));
                break;

        }

        PlaySkillSE(s.sePath);
    }

    // --- ★ 追加：防御フィールド専用のチャージ演出ルーチン ---
    // 📄 PlayerDanmakuEmitter.cs 内の防御フィールド制御セクター【領域展開・動的巨大延長版】
    private IEnumerator ChargeAndExecuteDefensiveField(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; //
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>(); //

        if (myMove != null) //
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier; //
        }

        // 💡 1. 領域展開中（スペルカード発動中）であるかステートをチェック
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        // 💡 2. 【高橋さんの指定】：領域中ならサイズと持続時間の変数を動的にブースト！
        float finalFieldDuration = 1.0f; // 通常時の持続秒数
        float finalFieldScale = 2.0f;    // 通常時のDefensiveFieldインスペクター想定スケール

        if (isSpellActive)
        {
            finalFieldDuration = 2.0f;   // 🎯 領域展開中：持続時間を「3.0秒」へ延長（2倍）
            finalFieldScale = 3.5f;      // 🎯 領域展開中：サイズ（最大スケール）を「3.5倍」へ巨大化
            Debug.Log($"<color=gold>🔮【領域展開・絶対防壁】防御フィールドを極大化！ Duration: {finalFieldDuration}s, Scale: {finalFieldScale}</color>");
        }

        // チャージ演出
        float chargeTime = 0.1f; //
        if (BossEffectManager.Instance != null) //
        {
            BossEffectManager.Instance.PlayChargeEffect(chargeTime, s.bulletData.breakColor, transform.position); //
        }
        yield return new WaitForSeconds(chargeTime + 0.2f); //

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SLASH, 0.5f); //
        }

        // 💡 3. 変調されたサイズと持続時間を手渡しして、スキル本体を実体化！
        ExecuteDefensiveField(s, finalFieldDuration, finalFieldScale);

        // 💡 4. 【インフラ完全同期】：スキル終了まで待機（引き伸ばされた動的持続時間に正確に合わせる）
        yield return new WaitForSeconds(finalFieldDuration);

        // 倍率を戻す
        if (myMove != null) //
        {
            myMove.skillSpeedMultiplier = 1.0f; //
        }
        _activeSkillCoroutines--; //
    }

    // 🎯【引数拡張】：外部変調パラメータを確実に受け取れるようにオーバーロード調停
    private void ExecuteDefensiveField(PlayerSkillData.SkillSettings s, float duration, float scale)
    {
        GameObject fieldObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity); //
        var myStatus = GetComponentInParent<PlayerStatusManager>(); //
        int ownerId = (myStatus != null) ? myStatus.playerId : 1; //
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet"; //
        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet"); //

        var field = fieldObj.GetComponent<DefensiveField>(); //
        if (field == null) field = fieldObj.AddComponent<DefensiveField>(); //

        // 💡 拡張された Initialize 窓口へパラメータを一挙にインジェクション！
        field.Initialize(transform, s.bulletData, duration, assignedTag, assignedLayer, scale);
    }
    public void FireEX(PlayerSkillData.SkillSettings s)
    {
        if (!enabled)
        {
            PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
            foreach (var emitter in allEmitters)
            {
                if (emitter != null && emitter.enabled)
                {
                    emitter.FireEX(s);
                    return;
                }
            }
            return;
        }

        if (!PlayerMove.CanShoot) return;

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;

        if (_isEXSkillActive) return;

        StartCoroutine(ExecuteEXInfrastructureRoutine(s));
    }

    protected IEnumerator ExecuteEXInfrastructureRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();

        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        PlaySkillSE(s.sePath);

        try
        {
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isZoneActive = (myStatus != null && myStatus.isSpellCardActive);

            PlayerSkillData.SkillSettings enhancedEXSettings = s;

            if (isZoneActive)
            {
                enhancedEXSettings.speed = s.speed * 1.3f;
                s = enhancedEXSettings;
            }

            yield return StartCoroutine(ExecuteSkillEX(s));
        }
        finally
        {
            if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
        }
        yield return null;
    }

    protected virtual IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s) { yield return null; }

    public void ExecuteSubShot(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer, float delay_ = 0f)
    {
        if (data == null || data.bulletPrefab == null) return;
        if (delay_ < 1) { delay_ = 1; }

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.1f);
        }

        CreateShot(
            data: data,
            pos: pos,
            speed: speed,
            angle: angle,
            delay: delay_,
            isConverge: false,
            accel: accel,
            maxSpeed: maxSpeed,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: false
        );
    }

    public void ExecuteSubShot02(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer, float angularVelocity = 0f, float maxRotationLimit = 0f)
    {
        if (data == null || data.bulletPrefab == null) return;

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.1f);
        }

        CreateShot(
            data: data,
            pos: pos,
            speed: speed,
            angle: angle,
            delay: 0,
            isConverge: false,
            accel: accel,
            maxSpeed: maxSpeed,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: true,
            angularVelocity: angularVelocity,
            maxRotationLimit: maxRotationLimit
        );
    }

    public DanmakuBullet ExecuteSubShot02_Returnable(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer, float angularVelocity = 0f, float maxRotationLimit = 0f)
    {
        if (data == null || data.bulletPrefab == null) return null;

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.1f);
        }

        return CreateShot(
            data: data,
            pos: pos,
            speed: speed,
            angle: angle,
            delay: 0f,
            isConverge: false,
            accel: accel,
            maxSpeed: maxSpeed,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: true,
            angularVelocity: angularVelocity,
            maxRotationLimit: maxRotationLimit
        );
    }

    protected void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    protected DanmakuBullet CreateShot(BulletData data, Vector3 pos, float speed, float angle, float delay,
                                       bool isConverge = false, float accel = 0f, float maxSpeed = 0f,
                                       Material customMaterial = null, float customScale = 1.0f,
                                       bool isIndestructible = false,
                                       float angularVelocity = 0f, float maxRotationLimit = 0f)
    {
        if (data == null || data.bulletPrefab == null) return null;
        if (delay < 1.0f) { delay = 1.0f; }

        BulletData runtimeData = Instantiate(data);
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        if (myStatus != null && myStatus.characterData != null)
        {
            float atkMultiplier = 1.0f;
            switch (myStatus.characterData.rankAttack)
            {
                case StatusRank.E: atkMultiplier = 0.8f; break;
                case StatusRank.D: atkMultiplier = 0.9f; break;
                case StatusRank.C: atkMultiplier = 1.0f; break;
                case StatusRank.B: atkMultiplier = 1.1f; break;
                case StatusRank.A: atkMultiplier = 1.2f; break;
                case StatusRank.EX: atkMultiplier = 1.3f; break;
            }
            if (myStatus.IsAttackBoostActive) atkMultiplier *= 1.3f;
            atkMultiplier *= myStatus.GetJealousyMultiplier();
            runtimeData.damage = Mathf.RoundToInt(runtimeData.damage * atkMultiplier);
        }

        Quaternion initialRotation = Quaternion.Euler(0f, 0f, angle - 90f);

        GameObject obj = null;
        if (BulletPool.Instance != null)
        {
            obj = BulletPool.Instance.Get(data.bulletPrefab, pos, initialRotation);
        }
        else
        {
            obj = Instantiate(data.bulletPrefab, pos, initialRotation);
        }

        if (obj == null) return null;

        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
        if (bullet != null)
        {
            float finalMaxSpeed = (maxSpeed == 0f) ? speed : maxSpeed;
            bullet.Initialize(
                shooter: _rootOwner,
                target: targetTag,
                speed: speed,
                angle: angle,
                accel: accel,
                maxSpeed: finalMaxSpeed,
                angVel: angularVelocity,
                delay: delay,
                data: runtimeData,
                converge: isConverge
            );
            bullet.isIndestructible = isIndestructible;
        }

        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        obj.tag = assignedTag;

        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        obj.layer = assignedLayer;
        SetLayerRecursive(obj, assignedLayer);

        float finalBulletScale = runtimeData.bulletScale * customScale;
        obj.transform.localScale = new Vector3(finalBulletScale, finalBulletScale, 1.0f);

        if (customMaterial != null)
        {
            SpriteRenderer mainSR = obj.GetComponentInChildren<SpriteRenderer>();
            if (mainSR != null) mainSR.material = customMaterial;
        }

        obj.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        return bullet;
    }

    protected EnemyLaserBeam CreateLaserShot(BulletData data, Vector3 pos, float speed, int count, float wideAngle, int warningFrame, bool isSetupB = false)
    {
        if (BulletManager.Instance == null || data == null || data.bulletPrefab == null) return null;

        BulletData runtimeData = Instantiate(data);
        BulletManager.LaserColor color = runtimeData.laserColor;
        var laserSet = BulletManager.Instance.GetLaserSet(color);

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        int finalLaserDamage = runtimeData.damage;
        if (myStatus != null && myStatus.characterData != null)
        {
            float atkMultiplier = 1.0f;
            switch (myStatus.characterData.rankAttack)
            {
                case StatusRank.E: atkMultiplier = 0.8f; break;
                case StatusRank.D: atkMultiplier = 0.9f; break;
                case StatusRank.C: atkMultiplier = 1.0f; break;
                case StatusRank.B: atkMultiplier = 1.1f; break;
                case StatusRank.A: atkMultiplier = 1.2f; break;
                case StatusRank.EX: atkMultiplier = 1.3f; break;
            }

            if (myStatus.IsAttackBoostActive) atkMultiplier *= 1.3f;
            atkMultiplier *= myStatus.GetJealousyMultiplier();
            finalLaserDamage = Mathf.RoundToInt(finalLaserDamage * atkMultiplier);
        }

        GameObject laserObj = Instantiate(BulletManager.Instance.laserBeamPrefab, pos, Quaternion.identity);

        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        laserObj.tag = assignedTag;

        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        laserObj.layer = assignedLayer;
        SetLayerRecursive(laserObj, assignedLayer);

        EnemyLaserBeam laser = laserObj.GetComponent<EnemyLaserBeam>();
        if (laser != null)
        {
            if (isSetupB)
            {
                laser.SetupB(_rootOwner, targetTag, finalLaserDamage, pos.x, pos.y, count, wideAngle, color, warningFrame,
                             BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite, runtimeData);
            }
            else
            {
                laser.SetupA(_rootOwner, targetTag, finalLaserDamage, pos.x, pos.y, count, wideAngle, color, warningFrame,
                             BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite, runtimeData);
            }
        }

        return laser;
    }

    private void RestoreSpeedSafety(PlayerMove myMove)
    {
        if (myMove == null) return;
        if (_isEXSkillActive) return;
        myMove.skillSpeedMultiplier = 1.0f;
    }

    private IEnumerator TemporarySlow(float multiplier, float duration)
    {
        PlayerMove myMove = (_rootOwner != null) ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();
        if (myMove != null) myMove.skillSpeedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        RestoreSpeedSafety(myMove);
    }

    protected void PlaySkillSE(String path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
            SEManager.Instance.Play(clip, 0.4f);
    }
}