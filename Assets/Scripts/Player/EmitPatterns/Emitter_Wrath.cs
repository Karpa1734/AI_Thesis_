using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Emitter_Wrath : PlayerDanmakuEmitter
{
    // 👑 傲慢のステート管理（デフォルトはディフェンスモードから開始）
    private bool IsAttackmode = false;
    protected bool _isXLineReversed;
    // ⚔️ カリンのZスキル＝「しの字」アーク一閃！
    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        yield return null;
    }

    // ⚔️ カリンのXスキル＝空間一閃・双極ブレード！
    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        yield return null;
    }

    // ⚔️ カリンのCスキル＝ブーメラン設置！
    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteIcicleRay(s));
    }

    // ⚔️ カリンのVスキル＝防御フィールドチャージ！
    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ChargeAndExecuteDefensiveField(s));
    }

    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        yield return null;
    }




    // 🌟 同時展開しているアクティブなレーザーのセットを追跡するリスト
    private List<List<EnemyLaserBeam>> _activeIcicleLaserSets = new List<List<EnemyLaserBeam>>();
    private const int MAX_ICICLE_SETS = 3; // 最大3セットまで同時展開可能

    // 🌟【新規追加】：アイスレーザーの使用回数をカウントする変数
    private int _icicleUseCount = 0;

    // 💡 外部から上限に達しているか安全に確認するためのヘルパー
    public bool HasReachedMaxIcicleLasers()
    {
        if (_activeIcicleLaserSets != null)
        {
            _activeIcicleLaserSets.RemoveAll(set => set == null || set.TrueForAll(l => l == null));
            return _activeIcicleLaserSets.Count >= MAX_ICICLE_SETS;
        }
        return false;
    }

    /// <summary>
    /// 🧊 予告線を敵機方向に向かわせ、指定フレーム後に実線化して発射するアイスレーザー（3回ごとにクールタイム発動・移動デバフ連動版）
    /// </summary>
    private IEnumerator ExecuteIcicleRay(PlayerSkillData.SkillSettings s)
    {
        if (BulletManager.Instance == null) yield break;

        // 🛡️ 最大数チェック
        if (_activeIcicleLaserSets != null)
        {
            _activeIcicleLaserSets.RemoveAll(set => set == null || set.TrueForAll(l => l == null));
            if (_activeIcicleLaserSets.Count >= MAX_ICICLE_SETS)
            {
                yield break;
            }
        }

        PlaySkillSE(s.sePath);

        int warningFrame = 30; // 予告フレーム (0.5秒)
        float targetAngle = GetAngleToTarget(transform.position) + s.angleOffset;

        List<EnemyLaserBeam> currentSetLasers = new List<EnemyLaserBeam>();

        EnemyLaserBeam laser = CreateLaserShot(
            s.bulletData,
            transform.position,
            s.speed,
            s.count,
            s.wideAngle,
            warningFrame,
            isSetupB: true
        );

        if (laser != null)
        {
            currentSetLasers.Add(laser);

            // 予告線が敵機方向を向くようにデータを登録
            laser.AddData(new EnemyLaserBeam.LaserTransformData
            {
                frame = 0,
                dist = 0f,
                distAngle = 0f,
                laserAngle = targetAngle,
                distAngleVel = 0f,
                laserAngleVel = 0f,
                isSmooth = true
            });

            // 予告時間後の実線化ロックデータ
            laser.AddData(new EnemyLaserBeam.LaserTransformData
            {
                frame = warningFrame,
                laserAngleVel = 0f,
                isSmooth = true
            });

            laser.Fire();
        }

        if (currentSetLasers.Count > 0)
        {
            _activeIcicleLaserSets.Add(currentSetLasers);
            // 💡 s.moveSpeedMultiplier を用いた移動デバフの適用をライフタイム管理に連動させる
            float totalLifeTime = (warningFrame / 60f) + 1.0f;
            StartCoroutine(ManageIcicleSetLifetime(currentSetLasers, totalLifeTime, s.moveSpeedMultiplier));
        }

        // 🌟【3回使用ごとのクールタイム管理】
        _icicleUseCount++;
        if (_icicleUseCount >= 3)
        {
            _icicleUseCount = 0; // 3回に達したのでカウンターをリセット

            // 💡 3回目の発射時のみ、クールタイム分だけしっかりとウェイトを挟む
            float coolTime = s.cooldown > 0f ? s.cooldown : 1.0f;
            yield return new WaitForSeconds(coolTime);
        }
        else
        {
            // 1回目和2回目は即座に完了扱いとし、スイスイ連射できるようにする
            yield break;
        }
    }

    /// <summary>
    /// 生成されたレーザーセットのライフタイムと所有者の移動速度デバフを監視・管理する
    /// </summary>
    private IEnumerator ManageIcicleSetLifetime(List<EnemyLaserBeam> laserSet, float duration, float speedMultiplier)
    {
        _activeSkillCoroutines++;

        // 🌟【移動デバフの適用】：レーザーが場に存在する間、所有者の移動速度倍率を制限する
        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();
        if (myMove != null && !_isEXSkillActive)
        {
            // 複数のレーザーが同時に出ていても破綻しないよう、最小値（より遅い方）または設定値を適用
            myMove.skillSpeedMultiplier = (speedMultiplier > 0f) ? Mathf.Min(myMove.skillSpeedMultiplier, speedMultiplier) : 0.8f;
        }

        yield return new WaitForSeconds(duration);

        foreach (var laser in laserSet)
        {
            if (laser != null)
            {
                laser.ForceClose();
            }
        }

        if (_activeIcicleLaserSets != null)
        {
            _activeIcicleLaserSets.Remove(laserSet);
        }

        // 🌟【移動速度の復元】：現在他にもアクティブなアイスレーザーセットが残っていなければ速度を1.0fに戻す
        if (myMove != null && !_isEXSkillActive)
        {
            if (_activeIcicleLaserSets == null || _activeIcicleLaserSets.Count == 0)
            {
                myMove.skillSpeedMultiplier = 1.0f;
            }
        }

        if (_activeSkillCoroutines > 0)
        {
            _activeSkillCoroutines--;
        }
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
        float chargeTime = 0.3f; //
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

}
