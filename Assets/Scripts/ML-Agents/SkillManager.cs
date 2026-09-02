// --- SkillManager.cs 【ストーリー高速連射 ＆ VSリキャスト完全両立版】 ---
using KanKikuchi.AudioManager;
using TMPro;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
    PlayerSkillData skillData;

    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler;
    private PlayerDanmakuEmitter emitter;
    private PlayerStatusManager statusManager;

    [Header("UI Slots (Normal Skills Only)")]
    public SkillCooldownUI uiZ;
    public SkillCooldownUI uiX;
    public SkillCooldownUI uiC;
    public SkillCooldownUI uiV;

    private int burstCountZ, burstCountX, burstCountC, burstCountV;
    private float burstResetTimerZ, burstResetTimerX, burstResetTimerC, burstResetTimerV;
    private float _recoveryDelayTimer = 0f;
    private const float BURST_RESET_DELAY = 0.5f;
    public float timerZ, timerX, timerC, timerV, timerEX;

    private const float EX_COOLDOWN = 2.5f;

    [Header("Energy UI")]
    public EnergyGaugeUI energyGauge;
    [Header("Ultimate UI")]
    public UltimateGaugeUI ultimateGaugeUI;

    private float _cPressedTimestamp = -100f;
    private float _vPressedTimestamp = -100f;
    private const float INTERACTION_WINDOW = 0.08f;

    private int _cHoldFrame = 0;
    private int _vHoldFrame = 0;
    private const int HOLD_REMAINS_FRAMES = 5;

    private bool _isExExecutedInThisWindow = false;
    private PlayerMove.ReplayFrame _lastInput;
    private float _recoveryCooldownTimer = 0f;
    private float CostRegenMultiplier = 0;

    // 🧬【汎用チャージマネジメントスロット】：各スロットが現在溜め状態にあるかを追跡
    private bool _isZCharging = false;

    // 🌲 ストーリーモード専用の超高速連射用タイマー
    private float _storyZFireTimer = 0f;

    [Header("🔧 Cost Debug UI Slots")]
    [Tooltip("コストの現在値と最大値を表示するTMPテキストを登録してください")]
    public TextMeshProUGUI energyNumericText;
    [Tooltip("マナ自然回復が再開するまでの待機硬直タイマーを表示するTMPテキストを登録してください")]
    public TextMeshProUGUI recoveryDelayNumericText;

    void Start()
    {
        playerMove = GetComponentInParent<PlayerMove>();
        hitHandler = GetComponentInParent<PlayerHitHandler>();
        emitter = GetComponentInParent<PlayerDanmakuEmitter>();
        statusManager = GetComponentInParent<PlayerStatusManager>();

        if (ultimateGaugeUI != null && playerMove != null)
        {
            ultimateGaugeUI.Initialize(playerMove);
        }

        if (statusManager != null)
        {
            skillData = statusManager.characterData;
        }

        if (playerMove != null && skillData != null)
        {
            playerMove.currentEnergy = playerMove.maxEnergy;

            if (energyGauge != null) energyGauge.Initialize(playerMove);
        }

        if (skillData != null)
        {
            if (uiZ != null) uiZ.SetSkillIcon(skillData.skillZ.skillIcon);
            if (uiX != null) uiX.SetSkillIcon(skillData.skillX.skillIcon);
            if (uiC != null) uiC.SetSkillIcon(skillData.skillC.skillIcon);
            if (uiV != null) uiV.SetSkillIcon(skillData.skillV.skillIcon);
        }

        // 🎯 ストーリーモード時はスキルUI（アイコン等）を不可視にする[cite: 3, 20]
        if (GameModeManager.IsStoryMode)
        {
            if (uiZ != null && uiZ.gameObject != null) uiZ.gameObject.SetActive(false);
            if (uiX != null && uiX.gameObject != null) uiX.gameObject.SetActive(false);
            if (uiC != null && uiC.gameObject != null) uiC.gameObject.SetActive(false);
            if (uiV != null && uiV.gameObject != null) uiV.gameObject.SetActive(false);
        }

        ResetAllTimers();
        UpdateAllCooldownUI();
        UpdateCostNumericText();
    }

    void Update()
    {
        // 🌲 ストーリーモード用の連射タイマーを毎フレーム減衰
        if (_storyZFireTimer > 0f)
        {
            _storyZFireTimer -= Time.deltaTime;
        }

        if (playerMove == null || skillData == null || statusManager == null) return;
        if (!PlayerMove.CanShoot)
        {
            playerMove.currentEnergy = playerMove.maxEnergy;
        }
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            if (_isZCharging)
            {
                _isZCharging = false;
            }
            return;
        }

        PlayerDanmakuEmitter activeEmitter = null;
        PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
        if (allEmitters == null || allEmitters.Length == 0) allEmitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);

        foreach (var em in allEmitters)
        {
            if (em != null && em.enabled)
            {
                activeEmitter = em;
                break;
            }
        }

        if (activeEmitter == null) activeEmitter = emitter;

        const float BASE_WAIT_SECONDS = 0.5f;

        if (activeEmitter != null && activeEmitter.IsAnySkillActive)
        {
            float passiveDelayRate = 1.0f;
            if (statusManager != null && statusManager.HasPassiveSkill(PassiveSkillType.GreedReduction))
            {
                passiveDelayRate = 0.7f;
            }

            if (statusManager.isSpellCardActive)
            {
                _recoveryCooldownTimer = (BASE_WAIT_SECONDS * 0.5f) * passiveDelayRate;
            }
            else if (statusManager.isOverheated)
            {
                _recoveryCooldownTimer = (BASE_WAIT_SECONDS * 2.0f) * passiveDelayRate;
            }
            else
            {
                _recoveryCooldownTimer = BASE_WAIT_SECONDS * passiveDelayRate;
            }
        }
        else
        {
            if (_recoveryCooldownTimer > 0f)
            {
                _recoveryCooldownTimer -= Time.deltaTime;
            }
        }

        if (_recoveryCooldownTimer <= 0f && PlayerMove.CanShoot)
        {
            float regenMultiplier = 1.0f;
            if (statusManager.isOverheated) regenMultiplier = 0.5f;
            else if (statusManager.isSpellCardActive)
            {
                regenMultiplier = 2.0f;

                if (statusManager.characterData != null && statusManager.characterData.vjtEffectType == VJTEffectType.GreedCast)
                {
                    regenMultiplier *= 1.5f;
                }
            }

            if (playerMove != null && playerMove.Opponent != null)
            {
                PlayerStatusManager oppStatus = playerMove.Opponent.GetComponent<PlayerStatusManager>();
                if (oppStatus != null && oppStatus.isSpellCardActive && oppStatus.characterData != null)
                {
                    if (oppStatus.characterData.vjtEffectType == VJTEffectType.GreedCast)
                    {
                        regenMultiplier *= 0.5f;
                    }
                }
            }

            if (statusManager != null && statusManager.IsSlothRegenBlocked())
            {
                regenMultiplier = 0f;
            }

            if (statusManager != null && statusManager.IsSlothBoostActive())
            {
                regenMultiplier *= 1.5f;
            }

            playerMove.currentEnergy = Mathf.Min(
                playerMove.maxEnergy,
                playerMove.currentEnergy + (playerMove.energyRegenRate * regenMultiplier * Time.deltaTime)
            );
        }

        UpdateTimers();
        UpdateAllCooldownUI();
        UpdateCostNumericText();

        if (!PlayerMove.CanShoot) return;
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal) return;

        bool zPressed = false;
        bool xPressed = false;
        bool cPressed = false;
        bool vPressed = false;
        bool exPressed = false;
        bool vjtPressed = false;

        DanmakuAgent agent = GetComponentInParent<DanmakuAgent>();

        if (agent != null && (agent._useAutoEvadeAI || playerMove.currentMode == PlayerMove.ReplayMode.Playing))
        {
            var input = playerMove.currentFrameInput;
            zPressed = input.shotZ;
            xPressed = input.shotX;
            cPressed = input.shotV ? false : input.shotC;
            vPressed = input.shotV;
            exPressed = input.ultimate;

            vjtPressed = (agent._useAutoEvadeAI && input.ultimate &&
                          playerMove.ultimateEnergy >= 200f && !statusManager.isSpellCardActive);
        }
        else
        {
            if (InputManager.Instance != null)
            {
                var inputSet = (playerMove.playerId == 1) ? InputManager.Instance.player1 : InputManager.Instance.player2;

                zPressed = inputSet.skillZ.action.IsPressed();
                xPressed = inputSet.skillX.action.IsPressed();
                cPressed = inputSet.skillC.action.IsPressed();
                vPressed = inputSet.skillV.action.IsPressed();

                bool isZX_Combination = (zPressed && xPressed);
                if (isZX_Combination && (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.X)))
                {
                    vjtPressed = true;
                }

                if (!vjtPressed && inputSet.skillVJT != null && inputSet.skillVJT.action != null)
                {
                    if (inputSet.skillVJT.action.WasPressedThisFrame())
                    {
                        vjtPressed = true;
                    }
                }

                if (inputSet.skillEX != null && inputSet.skillEX.action != null)
                {
                    exPressed = inputSet.skillEX.action.WasPressedThisFrame();
                }
                else
                {
                    if (cPressed && vPressed && (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V)))
                    {
                        exPressed = true;
                    }
                }
            }
        }

        // 🎯 ストーリーモード時はZスキル以外を使用不可にする[cite: 3, 20]
        if (GameModeManager.IsStoryMode)
        {
            xPressed = false;
            cPressed = false;
            vPressed = false;
            exPressed = false;
            vjtPressed = false;
        }

        if (vjtPressed && !GameModeManager.IsStoryMode)
        {
            return;
        }

        if (exPressed)
        {
            return;
        }

        if (activeEmitter != null && activeEmitter.IsUltimateSkillActive)
        {
            return;
        }

        // =========================================================================
        // 🌲【ストーリーモード時の超高速連射バイパス】：Zスキルのみ 0.05秒 間隔で連射させる
        // =========================================================================
        if (GameModeManager.IsStoryMode)
        {
            if (zPressed && _storyZFireTimer <= 0f)
            {
                _storyZFireTimer = 0.05f; // 0.05秒間隔に設定

                // ストーリー用連射ショットの発射処理
                Vector3 spawnPos = transform.position + new Vector3(0.3f, -0.2f, 0f);
                float straightAngle = 0f;
                for (int i = 0; i < 2; i++)
                {
                    activeEmitter.ExecuteSubShot(
                        data: skillData.skillZ.bulletData,
                        pos: spawnPos,
                        speed: skillData.skillZ.speed > 0f ? skillData.skillZ.speed : 10f,
                        angle: straightAngle,
                        accel: 0f,
                        maxSpeed: 0f,
                        tag: activeEmitter.targetTag,
                        layer: gameObject.layer,
                        delay_: 1.0f
                    );
                    spawnPos += new Vector3(0, 0.4f, 0f);
                }
                if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT1, 0.3f);
            }
            return; // 通常のクールタイム付きスキル判定へ進まないようにここでリターン
        }
        // =========================================================================

        bool isMyVjtActive = (statusManager != null && statusManager.isSpellCardActive);
        HandleSkillInput(zPressed, ref timerZ, skillData.skillZ, isMyVjtActive, activeEmitter);
        HandleSkillInput(xPressed, ref timerX, skillData.skillX, isMyVjtActive, activeEmitter);
        HandleSkillInput(cPressed, ref timerC, skillData.skillC, isMyVjtActive, activeEmitter);
        HandleSkillInput(vPressed, ref timerV, skillData.skillV, isMyVjtActive, activeEmitter);

        UpdateCostNumericText();
    }

    private void UpdateCostNumericText()
    {
        if (playerMove == null) return;

        if (energyNumericText != null)
        {
            int currentEnergyInt = (int)playerMove.currentEnergy;
            int maxEnergyInt = (int)playerMove.maxEnergy;
            energyNumericText.text = $"{currentEnergyInt} / {maxEnergyInt}";
        }

        if (recoveryDelayNumericText != null)
        {
            if (_recoveryCooldownTimer > 0f)
            {
                recoveryDelayNumericText.text = $"{_recoveryCooldownTimer:F1}s";
                recoveryDelayNumericText.color = new Color(1f, 1f, 1f);
            }
            else
            {
                recoveryDelayNumericText.text = "";
            }
        }
    }

    private void HandleSkillInput(bool isPressed, ref float timer, PlayerSkillData.SkillSettings settings, bool isVjtActive, PlayerDanmakuEmitter activeEmitter)
    {
        bool isCostAllowed = (playerMove.currentEnergy >= settings.cost);

        if (settings.isChargeSkill)
        {
            if (isPressed && timer <= 0 && isCostAllowed && !_isZCharging)
            {
                _isZCharging = true;
                _recoveryDelayTimer = 0f;
                activeEmitter.Fire(settings);
            }

            if (!isPressed && _isZCharging)
            {
                _isZCharging = false;
                playerMove.currentEnergy -= settings.cost;

                if (playerMove != null && statusManager != null)
                {
                    float finalGain = settings.ultimateGain;
                    if (statusManager.isOverheated)
                    {
                        finalGain *= 0.5f;
                    }
                    playerMove.AddUltimateEnergy(finalGain);
                }

                float cooldownMultiplier = statusManager.isOverheated ? 1.5f : 1.0f;
                timer = settings.cooldown * cooldownMultiplier;
            }
        }
        else
        {
            if (isPressed && timer <= 0 && isCostAllowed)
            {
                _recoveryDelayTimer = 0f;
                playerMove.currentEnergy -= settings.cost;
                activeEmitter.Fire(settings);

                float cooldownMultiplier = statusManager.isOverheated ? 1.5f : 1.0f;
                timer = settings.cooldown * cooldownMultiplier;
            }
        }
    }

    private void UpdateTimers()
    {
        float dtMultiplier = 1.0f;
        if (statusManager != null && statusManager.IsSlothBoostActive())
        {
            dtMultiplier = 1.3f;
        }

        float dt = Time.deltaTime * dtMultiplier;

        if (timerZ > 0) timerZ -= dt;
        if (timerX > 0) timerX -= dt;
        if (timerC > 0) timerC -= dt;
        if (timerV > 0) timerV -= dt;
        if (timerEX > 0) timerEX -= dt;
    }

    private void ResetAllTimers()
    {
        timerZ = timerX = timerC = timerV = timerEX = 0;
        burstCountZ = burstCountX = burstCountC = burstCountV = 0;
        burstResetTimerZ = burstResetTimerX = burstResetTimerC = burstResetTimerV = 0;
    }

    private void UpdateAllCooldownUI()
    {
        if (skillData == null) return;
        if (uiZ != null) uiZ.UpdateCooldown(timerZ, skillData.skillZ.cooldown * (statusManager.isOverheated ? 1.5f : 1.0f));
        if (uiX != null) uiX.UpdateCooldown(timerX, skillData.skillX.cooldown * (statusManager.isOverheated ? 1.5f : 1.0f));
        if (uiC != null) uiC.UpdateCooldown(timerC, skillData.skillC.cooldown * (statusManager.isOverheated ? 1.5f : 1.0f));
        if (uiV != null) uiV.UpdateCooldown(timerV, skillData.skillV.cooldown * (statusManager.isOverheated ? 1.5f : 1.0f));
    }

    public void InstantFullRecovery()
    {
        ResetAllTimers();
        if (playerMove != null)
        {
            playerMove.currentEnergy = playerMove.maxEnergy;
        }
        _recoveryDelayTimer = 0f;
        UpdateAllCooldownUI();
    }
}