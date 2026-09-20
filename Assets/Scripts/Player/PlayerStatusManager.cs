// --- PlayerStatusManager.cs 【VJTタイムベース・UIブロック・エラー完全解消版・バトルメトリクス対応】 ---
using DG.Tweening;
using KanKikuchi.AudioManager;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStatusManager : MonoBehaviour
{
    [Header("Player Settings")]
    public int playerId = 1;
    public PlayerSkillData characterData;
    [Header("📚 キャラクターアセットデータベース")]
    public PlayerSkillData[] allCharacterDataDatabase;
    [Tooltip("User1 または User2 と表記されているプレイヤーネームUIをここに登録してください")]
    public TextMeshProUGUI playerNameText;
    [Header("Resources")]
    public int life = 0;
    public float currentHP = 100f;
    public float maxHP = 100f;
    public int stockLives = 2;

    [Header("Piece Settings")]
    public int lifePieces = 0;
    public int lifePiecesRequired = 3;

    [Header("Timers")]
    public float invincibleTimer = 0f;
    public float deathBombTimer = 0f;

    [Header("Statistics")]
    public int continueCount = 0;
    public TextMeshProUGUI countdownText;

    [Header("UI References")]
    public PlayerStatusUI lifeUI;
    public ExtendNotificationUI extendUI;

    [Header("Round Transition")]
    public CanvasGroup screenFader;

    [Header("Global References")]
    public PauseManager pauseManager;
    [Header("VJT Visual Effects")]
    public SpellBarrierEffect spellBarrier;
    [Tooltip("新設した PlayerSpellRing_Line の【プレハブ】をここに登録してください")]
    public GameObject spellRingPrefab;
    private GameObject spawnedRingInstance;
    [Tooltip("新設した PlayerSpellCircle の【プレハブ】をここに登録してください")]
    public GameObject spellCirclePrefab;
    private GameObject spawnedCircleInstance;
    private PlayerMove _playerMove;

    [Header("--- VJT Overheat Settings ---")]
    [Tooltip("このキャラクターがVJTを解除・破砕された後の【術式焼き切れ（冷却期間）】の持続時間（秒）")]
    public float characterOverheatDuration = 20f;
    private bool _wasVJTReadyLastFrame = false;
    private bool _wasCounterReadyLastFrame = false;
    public bool IsInvincible => invincibleTimer > 0;
    public bool IsDeathBombWindow => deathBombTimer > 0;

    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI winText;
    public TextMeshProUGUI koText;
    public UnityEngine.UI.Slider hpBar;
    public UnityEngine.UI.Slider orangeBar;
    public UnityEngine.UI.Slider spellHpBar;
    public float lerpSpeed = 2.0f;
    private float _hpDrainAccumulator = 0f;
    private float _actionTaxAccumulator = 0f;

    [Header("--- Spell Card (VJT) 上乗せライフシステム ---")]
    public bool isSpellCardActive = false;
    public bool isOverheated = false;

    public float spellHP = 0f;
    public float spellMaxHP = 0f;

    [HideInInspector] public float preSpellHP;

    [Header("--- VJT Duration Settings (Seconds) ---")]
    public float minSpellDuration = 8.0f;
    public float maxSpellDuration = 15.0f;

    public float totalSpellDuration = 0f;
    public float spellTimer = 0f;
    public float initialUltimateEnergy = 0f;

    public float overheatDuration = 5f;
    [NonSerialized] public float overheatTimer = 0f;
    public TextMeshProUGUI hpNumericText;

    private float appearanceElapsed = 0f;
    private float animatedSpellHP = 0f;
    private bool isAnimatingSpellBar = false;
    private const float SPELL_BAR_ANIM_DURATION = 0.4f;

    [Header("🎯 Hitbox & Sprite Assignments (Inspector)")]
    [Tooltip("子オブジェクトにある当たり判定用コライダーをここにドラッグ＆ドロップしてください")]
    public Collider2D playerCollider;

    [Tooltip("当たり判定を視覚化している1つ目のスプライト（例：コア画像など）")]
    public SpriteRenderer hitboxSprite1;

    [Tooltip("当たり判定を視覚化している2つ目のスプライト（例：外枠・オーラ画像など）")]
    public SpriteRenderer hitboxSprite2;

    [HideInInspector] public Vector3 originalColliderScale;
    private float originalColliderRadius = 0.2f;

    private Vector3 originalSprite1Scale = Vector3.one;
    private Vector3 originalSprite2Scale = Vector3.one;

    public static bool isAnyVJTActive = false;

    private static int lastRequestFrame = -1;
    private static PlayerStatusManager p1Requester = null;
    private static PlayerStatusManager p2Requester = null;

    [Header("👁️ Jealousy Field Settings (Internal)")]
    [Tooltip("JealousyFogEffectスクリプトと画像がセットされた【黒い霧のプレハブ】をここに登録してください")]
    public GameObject jealousyFogPrefab;
    private float _fogSpawnTimer = 0f;

    private float _passiveAtkBoostTimer = 0f;
    public bool IsAttackBoostActive => _passiveAtkBoostTimer > 0f;
    private SpriteRenderer _myOwnCharacterRenderer;

    [Header("🔧 Debug UI Slots")]
    [Tooltip("HP/MP/アルカナの生数値を小数点第一位まで表示するデバッグ用Textアタッチ枠")]
    public TextMeshProUGUI debugStatusText;
    [Header("--- VJT Counter Timing ---")]
    [HideInInspector] public float timeSinceVJTActivated = 0f;
    private StatusRank _originalCharacterRank;
    private bool _hasCachedRank = false;

    private float _failedSpellSoundTimer = 0f;

    [Header("⏳ ロード画面・プログレスバー設定")]
    [Tooltip("ロード中に表示する専用のCanvasやPanel（非同期ロード中のみActiveにする）")]
    public GameObject loadingScreenCanvas;
    [Tooltip("進捗状況を表示するUI Slider（値の範囲は 0.0 ～ 1.0）")]
    public UnityEngine.UI.Slider progressBarSlider;
    [Tooltip("進捗率をパーセンテージ（例: 50%）で表示するテキストUI（任意）")]
    public TextMeshProUGUI progressText;

    private struct CharacterRankBackup
    {
        public StatusRank hp;
        public StatusRank mp;
        public StatusRank attack;
        public StatusRank agility;
        public StatusRank mmpRegen;
        public StatusRank spellZone;
    }
    private CharacterRankBackup _originalBackup;
    public static bool FromCharacterSelect = false;

    void Awake()
    {
        _playerMove = GetComponent<PlayerMove>();

        int targetSelectedId = (playerId == 1) ? GameSelectionData.SelectedCharacterP1 : GameSelectionData.SelectedCharacterP2;
        if (playerId == 2 && GameModeManager.IsStoryMode)
        {
            if (StoryModeManager.CurrentActiveRoute != null && StoryModeManager.CurrentActiveRoute.stages.Count > 0)
            {
                int currentStageIdx = StoryModeManager.CurrentStageNumber - 1;
                if (currentStageIdx >= 0 && currentStageIdx < StoryModeManager.CurrentActiveRoute.stages.Count)
                {
                    targetSelectedId = StoryModeManager.CurrentActiveRoute.stages[currentStageIdx].bossCharacterId;
                    Debug.Log($"<color=magenta>🎯 [PlayerStatusManager] StoryMode優先介入！ 2PボスIDを [{targetSelectedId}] に確定更新しました。</color>");
                }
            }
        }
        bool shouldLoadFromDatabase = FromCharacterSelect || GameModeManager.IsStoryMode;

        if (shouldLoadFromDatabase && allCharacterDataDatabase != null && targetSelectedId >= 0 && targetSelectedId < allCharacterDataDatabase.Length)
        {
            characterData = Instantiate(allCharacterDataDatabase[targetSelectedId]);
            Debug.Log($"<color=lime>✅ [PlayerStatusManager] Player {playerId} ➔ データベースから ID [{targetSelectedId}] ({characterData.characterName}) を正常ロードしました。</color>");
        }
        else if (characterData != null)
        {
            characterData = Instantiate(characterData);
        }

        if (BossPracticeManager.IsPracticeMode)
        {
            stockLives = 0; life = 0;
        }
        else if (GameModeManager.IsStoryMode)
        {
            if (playerId == 1)
            {
                life = 3;
                stockLives = 3;
            }
        }
        else
        {
            life = 0;
            stockLives = 0;
        }

        if (playerCollider == null)
        {
            playerCollider = GetComponentInChildren<Collider2D>();
        }

        if (playerCollider != null)
        {
            originalColliderScale = playerCollider.transform.localScale;

            if (playerCollider is CircleCollider2D circle)
            {
                originalColliderRadius = circle.radius;
            }
        }

        if (hitboxSprite1 != null) originalSprite1Scale = hitboxSprite1.transform.localScale;
        if (hitboxSprite2 != null) originalSprite2Scale = hitboxSprite2.transform.localScale;

        _myOwnCharacterRenderer = GetComponent<SpriteRenderer>();
        if (_myOwnCharacterRenderer == null) _myOwnCharacterRenderer = GetComponentInChildren<SpriteRenderer>();

        if (HasPassiveSkill(PassiveSkillType.LustSmall) && playerCollider != null)
        {
            CircleCollider2D startCircle = playerCollider as CircleCollider2D;
            if (startCircle != null)
            {
                startCircle.radius = originalColliderRadius * 0.8f;
                Debug.Log($"<color=lime>🛡️【パッシブ】SmallHitboxによりコライダー半径を常時0.8倍に縮小しました。</color>");
            }
        }

        ApplyCharacterRanks();

        if (characterData != null)
        {
            PlayerDanmakuEmitter[] allEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);
            foreach (var em in allEmitters) em.enabled = false;

            var wrath = GetComponentInChildren<Emitter_Wrath>(true);
            if (wrath != null) wrath.enabled = true;
        }
    }

    void Start()
    {
#if UNITY_EDITOR
        if (!FromCharacterSelect && characterData != null && allCharacterDataDatabase != null)
        {
            for (int i = 0; i < allCharacterDataDatabase.Length; i++)
            {
                if (allCharacterDataDatabase[i] != null && allCharacterDataDatabase[i].characterName == characterData.characterName)
                {
                    if (playerId == 1) GameSelectionData.SelectedCharacterP1 = i;
                    else if (playerId == 2) GameSelectionData.SelectedCharacterP2 = i;

                    characterData = Instantiate(allCharacterDataDatabase[i]);
                    ApplyCharacterRanks();
                    break;
                }
            }
            Debug.Log($"<color=yellow>🔧 [DEBUG MODE] シーン直接起動を検知。インスペクターのデバッグ用データ【{characterData.characterName}】を同期しました。</color>");
        }
#endif
        BGMManager.Instance.Play(BGMPath.BATTLE01, 1.0f, 1.0f);
        ApplyCharacterSettings();

        if (HasPassiveSkill(PassiveSkillType.PrideStatusSteal))
        {
            ExecutePrideStatusSteal();
        }

        DanmakuAgent trainingAgent = GetComponent<DanmakuAgent>();
        if (trainingAgent != null && Unity.MLAgents.Academy.Instance.IsCommunicatorOn)
        {
            maxHP *= 100f;
        }

        currentHP = maxHP;

        if (_playerMove != null)
        {
            _playerMove.currentEnergy = _playerMove.maxEnergy;
            Debug.Log($"<color=cyan>💧 初期マナを最大ランク基準に完全同期しました。currentEnergy={_playerMove.currentEnergy}, maxEnergy={_playerMove.maxEnergy}</color>");
        }

        isSpellCardActive = false;
        isOverheated = false;
        spellTimer = 0f;
        overheatTimer = 0f;
        invincibleTimer = 0f;

        PlayerDanmakuEmitter[] startupEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);
        foreach (var em in startupEmitters)
        {
            if (em != null)
            {
                System.Reflection.FieldInfo exField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (exField != null) exField.SetValue(em, false);
            }
        }

        PlayerAnimation startupAnim = GetComponentInChildren<PlayerAnimation>(true);
        if (startupAnim != null)
        {
            startupAnim.isInvincible = false;
        }
        if (GameModeManager.IsStoryMode && playerId == 2)
        {
            StoryBossPhaseManager bossManager = GetComponent<StoryBossPhaseManager>();
            if (bossManager == null) bossManager = gameObject.AddComponent<StoryBossPhaseManager>();
            bossManager.enabled = true;

            BossDanmakuExecutor bossExecutor = GetComponent<BossDanmakuExecutor>();
            if (bossExecutor == null) bossExecutor = gameObject.AddComponent<BossDanmakuExecutor>();
            bossExecutor.enabled = true;

            Debug.Log($"<color=magenta>👑【Story Mode】Player 2 ({characterData.characterName}) をステージボス＆弾幕モジュール化しました。</color>");
        }

        // 📊 戦闘開始時にメトリクス計測を開始（1P側のみ、または共通でキック）
        if (playerId == 1 && BattleMetricsManager.Instance != null)
        {
            BattleMetricsManager.Instance.StartTracking();
        }

        StartCoroutine(SetupInitialUI());
        StartCoroutine(InitUIWithDelay());
    }

    private void ApplyCharacterRanks()
    {
        if (characterData == null) return;

        switch (characterData.rankHP)
        {
            case StatusRank.E: maxHP = 70f; break;
            case StatusRank.D: maxHP = 85f; break;
            case StatusRank.C: maxHP = 100f; break;
            case StatusRank.B: maxHP = 115f; break;
            case StatusRank.A: maxHP = 130f; break;
            case StatusRank.EX: maxHP = 145f; break;
        }

        float convertedMaxEnergy = 100f;
        switch (characterData.rankMP)
        {
            case StatusRank.E: convertedMaxEnergy = 70f; break;
            case StatusRank.D: convertedMaxEnergy = 85f; break;
            case StatusRank.C: convertedMaxEnergy = 100f; break;
            case StatusRank.B: convertedMaxEnergy = 115f; break;
            case StatusRank.A: convertedMaxEnergy = 130f; break;
            case StatusRank.EX: convertedMaxEnergy = 145f; break;
        }
        if (_playerMove != null) _playerMove.maxEnergy = convertedMaxEnergy;

        if (_playerMove != null)
        {
            float calculatedAgilitySpeed = 5.0f;
            switch (characterData.rankAgility)
            {
                case StatusRank.E: calculatedAgilitySpeed = 3.8f; break;
                case StatusRank.D: calculatedAgilitySpeed = 4.4f; break;
                case StatusRank.C: calculatedAgilitySpeed = 5.0f; break;
                case StatusRank.B: calculatedAgilitySpeed = 5.6f; break;
                case StatusRank.A: calculatedAgilitySpeed = 6.2f; break;
                case StatusRank.EX: calculatedAgilitySpeed = 6.8f; break;
            }
            _playerMove.SetSpeedFromRank(calculatedAgilitySpeed);
        }

        if (_playerMove != null)
        {
            switch (characterData.rankMMPRegen)
            {
                case StatusRank.E: _playerMove.energyRegenRate = 50f; break;
                case StatusRank.D: _playerMove.energyRegenRate = 60f; break;
                case StatusRank.C: _playerMove.energyRegenRate = 70f; break;
                case StatusRank.B: _playerMove.energyRegenRate = 80f; break;
                case StatusRank.A: _playerMove.energyRegenRate = 90f; break;
                case StatusRank.EX: _playerMove.energyRegenRate = 100f; break;
            }
        }

        switch (characterData.rankSpellZone)
        {
            case StatusRank.E: maxSpellDuration = 20f; characterOverheatDuration = maxSpellDuration * 0.8f; break;
            case StatusRank.D: maxSpellDuration = 25f; characterOverheatDuration = maxSpellDuration * 0.8f; break;
            case StatusRank.C: maxSpellDuration = 30f; characterOverheatDuration = maxSpellDuration * 0.8f; break;
            case StatusRank.B: maxSpellDuration = 35f; characterOverheatDuration = maxSpellDuration * 0.8f; break;
            case StatusRank.A: maxSpellDuration = 40f; characterOverheatDuration = maxSpellDuration * 0.8f; break;
            case StatusRank.EX: maxSpellDuration = 45f; characterOverheatDuration = maxSpellDuration * 0.8f; break;
        }
        minSpellDuration = maxSpellDuration * 0.6f;
    }


    private IEnumerator InitUIWithDelay()
    {
        yield return null;
        UpdateUI();
    }

    private IEnumerator SetupInitialUI()
    {
        yield return null;
        currentHP = maxHP;
        UpdateUI();

        if (orangeBar != null)
        {
            orangeBar.maxValue = maxHP;
            orangeBar.value = currentHP;
        }
    }

    private void ApplyCharacterSettings()
    {
        if (characterData != null)
        {
            if (characterNameText != null)
            {
                characterNameText.text = characterData.characterName;
                characterNameText.color = characterData.imageColor;
            }
        }

        if (playerNameText != null)
        {
            DanmakuAgent agent = GetComponent<DanmakuAgent>();

            if (agent != null && agent._useAutoEvadeAI)
            {
                playerNameText.text = "COM";
            }
            else
            {
                playerNameText.text = (playerId == 1) ? "User1" : "User2";
            }
        }
    }

    void Update()
    {
        if (_passiveAtkBoostTimer > 0f)
        {
            _passiveAtkBoostTimer -= Time.deltaTime;
        }
        if (_failedSpellSoundTimer > 0f)
        {
            _failedSpellSoundTimer -= Time.deltaTime;
        }

        if (HasPassiveSkill(PassiveSkillType.GluttonyRegen) && currentHP > 0f)
        {
            float currentLimitHP = isSpellCardActive ? spellMaxHP : maxHP;
            float currentCheckHP = isSpellCardActive ? spellHP : currentHP;

            if (currentCheckHP < currentLimitHP)
            {
                float baseRegenAmount = maxHP * 0.005f * Time.deltaTime;

                if (isSpellCardActive)
                {
                    float areaRegenAmount = baseRegenAmount * 30f;
                    spellHP = Mathf.Min(spellHP + areaRegenAmount, spellMaxHP);
                }
                else
                {
                    float requiredEnergy = 0.5f * Time.deltaTime;

                    if (_playerMove != null && _playerMove.ultimateEnergy >= requiredEnergy)
                    {
                        _playerMove.ultimateEnergy -= requiredEnergy;
                        currentHP = Mathf.Min(currentHP + baseRegenAmount, maxHP);
                    }
                }
            }
        }

        PlayerDanmakuEmitter myEmitter = null;
        PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
        if (allEmitters == null || allEmitters.Length == 0) allEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);

        foreach (var em in allEmitters)
        {
            if (em != null && em.enabled)
            {
                myEmitter = em;
                break;
            }
        }
        if (myEmitter == null) myEmitter = GetComponentInChildren<PlayerDanmakuEmitter>();


        if (myEmitter != null && myEmitter.IsUltimateSkillActive)
        {
            invincibleTimer = 0.1f;
        }
        else if (invincibleTimer > 0)
        {
            invincibleTimer -= Time.deltaTime;
        }

        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;

        if (hpNumericText != null)
        {
            if (isSpellCardActive)
            {
                hpNumericText.text = $"{animatedSpellHP:F1} / {spellMaxHP:F1}";
                hpNumericText.color = new Color(1f, 0.85f, 0f);
            }
            else
            {
                hpNumericText.text = $"{currentHP:F1} / {maxHP:F1}";
                hpNumericText.color = Color.white;
            }
        }

        if (isSpellCardActive)
        {
            if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.StopTimer();
            timeSinceVJTActivated += Time.deltaTime;

            PlayerHitHandler hitHandler = GetComponentInChildren<PlayerHitHandler>();
            PlayerMove oppMove = _playerMove != null ? _playerMove.Opponent : null;
            PlayerHitHandler oppHitHandler = oppMove != null ? oppMove.GetComponentInChildren<PlayerHitHandler>() : null;

            bool isRoundEnded = (hitHandler != null && hitHandler.currentState == PlayerHitHandler.PlayerState.Down) ||
                                (oppHitHandler != null && oppHitHandler.currentState == PlayerHitHandler.PlayerState.Down);
            if (!isRoundEnded)
            {
                bool isULTActive = (myEmitter != null && myEmitter.IsUltimateSkillActive);

                spellTimer -= Time.deltaTime;

                bool hasUsedEXDuringSpell = false;
                PlayerDanmakuEmitter[] activeEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);
                foreach (var em in activeEmitters)
                {
                    if (em != null)
                    {
                        System.Reflection.FieldInfo exField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (exField != null && (bool)exField.GetValue(em)) { hasUsedEXDuringSpell = true; break; }
                    }
                }


                if (hasUsedEXDuringSpell || _playerMove.ultimateEnergy <= 0.1f)
                {
                    _playerMove.ultimateEnergy = 0f;
                }
                else if (!isULTActive)
                {
                    float timeRatio = Mathf.Clamp01(spellTimer / totalSpellDuration);
                    _playerMove.ultimateEnergy = initialUltimateEnergy * timeRatio;
                }

                ExecuteFieldEffectToOpponent();

                if (spellTimer <= 0f)
                {
                    spellTimer = 0f;
                    _playerMove.ultimateEnergy = 0f;
                    DeactivateSpellCard(false);
                }
            }

            if (isAnimatingSpellBar)
            {
                appearanceElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(appearanceElapsed / SPELL_BAR_ANIM_DURATION);
                float easedT = t * t * (3f - 2f * t);
                animatedSpellHP = Mathf.Lerp(0f, spellHP, easedT);

                if (t >= 1f) isAnimatingSpellBar = false;
            }
            else
            {
                animatedSpellHP = spellHP;
            }
        }
        else
        {
            timeSinceVJTActivated = 0f;
        }

        if (isOverheated)
        {
            overheatTimer -= Time.deltaTime;
            if (overheatTimer <= 0f)
            {
                isOverheated = false;
                Debug.Log("<color=green>⏳【VJT】術式冷却完了。通常状態へ復帰しました。</color>");
            }
        }

        float targetSliderValue = isSpellCardActive ? animatedSpellHP : currentHP;
        if (orangeBar != null && orangeBar.value > targetSliderValue)
        {
            orangeBar.value = Mathf.Lerp(orangeBar.value, targetSliderValue, Time.deltaTime * lerpSpeed);
            if (orangeBar.value - targetSliderValue < 0.1f) orangeBar.value = targetSliderValue;
        }

        if (_playerMove != null && !isSpellCardActive && PlayerMove.CanShoot)
        {
            bool isCounterCurrentlyReady = false;

            if (isAnyVJTActive && !isOverheated && _playerMove.ultimateEnergy >= 200f)
            {
                PlayerMove oppMove = _playerMove.Opponent;
                PlayerStatusManager oppStatus = oppMove != null ? oppMove.GetComponent<PlayerStatusManager>() : null;

                if (oppStatus != null && oppStatus.isSpellCardActive)
                {
                    float myProgress = Mathf.InverseLerp(200f, 300f, _playerMove.ultimateEnergy);
                    float myExpectedDuration = Mathf.Lerp(minSpellDuration, maxSpellDuration, myProgress);
                    float oppRemainingTime = oppStatus.spellTimer;

                    if (myExpectedDuration - oppRemainingTime > 10f)
                    {
                        isCounterCurrentlyReady = true;
                    }
                }
            }

            bool isVJTCurrentlyReady = _playerMove.ultimateEnergy >= 200f && !isOverheated && !isAnyVJTActive;

            if (isCounterCurrentlyReady && !_wasCounterReadyLastFrame)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.GETSPELLCARD, 1.0f);
                Debug.Log("<color=red>🔔【VJT UI】💥領域返し（カウンターVJT）が完全成立しました！チャンス音再生！</color>");
            }

            if (isVJTCurrentlyReady && !_wasVJTReadyLastFrame && !isCounterCurrentlyReady)
            {
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.GETSPELLCARD, 1.0f);
                Debug.Log("<color=cyan>🔔【VJT UI】🔮通常領域（VJT）が発動可能になりました！チャージ音再生！</color>");
            }

            _wasCounterReadyLastFrame = isCounterCurrentlyReady;
            _wasVJTReadyLastFrame = isVJTCurrentlyReady;
        }
        else
        {
            _wasCounterReadyLastFrame = false;
            _wasVJTReadyLastFrame = false;
        }

        UpdateUI();
    }

    public void ActivateSpellCard()
    {
        if (GameModeManager.IsStoryMode)
        {
            Debug.Log("<color=yellow>🛡️ [VJT BLOCKED] ストーリーモードのため領域展開は使用できません。</color>");
            return;
        }

        if (isSpellCardActive || isOverheated || _playerMove.ultimateEnergy < 200f)
        {
            string reason = "";
            if (isSpellCardActive) reason += "[すでに自身がVJT展開中] ";
            if (isOverheated) reason += $"[術式焼き切れ・冷却デバフ中 (残り {overheatTimer:F1}秒)] ";
            if (_playerMove.ultimateEnergy < 200f) reason += $"[アルカナゲージ不足 (必要:200% / 現在:{_playerMove.ultimateEnergy:F1}%)] ";

            Debug.LogError($"<color=red>❌ [VJT BLOCK] Player {playerId} の領域発動が手前で拒絶されました。 理由: {reason}</color>");
            return;
        }

        if (isAnyVJTActive && !isSpellCardActive)
        {
            PlayerMove oppMove = _playerMove != null ? _playerMove.Opponent : null;
            PlayerStatusManager oppStatus = oppMove != null ? oppMove.gameObject.GetComponent<PlayerStatusManager>() : null;

            if (oppStatus != null && oppStatus.isSpellCardActive)
            {
                if (oppStatus.timeSinceVJTActivated < 3.0f)
                {
                    Debug.Log($"<color=yellow>🛡️ [VJT COUNTER BLOCKED] 相手の領域展開からまだ {oppStatus.timeSinceVJTActivated:F1}秒 です (3.0秒必要)。</color>");
                    return;
                }

                float myProgress = Mathf.InverseLerp(200f, 300f, _playerMove.ultimateEnergy);
                float myExpectedDuration = Mathf.Lerp(minSpellDuration, maxSpellDuration, myProgress);
                float oppRemainingTime = oppStatus.spellTimer;
                float timeDifference = myExpectedDuration - oppRemainingTime;

                Debug.Log($"<color=yellow>⚔️ [VJT COUNTER CHECK] 領域返しジャッジ走査中... 時間差: {timeDifference:F2}秒 (必要: >10.0秒)</color>");

                if (timeDifference > 10f)
                {
                    Debug.Log($"<color=red>💥💥【領域返し(カウンターVJT)成立!!】時間差: {timeDifference:F2}秒</color>");
                    oppStatus.DeactivateSpellCard(false);
                    oppMove.ultimateEnergy *= 0.5f;
                    float counterVJTDuration = timeDifference;
                    ExecuteCounterActivationSequence(counterVJTDuration);
                    return;
                }
                else
                {
                    Debug.Log($"<color=yellow>🛡️ [VJT COUNTER FAILED] 領域返しの条件(持続アドバンテージ10秒以上)を満たしていないため、不発判定処理を行います。</color>");

                    if (_failedSpellSoundTimer <= 0f)
                    {
                        _failedSpellSoundTimer = 0.5f;
                    }
                    return;
                }
            }
        }

        if (SpellCardManager.Instance != null && !SpellCardManager.Instance.TryRequestVJT(this))
        {
            Debug.LogError($"<color=red>❌ [VJT BLOCK] SpellCardManager によって発動リクエストが拒否されました。世界ロックの状態に矛盾があります。</color>");
            return;
        }

        Debug.Log($"<color=green>💎 [VJT SUCCESS] 全てのチェックを通過！排他フレームジャッジ（同時押しチェックコルーチン）へ移行します。</color>");

        if (Time.frameCount != lastRequestFrame)
        {
            lastRequestFrame = Time.frameCount;
            p1Requester = (playerId == 1) ? this : null;
            p2Requester = (playerId == 2) ? this : null;
            StartCoroutine(ExecuteSpellCardWithFrameCheck());
        }
        else
        {
            if (playerId == 1) p1Requester = this;
            if (playerId == 2) p2Requester = this;
        }
    }

    private IEnumerator ExecuteSpellCardWithFrameCheck()
    {
        yield return null;

        PlayerStatusManager finalWinner = null;

        if (p1Requester != null && p2Requester != null)
        {
            bool isP1Winner = UnityEngine.Random.value < 0.5f;
            finalWinner = isP1Winner ? p1Requester : p2Requester;
            PlayerStatusManager finalLoser = isP1Winner ? p2Requester : p1Requester;

            Debug.Log($"<color=red>⚔️【VJT同時発動】完全同時押しジャッジ！ 勝者: [Player {finalWinner.playerId}]</color>");

            if (SpellCardManager.Instance != null)
            {
                SpellCardManager.Instance.ReleaseVJT(finalLoser);
            }

            finalWinner.ExecuteActivationSequence();
        }
        else
        {
            if (p1Requester != null) p1Requester.ExecuteActivationSequence();
            if (p2Requester != null) p2Requester.ExecuteActivationSequence();
        }

        p1Requester = null;
        p2Requester = null;
        lastRequestFrame = -1;
    }

    private void PlayVJTCutIn()
    {
        if (characterData == null || characterData.characterSprite == null) return;

        GameObject cutInObj = new GameObject("VJTCutInImage_" + playerId);

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            cutInObj.transform.SetParent(canvas.transform, false);
        }

        RectTransform cutInRect = cutInObj.AddComponent<RectTransform>();
        cutInRect.anchorMin = new Vector2(0.5f, 0.5f);
        cutInRect.anchorMax = new Vector2(0.5f, 0.5f);
        cutInRect.pivot = new Vector2(0.5f, 0.5f);

        Sprite sprite = characterData.characterSprite;
        float targetHeight = 1200f;
        float spriteWidth = sprite.rect.width;
        float spriteHeight = sprite.rect.height;

        if (spriteWidth > 0f && spriteHeight > 0f)
        {
            float aspectRatio = spriteWidth / spriteHeight;
            cutInRect.sizeDelta = new Vector2(targetHeight * aspectRatio, targetHeight);
        }
        else
        {
            cutInRect.sizeDelta = new Vector2(400f, targetHeight);
        }

        UnityEngine.UI.Image cutInImage = cutInObj.AddComponent<UnityEngine.UI.Image>();
        cutInImage.sprite = sprite;
        cutInImage.preserveAspect = true;

        float startX = (playerId == 1) ? -1500f : 1500f;
        float endX = 0f;
        float exitX = (playerId == 1) ? 1500f : -1500f;

        cutInRect.anchoredPosition = new Vector3(startX, 500f, 0f);
        Color c = cutInImage.color;
        c.a = 0f;
        cutInImage.color = c;

        cutInImage.DOFade(1f, 0.2f);

        Sequence seq = DOTween.Sequence();

        seq.Append(cutInRect.DOAnchorPos(new Vector2(endX, 0f), 1.1f).SetEase(Ease.OutCubic));
        seq.AppendInterval(0.4f);
        seq.Append(cutInRect.DOAnchorPos(new Vector2(exitX, -500f), 1.1f).SetEase(Ease.InCubic));
        seq.Join(cutInImage.DOFade(0f, 0.2f).SetDelay(0.7f));

        seq.OnComplete(() =>
        {
            if (cutInObj != null)
            {
                Destroy(cutInObj);
            }
        });
    }

    private void ExecuteActivationSequence()
    {
        ClearAllBulletsOnField();
        Debug.Log($"<color=cyan>🔥【聖少女領域 - VJT展開】現在のゲージ残量: {_playerMove.ultimateEnergy}%</color>");

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.CARDCALL, 1.0f);

        isSpellCardActive = true;
        isAnyVJTActive = true;
        isOverheated = false;

        PlayVJTCutIn();
        initialUltimateEnergy = _playerMove.ultimateEnergy;
        preSpellHP = currentHP;

        float fullArmorHP = maxHP * 30f;

        float progress = Mathf.InverseLerp(200f, 300f, initialUltimateEnergy);
        totalSpellDuration = Mathf.Lerp(minSpellDuration, maxSpellDuration, progress);
        spellTimer = totalSpellDuration;

        float spawnHPRatio = Mathf.Lerp(0.6f, 1.0f, progress);

        spellMaxHP = fullArmorHP;
        spellHP = fullArmorHP * spawnHPRatio;

        isAnimatingSpellBar = true;
        appearanceElapsed = 0f;
        animatedSpellHP = 0f;

        if (playerCollider != null)
        {
            playerCollider.transform.localScale = originalColliderScale * 30f;
        }

        if (spellBarrier != null)
        {
            Color charColor = (characterData != null) ? characterData.imageColor : Color.white;

            spellBarrier.SetBarrierActive(true);

            Renderer[] barrierRenderers = spellBarrier.GetComponentsInChildren<Renderer>(true);
            foreach (var r in barrierRenderers)
            {
                if (r is SpriteRenderer sr)
                {
                    sr.color = charColor;
                }
                else if (r is LineRenderer lr)
                {
                    lr.startColor = charColor;
                    lr.endColor = charColor;
                }
                else if (r.material != null)
                {
                    r.material.color = charColor;
                }
            }

            ParticleSystem[] barrierParticles = spellBarrier.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in barrierParticles)
            {
                var mainModule = ps.main;
                mainModule.startColor = charColor;
            }
        }

        UpdateUI();
        SyncBarsImmediately();

        if (spellRingPrefab != null && spawnedRingInstance == null)
        {
            spawnedRingInstance = Instantiate(spellRingPrefab, transform.position, Quaternion.identity);
            PlayerSpellRing_Line ringScript = spawnedRingInstance.GetComponent<PlayerSpellRing_Line>();
            if (ringScript != null)
            {
                ringScript.targetStatus = this;
                ringScript.Activate(totalSpellDuration);
            }
        }

        if (spellCirclePrefab != null && spawnedCircleInstance == null)
        {
            spawnedCircleInstance = Instantiate(spellCirclePrefab, transform.position, Quaternion.identity);
            PlayerSpellCircle circleScript = spawnedCircleInstance.GetComponent<PlayerSpellCircle>();
            if (circleScript != null)
            {
                circleScript.Activate(this, totalSpellDuration);
            }
        }

        if (EnemySpellCardUI.Instance != null && characterData != null)
        {
            string displayName = string.IsNullOrEmpty(characterData.spellCardName)
                ? characterData.characterName
                : characterData.spellCardName;

            EnemySpellCardUI.Instance.DisplaySpell(
                displayName,
                0,
                0,
                1000000f,
                false,
                this.playerId
            );
        }

        if (VJTSpellBackgroundManager2D.Instance != null)
        {
            VJTSpellBackgroundManager2D.Instance.SetSpellBackgroundActive(true, this.characterData);
        }
    }

    public void DeactivateSpellCard(bool isDefeatedByDamage)
    {
        if (!isSpellCardActive) return;

        bool isExSkillTriggered = false;
        PlayerDanmakuEmitter[] emitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);
        foreach (var em in emitters)
        {
            if (em != null)
            {
                System.Reflection.FieldInfo exField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (exField != null && (bool)exField.GetValue(em))
                {
                    isExSkillTriggered = true;
                    break;
                }
            }
        }

        float finalCarryOver = 0f;

        if (isExSkillTriggered)
        {
            if (_playerMove != null)
            {
                _playerMove.ultimateEnergy = 0f;
                Debug.Log("<color=orange>👑【ULTゲージ確定リセット】必殺技（EX）使用による領域解除のため、ULTゲージを完全に 0 に固定します。</color>");
            }
        }
        else if (_playerMove != null)
        {
            finalCarryOver = _playerMove.ultimateEnergy * 0.5f;
            Debug.Log($"<color=cyan>✨【ULTゲージ持ち越し準備】領域解除時の残量 {_playerMove.ultimateEnergy}% の半分である {finalCarryOver}% をキープします。</color>");
        }

        isSpellCardActive = false;
        isAnyVJTActive = false;

        if (isDefeatedByDamage)
        {
            SetInvincible(1.0f);
        }

        if (spellBarrier != null)
        {
            spellBarrier.SetBarrierActive(false);
        }

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SPELL_OFF, 0.5f);

        if (SpellCardManager.Instance != null)
        {
            SpellCardManager.Instance.ReleaseVJT(this);
        }

        if (playerCollider != null)
        {
            playerCollider.transform.localScale = originalColliderScale;

            if (playerCollider is CircleCollider2D circle)
            {
                float targetRadius = originalColliderRadius;
                if (HasPassiveSkill(PassiveSkillType.LustSmall))
                {
                    targetRadius *= 0.8f;
                }
                circle.radius = targetRadius;
            }
        }

        if (_playerMove != null && _playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null)
            {
                if (oppStatus.playerCollider is CircleCollider2D oppCircle)
                {
                    float oppTargetRadius = oppStatus.originalColliderRadius;
                    if (oppStatus.HasPassiveSkill(PassiveSkillType.LustSmall)) oppTargetRadius *= 0.8f;
                    oppCircle.radius = oppTargetRadius;
                }

                SpriteRenderer oppMainSR = _playerMove.Opponent.GetComponentInChildren<SpriteRenderer>();
                SpriteRenderer[] allOppSRs = _playerMove.Opponent.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in allOppSRs)
                {
                    if (sr == oppMainSR) continue;
                    if (sr.transform != _playerMove.Opponent.transform)
                    {
                        sr.transform.localScale = Vector3.one;
                    }
                }
            }
        }

        if (isDefeatedByDamage)
        {
            currentHP = preSpellHP;
        }
        else
        {
            float spellHpRatio = 0f;
            if (spellMaxHP > 0f)
            {
                spellHpRatio = Mathf.Clamp01(spellHP / spellMaxHP);
            }
            float targetHP = maxHP * spellHpRatio;
            currentHP = Mathf.Max(preSpellHP, targetHP);
            currentHP = Mathf.Min(currentHP, maxHP);
        }

        spellHP = 0f;
        spellMaxHP = 0f;
        spellTimer = 0f;
        totalSpellDuration = 0f;
        initialUltimateEnergy = 0f;

        if (_playerMove != null)
        {
            if (isExSkillTriggered)
            {
                _playerMove.ultimateEnergy = 0f;
            }
            else
            {
                _playerMove.ultimateEnergy = finalCarryOver;
                if (finalCarryOver > 0f)
                {
                    Debug.Log($"<color=lime>🔋【ULTキャリーオーバー適用】非EX解除のため、次へ持ち越すゲージ {finalCarryOver}% を正しく適用しました。</color>");
                }
            }
        }

        isOverheated = true;
        overheatTimer = (characterData != null) ? characterData.characterOverheatDuration : 20f;

        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.ResumeTimer();
        }

        UpdateUI();
        SyncBarsImmediately();

        if (spawnedRingInstance != null)
        {
            PlayerSpellRing_Line ringScript = spawnedRingInstance.GetComponent<PlayerSpellRing_Line>();
            if (ringScript != null) ringScript.Deactivate();
            Destroy(spawnedRingInstance);
            spawnedRingInstance = null;
        }

        if (spawnedCircleInstance != null)
        {
            PlayerSpellCircle circleScript = spawnedCircleInstance.GetComponent<PlayerSpellCircle>();
            if (circleScript != null) circleScript.Deactivate();
            if (spawnedCircleInstance != null)
            {
                Destroy(spawnedCircleInstance);
                spawnedCircleInstance = null;
            }
        }

        if (EnemySpellCardUI.Instance != null)
        {
            EnemySpellCardUI.Instance.HideSpell();
        }

        if (VJTSpellBackgroundManager2D.Instance != null)
        {
            VJTSpellBackgroundManager2D.Instance.SetSpellBackgroundActive(false);
        }
    }

    private void ExecuteCounterActivationSequence(float overrideDuration)
    {
        ClearAllBulletsOnField();

        Debug.Log($"<color=lime>👑【COUNTER VJT FLUSH】超過時間 {overrideDuration:F2} 秒で世界を再定義します！</color>");

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.CARDCALL, 1.2f);

        isSpellCardActive = true;
        isAnyVJTActive = true;
        isOverheated = false;

        PlayVJTCutIn();
        initialUltimateEnergy = _playerMove.ultimateEnergy;
        preSpellHP = currentHP;

        float fullArmorHP = maxHP * 30f;

        totalSpellDuration = overrideDuration;
        spellTimer = totalSpellDuration;

        float progress = Mathf.InverseLerp(200f, 300f, initialUltimateEnergy);
        float spawnHPRatio = Mathf.Lerp(0.6f, 1.0f, progress);
        spellMaxHP = fullArmorHP;
        spellHP = fullArmorHP * spawnHPRatio;

        isAnimatingSpellBar = true;
        appearanceElapsed = 0f;
        animatedSpellHP = 0f;

        if (playerCollider != null)
        {
            playerCollider.transform.localScale = originalColliderScale * 30f;
        }

        if (spellBarrier != null)
        {
            Color charColor = (characterData != null) ? characterData.imageColor : Color.white;
            spellBarrier.SetBarrierActive(true);
            Renderer[] barrierRenderers = spellBarrier.GetComponentsInChildren<Renderer>(true);
            foreach (var r in barrierRenderers)
            {
                if (r is SpriteRenderer sr) sr.color = charColor;
                else if (r is LineRenderer lr) { lr.startColor = charColor; lr.endColor = charColor; }

                else if (r.material != null) r.material.color = charColor;
            }
        }

        UpdateUI();
        SyncBarsImmediately();

        if (spellRingPrefab != null && spawnedRingInstance == null)
        {
            spawnedRingInstance = Instantiate(spellRingPrefab, transform.position, Quaternion.identity);
            PlayerSpellRing_Line ringScript = spawnedRingInstance.GetComponent<PlayerSpellRing_Line>();
            if (ringScript != null) { ringScript.targetStatus = this; ringScript.Activate(totalSpellDuration); }

        }
        if (spellCirclePrefab != null && spawnedCircleInstance == null)
        {
            spawnedCircleInstance = Instantiate(spellCirclePrefab, transform.position, Quaternion.identity);
            PlayerSpellCircle circleScript = spawnedCircleInstance.GetComponent<PlayerSpellCircle>();
            if (circleScript != null) circleScript.Activate(this, totalSpellDuration);
        }
        if (EnemySpellCardUI.Instance != null && characterData != null)
        {
            string displayName = string.IsNullOrEmpty(characterData.spellCardName) ? characterData.characterName : characterData.spellCardName;
            EnemySpellCardUI.Instance.DisplaySpell(displayName, 0, 0, 1000000f, false, this.playerId);
        }
        if (VJTSpellBackgroundManager2D.Instance != null)
        {
            VJTSpellBackgroundManager2D.Instance.SetSpellBackgroundActive(true, this.characterData);
        }
    }

    public bool ApplyDamage(int amount)
    {
        if (HasPassiveSkill(PassiveSkillType.WrathCounter))
        {
            _passiveAtkBoostTimer = 8.0f;
            Debug.Log($"<color=orange>⚔️【パッシブ発動】被弾をトリガーに8秒間、攻撃力1.3倍バフが起動しました！</color>");
        }

        // =========================================================================
        // 🎯【バトルメトリクス連携】：ダメージイベントの計測マネージャーへの通知
        // 💡 playerId == 1 なら 1P（自機）が被弾、playerId == 2 なら 2P（敵機）が被弾
        // =========================================================================
        if (BattleMetricsManager.Instance != null)
        {
            bool isPlayerHit = (playerId == 1);
            BattleMetricsManager.Instance.RecordDamageEvent(isPlayerHit, amount);
        }
        // =========================================================================

        if (isSpellCardActive)
        {
            spellHP -= amount;
            UpdateUI();

            if (spellHP <= 0)
            {
                spellHP = 0;

                bool isStoryBossSpell = GameModeManager.IsStoryMode && playerId == 2;

                DeactivateSpellCard(true);

                if (isStoryBossSpell)
                {
                    return true;
                }

                return false;
            }
            return false;
        }

        currentHP -= amount;
        UpdateUI();

        if (currentHP <= 0)
        {
            currentHP = 0;

            // 📊 決着（死亡）がついた瞬間にメトリクスのトラッキングを終了して出力
            if (BattleMetricsManager.Instance != null)
            {
                BattleMetricsManager.Instance.EndTrackingAndExport();
            }

            return true;
        }
        return false;
    }

    public bool HasPassiveSkill(PassiveSkillType type)
    {
        if (characterData == null || characterData.passiveSkills == null) return false;

        if (_playerMove != null && _playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null && oppStatus.isSpellCardActive && oppStatus.characterData != null)
            {
                if (oppStatus.characterData.vjtEffectType == VJTEffectType.NihilityField)
                {
                    if (type != PassiveSkillType.NihilityFieldCancel)
                    {
                        if (!HasPassiveSkill(PassiveSkillType.NihilityFieldCancel))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        foreach (var slot in characterData.passiveSkills)
        {
            if (slot.skillType == type) return true;
        }
        return false;
    }

    public bool SubtractLifeAndCheckRebirth()
    {
        if (GameModeManager.IsStoryMode)
        {
            if (stockLives > 0)
            {
                stockLives--;
                life = stockLives;
                UpdateUI();
                return false;
            }
            return true;
        }
        else
        {
            if (_playerMove != null && _playerMove.Opponent != null)
            {
                PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
                if (oppStatus != null)
                {
                    if (GameDifficultyManager.IsEndlessMode)
                    {
                        Debug.Log("<color=orange>🔄【Endless Mode】エンドレスモード稼働中。勝ち星を加算せず、次のラウンドへ進みます。</color>");

                        oppStatus.UpdateUI();
                        UpdateUI();

                        return false;
                    }

                    if (oppStatus.life >= 1)
                    {
                        oppStatus.life = 2;
                        oppStatus.UpdateUI();
                        UpdateUI();
                        return true;
                    }
                    else
                    {
                        oppStatus.life = 1;
                        oppStatus.UpdateUI();
                        UpdateUI();
                        return false;
                    }
                }
            }
            UpdateUI();
            return false;
        }
    }

    public IEnumerator GradualHealthRecovery(float duration)
    {
        float startHP = currentHP;
        float elapsed = 0;

        if (orangeBar != null)
        {
            orangeBar.maxValue = maxHP;
            orangeBar.value = startHP;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentHP = Mathf.Lerp(startHP, maxHP, elapsed / duration);
            UpdateUI();
            yield return null;
        }
        currentHP = maxHP;
        UpdateUI();

        if (orangeBar != null) orangeBar.value = maxHP;
    }

    public IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (screenFader == null) yield break;
        float startAlpha = screenFader.alpha;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            screenFader.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        screenFader.alpha = targetAlpha;
    }

    public void UpdateUI()
    {
        if (winText != null && !winText.gameObject.activeSelf) winText.gameObject.SetActive(false);
        if (koText != null && !koText.gameObject.activeSelf) koText.gameObject.SetActive(false);

        bool isVs = !GameModeManager.IsStoryMode;

        if (lifeUI != null)
        {
            lifeUI.SetCountVsVariant(life, lifePieces, lifePiecesRequired, isVs);
        }

        if (isSpellCardActive)
        {
            if (spellHpBar != null)
            {
                spellHpBar.gameObject.SetActive(true);
                spellHpBar.maxValue = spellMaxHP;
                spellHpBar.value = animatedSpellHP;
            }

            if (hpBar != null)
            {
                hpBar.gameObject.SetActive(true);
                hpBar.maxValue = maxHP;
                hpBar.value = preSpellHP;
                SetSliderAlpha(hpBar, 0.3f);
            }
        }
        else
        {
            if (hpBar != null)
            {
                hpBar.gameObject.SetActive(true);
                hpBar.maxValue = maxHP;
                hpBar.value = currentHP;
                SetSliderAlpha(hpBar, 1.0f);
            }

            if (spellHpBar != null)
            {
                spellHpBar.gameObject.SetActive(false);
            }
        }

        if (orangeBar != null)
        {
            orangeBar.maxValue = isSpellCardActive ? spellMaxHP : maxHP;
            float currentTargetValue = isSpellCardActive ? animatedSpellHP : currentHP;
            if (currentTargetValue >= (isSpellCardActive ? spellMaxHP : maxHP))
            {
                orangeBar.value = isSpellCardActive ? spellMaxHP : maxHP;
            }
        }
        if (debugStatusText != null)
        {
            float displayCurrentHP = isSpellCardActive ? spellHP : currentHP;
            float displayMaxHP = isSpellCardActive ? spellMaxHP : maxHP;
            string hpLabel = isSpellCardActive ? "<color=gold>SP-HP</color>" : "HP";

            float displayCurrentMP = (_playerMove != null) ? _playerMove.currentEnergy : 0f;
            float displayMaxMP = (_playerMove != null) ? _playerMove.maxEnergy : 100f;
            float arcanaPercentage = (_playerMove != null) ? _playerMove.ultimateEnergy : 0f;

            string rHP = characterData != null ? characterData.rankHP.ToString() : "C";
            string rMP = characterData != null ? characterData.rankMP.ToString() : "C";
            string rAtk = characterData != null ? characterData.rankAttack.ToString() : "C";
            string rAgi = characterData != null ? characterData.rankAgility.ToString() : "C";
            string rReg = characterData != null ? characterData.rankMMPRegen.ToString() : "C";
            string rSpl = characterData != null ? characterData.rankSpellZone.ToString() : "C";

            float atkMult = 1.0f;
            if (characterData != null)
            {
                switch (characterData.rankAttack)
                {
                    case StatusRank.E: atkMult = 0.8f; break;
                    case StatusRank.D: atkMult = 0.9f; break;
                    case StatusRank.C: atkMult = 1.0f; break;
                    case StatusRank.B: atkMult = 1.1f; break;
                    case StatusRank.A: atkMult = 1.2f; break;
                    case StatusRank.EX: atkMult = 1.3f; break;
                }
            }

            float speedAgi = (_playerMove != null) ? _playerMove.normalSpeed : 5.0f;
            float speedFoc = (_playerMove != null) ? _playerMove.focusSpeed : 2.0f;

            float regenRate = (_playerMove != null) ? _playerMove.energyRegenRate : 15f;

            string passiveStatusStr = "<color=gray>Inactive</color>";
            float finalAtkMult = atkMult;

            if (IsAttackBoostActive)
            {
                finalAtkMult = atkMult * 1.3f;
                passiveStatusStr = $"<color=gold>ACTIVE ({_passiveAtkBoostTimer:F1}s)</color>";
            }

            float jealousyMult = GetJealousyMultiplier();
            finalAtkMult *= jealousyMult;

            string gluttonyStatusStr = "<color=gray>OFF</color>";
            if (HasPassiveSkill(PassiveSkillType.GluttonyRegen))
            {
                float currentLimitHP = isSpellCardActive ? spellMaxHP : maxHP;
                float currentCheckHP = isSpellCardActive ? spellHP : currentHP;

                if (currentCheckHP >= currentLimitHP)
                {
                    gluttonyStatusStr = "<color=green>FULL (Idle)</color>";
                }
                else if (isSpellCardActive)
                {
                    gluttonyStatusStr = "<color=cyan>VJT FREE REGEN (+1%/s)</color>";
                }
                else
                {
                    float requiredEnergy = 1.0f * Time.deltaTime;
                    if (_playerMove != null && _playerMove.ultimateEnergy >= requiredEnergy)
                        gluttonyStatusStr = "<color=lime>CONSUMING REGEN (+1%/s)</color>";
                    else
                        gluttonyStatusStr = "<color=red>NO ENERGY (Paused)</color>";
                }
            }

            string prideStatusStr = "<color=gray>OFF</color>";
            if (HasPassiveSkill(PassiveSkillType.PrideStatusSteal))
            {
                prideStatusStr = "<color=gold>ACTIVE (Transcendence)</color>";
            }

            string slothStatusStr = "<color=gray>OFF</color>";
            if (HasPassiveSkill(PassiveSkillType.SlothStandStillBoost))
            {
                slothStatusStr = IsSlothBoostActive() ? "<color=lime>ACTIVE (x1.5)</color>" : "<color=yellow>MOVING (Idle)</color>";
            }

            string nihilityStatusStr = "<color=gray>OFF</color>";
            if (HasPassiveSkill(PassiveSkillType.NihilityFieldCancel))
            {
                bool isOpponentVJTActive = (_playerMove != null && _playerMove.Opponent != null &&
                                            _playerMove.Opponent.GetComponent<PlayerStatusManager>() != null &&
                                            _playerMove.Opponent.GetComponent<PlayerStatusManager>().isSpellCardActive);

                nihilityStatusStr = isOpponentVJTActive ? "<color=cyan>ABSORB (Blocking!)</color>" : "<color=lime>ON (Ready)</color>";
            }

            float currentRadius = 0f;
            if (playerCollider is CircleCollider2D circle)
            {
                currentRadius = circle.radius;
            }

            string debugInfo =
                            $"<b>== REALTIME RESOURCE ==</b>\n" +
                            $"{hpLabel}: {displayCurrentHP:F1} / {displayMaxHP:F1}\n" +
                            $"MP: {displayCurrentMP:F1} / {displayMaxMP:F1}\n" +
                            $"ARCANA: {arcanaPercentage:F1}%\n\n" +
                            $"<b>==🧬 PASSIVE SKILL STATUS ==</b>\n" +
                            $"AtkBoostOnHit: {passiveStatusStr}\n" +
                            $"SmallHitbox(0.8x): {(HasPassiveSkill(PassiveSkillType.LustSmall) ? "<color=lime>ON</color>" : "<color=gray>OFF</color>")}\n" +
                            $"JealousyBoost(Max1.5x): {(HasPassiveSkill(PassiveSkillType.JealousyAtkBoost) ? $"<color=orange>x{jealousyMult:F2}</color>" : "<color=gray>OFF</color>")}\n" +
                            $"GluttonyRegen(1%/s): {gluttonyStatusStr}\n" +
                            $"SlothBoost(1.3x): {slothStatusStr}\n" +
                            $"PrideSteal: {prideStatusStr}\n" +
                            $"NihilityCancel: {nihilityStatusStr}\n" +
                            $"[判定半径] Hitbox Radius: <color=cyan>{currentRadius:F3}</color> (Base: {originalColliderRadius:F2})\n\n" +
                            $"<b>== 6-STATUS RANKS & VALUE ==</b>\n" +
                            $"[体力] HP_Max: {maxHP:F1} ({rHP})\n" +
                $"[魔力] MP_Max: {displayMaxMP:F1} ({rMP})\n" +
                $"[攻撃] ATK_Mult: x{finalAtkMult:F2} (Base: x{atkMult:F1}) ({rAtk})\n" +
                $"[敏捷] SPD_High: {speedAgi:F1} / Low: {speedFoc:F1} ({rAgi})\n" +
                $"[再生] MP_Regen: {regenRate:F1}/s ({rReg})\n" +
                $"[領域] VJT_Time: {maxSpellDuration:F1}s / Min: {minSpellDuration:F1}s ({rSpl})";

            debugStatusText.text = debugInfo;
        }
    }

    public void SyncBarsImmediately()
    {
        if (hpBar != null)
        {
            hpBar.maxValue = isSpellCardActive ? spellMaxHP : maxHP;
            hpBar.value = isSpellCardActive ? animatedSpellHP : currentHP;
        }
        if (orangeBar != null)
        {
            orangeBar.maxValue = isSpellCardActive ? spellMaxHP : maxHP;
            orangeBar.value = isSpellCardActive ? animatedSpellHP : currentHP;
        }
    }

    private void SetSliderAlpha(UnityEngine.UI.Slider slider, float alpha)
    {
        UnityEngine.UI.Image[] images = slider.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in images)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    public void PerformContinue()
    {
        continueCount++;
        currentHP = maxHP;
        isSpellCardActive = false;
        isOverheated = false;
        spellHP = 0f;
        spellMaxHP = 0f;
        spellTimer = 0f;
        totalSpellDuration = 0f;
        initialUltimateEnergy = 0f;
        UpdateUI();

        PlayerHitHandler hitHandler = GetComponentInChildren<PlayerHitHandler>();
        if (hitHandler != null) hitHandler.StartRebirthFromContinue();
    }

    public void ResetContinueCount()
    {
        continueCount = 0;
    }

    public void TriggerGameOver()
    {
        if (pauseManager == null) return;

        if (BossPracticeManager.IsPracticeMode)
        {
            pauseManager.SetPracticeResultMode(true, false);
        }
        else
        {
            pauseManager.SetGameOverMode(true);
            StartCoroutine(LoadSceneAsyncRoutine("Title"));
        }
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.SetActive(true);
        }

        if (BGMManager.Instance != null)
        {
            BGMManager.Instance.FadeOut();
        }

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;

        float fakeProgress = 0f;
        float targetFakeProgress = 0f;
        float timer = 0f;

        while (!asyncOp.isDone)
        {
            float realProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);

            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                timer = UnityEngine.Random.Range(0.05f, 0.22f);

                if (fakeProgress < realProgress)
                {
                    float maxNext = Mathf.Min(realProgress, fakeProgress + UnityEngine.Random.Range(0.02f, 0.12f));
                    targetFakeProgress = UnityEngine.Random.Range(fakeProgress, maxNext);
                }
                else if (realProgress >= 1.0f && fakeProgress < 0.95f)
                {
                    targetFakeProgress = Mathf.MoveTowards(fakeProgress, 1.0f, UnityEngine.Random.Range(0.03f, 0.08f));
                }
            }

            fakeProgress = Mathf.MoveTowards(fakeProgress, targetFakeProgress, Time.unscaledDeltaTime * UnityEngine.Random.Range(0.6f, 1.5f));

            if (fakeProgress > realProgress && realProgress < 1.0f)
            {
                fakeProgress = realProgress;
            }

            if (progressBarSlider != null)
            {
                progressBarSlider.value = fakeProgress;
            }

            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(fakeProgress * 100f)}%";
            }

            if (fakeProgress >= 0.99f && realProgress >= 1.0f)
            {
                if (progressBarSlider != null) progressBarSlider.value = 1.0f;
                if (progressText != null) progressText.text = "100%";

                yield return new WaitForSecondsRealtime(0.25f);
                asyncOp.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    public void AddLife(int amount)
    {
        life = Mathf.Min(life + amount, 8);
        UpdateUI();
        if (extendUI != null) extendUI.Show("Extend!!", new Color(1f, 0.4f, 0.7f));
    }

    public void AddLifePiece(int amount)
    {
        lifePieces += amount;
        if (lifePieces >= lifePiecesRequired)
        {
            lifePieces -= lifePiecesRequired;
            AddLife(1);
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SE_EXTEND2);
        }
        UpdateUI();
    }

    public IEnumerator PlayKOAnimation()
    {
        if (koText == null) yield break;
        koText.gameObject.SetActive(true);

        koText.transform.localScale = Vector3.zero;
        float elapsed = 0;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float scale = 0;
            if (t < 0.7f) scale = Mathf.Lerp(0, 1.5f, t / 0.7f);
            else scale = Mathf.Lerp(1.5f, 1.0f, (t - 0.7f) / 0.3f);

            koText.transform.localScale = Vector3.one * scale;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        koText.transform.localScale = Vector3.one;
    }

    public IEnumerator FadeOutKOAnimation(float duration)
    {
        if (koText == null) yield break;
        Color startColor = koText.color;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1, 0, elapsed / duration);
            koText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
        koText.gameObject.SetActive(false);
        koText.color = startColor;
    }

    public float GetJealousyMultiplier()
    {
        if (!HasPassiveSkill(PassiveSkillType.JealousyAtkBoost))
        {
            return 1.0f;
        }

        if (isSpellCardActive)
        {
            return 1.5f;
        }

        if (_playerMove != null && _playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null && oppStatus.isSpellCardActive)
            {
                return 1.5f;
            }

            PlayerMove oppMove = _playerMove.Opponent.GetComponent<PlayerMove>();
            if (oppMove != null)
            {
                float oppGauge = Mathf.Clamp(oppMove.ultimateEnergy, 0f, 300f);
                float gaugeRatio = oppGauge / 300f;
                return Mathf.Lerp(1.0f, 1.5f, gaugeRatio);
            }
        }

        return 1.0f;
    }

    public bool IsSlothBoostActive()
    {
        if (!HasPassiveSkill(PassiveSkillType.SlothStandStillBoost) || _playerMove == null)
        {
            return false;
        }

        bool isMovingInputActive = Mathf.Abs(_playerMove.currentFrameInput.h) > 0.001f ||
                                   Mathf.Abs(_playerMove.currentFrameInput.v) > 0.001f;

        return !isMovingInputActive;
    }

    private class StatusEvaluator
    {
        public string Name;
        public StatusRank Rank;
        public int Order;
    }

    private void ExecutePrideStatusSteal()
    {
        if (!HasPassiveSkill(PassiveSkillType.PrideStatusSteal) || _playerMove == null || _playerMove.Opponent == null) return;

        PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
        if (oppStatus == null || oppStatus.characterData == null) return;

        if (characterData != null && !_hasCachedRank)
        {
            _originalBackup.hp = characterData.rankHP;
            _originalBackup.mp = characterData.rankMP;
            _originalBackup.attack = characterData.rankAttack;
            _originalBackup.agility = characterData.rankAgility;
            _originalBackup.mmpRegen = characterData.rankMMPRegen;
            _originalBackup.spellZone = characterData.rankSpellZone;
            _hasCachedRank = true;
        }

        var oppStats = new System.Collections.Generic.List<StatusEvaluator>
        {
            new StatusEvaluator { Name = "HP",        Rank = oppStatus.characterData.rankHP,        Order = 0 },
            new StatusEvaluator { Name = "MP",        Rank = oppStatus.characterData.rankMP,        Order = 1 },
            new StatusEvaluator { Name = "Attack",    Rank = oppStatus.characterData.rankAttack,    Order = 2 },
            new StatusEvaluator { Name = "Agility",   Rank = oppStatus.characterData.rankAgility,   Order = 3 },
            new StatusEvaluator { Name = "MMPRegen",  Rank = oppStatus.characterData.rankMMPRegen,  Order = 4 },
            new StatusEvaluator { Name = "SpellZone", Rank = oppStatus.characterData.rankSpellZone, Order = 5 }
        };

        oppStats.Sort((a, b) =>
        {
            if (a.Rank != b.Rank) return a.Rank.CompareTo(b.Rank);
            return a.Order.CompareTo(b.Order);
        });

        Debug.Log($"<color=gold>👑【傲慢のスキャン】相手({oppStatus.characterData.characterName})の低スペック上位: 1位 {oppStats[0].Name}({oppStats[0].Rank}), 2位 {oppStats[1].Name}({oppStats[1].Rank})</color>");

        for (int i = 0; i < 2; i++)
        {
            UpgradeTargetStatus(oppStats[i].Name);
        }

        ApplyCharacterRanks();
    }

    private void UpgradeTargetStatus(string statName)
    {
        if (characterData == null) return;

        switch (statName)
        {
            case "HP": characterData.rankHP = GetNextRank(characterData.rankHP); break;
            case "MP": characterData.rankMP = GetNextRank(characterData.rankMP); break;
            case "Attack": characterData.rankAttack = GetNextRank(characterData.rankAttack); break;
            case "Agility": characterData.rankAgility = GetNextRank(characterData.rankAgility); break;
            case "MMPRegen": characterData.rankMMPRegen = GetNextRank(characterData.rankMMPRegen); break;
            case "SpellZone": characterData.rankSpellZone = GetNextRank(characterData.rankSpellZone); break;
        }
    }

    public void RestoreOriginalRank()
    {
        if (_hasCachedRank && characterData != null)
        {
            characterData.rankHP = _originalBackup.hp;
            characterData.rankMP = _originalBackup.mp;
            characterData.rankAttack = _originalBackup.attack;
            characterData.rankAgility = _originalBackup.agility;
            characterData.rankMMPRegen = _originalBackup.mmpRegen;
            characterData.rankSpellZone = _originalBackup.spellZone;

            Debug.Log($"<color=cyan>🔄【デバッグ安全弁】{characterData.characterName} の6大ステータスランクを初期状態へ完全復元しました。</color>");
        }
    }

    public bool IsSlothRegenBlocked()
    {
        if (_playerMove != null && _playerMove.Opponent != null)
        {
            PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null && oppStatus.isSpellCardActive && oppStatus.characterData != null)
            {
                if (oppStatus.characterData.vjtEffectType == VJTEffectType.SlothStagnation)
                {
                    if (HasPassiveSkill(PassiveSkillType.NihilityFieldCancel)) return false;

                    bool isCurrentlyMoving = Mathf.Abs(_playerMove.currentFrameInput.h) > 0.001f ||
                                             Mathf.Abs(_playerMove.currentFrameInput.v) > 0.001f;

                    return isCurrentlyMoving;
                }
            }
        }
        return false;
    }

    void OnDestroy()
    {
        RestoreOriginalRank();
    }

    private StatusRank GetNextRank(StatusRank current)
    {
        if (current == StatusRank.EX) return StatusRank.EX;
        return (StatusRank)((int)current + 1);
    }

    public void SetInvincible(float duration)
    {
        invincibleTimer = duration;
        deathBombTimer = 0;
        if (_playerMove != null) _playerMove.SetInvincible(duration);
    }

    private void ClearAllBulletsOnField()
    {
        DanmakuBullet[] pBullets = UnityEngine.Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var b in pBullets) b.Deactivate(true, force: true);

        EnemyBullet[] eBullets = UnityEngine.Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var b in eBullets) b.Deactivate(true);
    }

    private void ExecuteFieldEffectToOpponent()
    {
        if (characterData == null || _playerMove == null || _playerMove.Opponent == null) return;

        PlayerMove oppMove = _playerMove.Opponent;
        GameObject oppObj = oppMove.gameObject;
        PlayerStatusManager oppStatus = oppObj.GetComponent<PlayerStatusManager>();

        if (oppStatus == null) return;
        if (oppStatus.IsInvincible) return;

        if (oppStatus.HasPassiveSkill(PassiveSkillType.NihilityFieldCancel))
        {
            return;
        }

        float value_W = 1.0f;
        float value_L = 1.5f;

        switch (characterData.vjtEffectType)
        {
            case VJTEffectType.WrathBurn:
                _hpDrainAccumulator += value_W * Time.deltaTime;

                if (_hpDrainAccumulator >= 1f)
                {
                    int damageToApply = Mathf.FloorToInt(_hpDrainAccumulator);
                    oppStatus.ApplyDamage(damageToApply);
                    _hpDrainAccumulator -= damageToApply;
                }
                break;

            case VJTEffectType.LustHit:
                if (oppStatus.playerCollider is CircleCollider2D oppCircle)
                {
                    float currentBaseRadius = oppStatus.HasPassiveSkill(PassiveSkillType.LustSmall) ? oppStatus.originalColliderRadius * 0.8f : oppStatus.originalColliderRadius;
                    oppCircle.radius = currentBaseRadius * value_L;
                }
                break;

            case VJTEffectType.GreedCast:
                SkillManager oppSkill = oppObj.GetComponentInChildren<SkillManager>();
                if (oppSkill != null)
                {
                }
                break;

            case VJTEffectType.JealousyFog:
                if (jealousyFogPrefab == null) return;

                _fogSpawnTimer += Time.deltaTime;
                if (_fogSpawnTimer >= 0.01f)
                {
                    _fogSpawnTimer = 0f;

                    for (int i = 0; i < 12; i++)
                    {
                        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * 1.8f;
                        Vector3 spawnPosition = oppObj.transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

                        GameObject fogInstance = Instantiate(jealousyFogPrefab, spawnPosition, Quaternion.identity);

                        if (oppStatus.playerCollider != null)
                        {
                            fogInstance.transform.localScale = oppStatus.playerCollider.transform.localScale;
                        }
                    }
                }
                break;

            case VJTEffectType.GluttonyPull:
                const float PULL_FORCE = 1.0f;

                Vector2 myPosition = transform.position;
                Vector2 oppPosition = oppObj.transform.position;
                Vector2 pullDir = (myPosition - oppPosition).normalized;

                if ((myPosition - oppPosition).sqrMagnitude > 0.01f)
                {
                    oppMove.AddExternalPull(pullDir * PULL_FORCE);
                }
                break;

            case VJTEffectType.SlothStagnation:
                oppMove.skillSpeedMultiplier = 0.9f;
                break;
        }
    }
}