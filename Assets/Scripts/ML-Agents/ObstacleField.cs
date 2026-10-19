using UnityEngine;
using System.Collections;

/// <summary>
/// 指定された座標（敵の位置）に予告範囲を表示し、一定時間後に巨大な罠（お邪魔弾幕）を展開するクラス。
/// 領域内の敵弾を消去しつつ、触れている相手に継続ダメージと移動制限を与える。
/// </summary>
[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class TrapField : MonoBehaviour
{
    private Transform _owner;
    private BulletData _data;
    private float _duration;
    private float _warningTime = 2.0f; // 💡 予告時間を長めに設定（必要に応じて調整可能）

    [Header("Size Settings")]
    [SerializeField] private float _maxScale = 3.0f; // 巨大な範囲
    [SerializeField] private float _expandTime = 0.2f; // 🌟 実体化時にサイズ0から膨らむ時間
    [SerializeField] private float _shrinkTime = 0.4f;

    [Header("⚡ Continuous Hit & Clean Settings")]
    [Tooltip("継続ダメージの発生間隔（秒）")]
    [SerializeField] private float _hitInterval = 0.5f;
    private float _nextHitEnableTime = 0f;

    private SpriteRenderer _spriteRenderer;
    private CircleCollider2D _collider;
    private bool _isActive = false; // 予告中は当たり判定・弾消しを無効化する

    /// <summary>
    /// 初期化。敵の座標を受け取り、その場で警告から展開へと移行する。
    /// </summary>
    public void Initialize(Transform owner, BulletData data, float duration, string bulletTag, int layer, float overrideScale = -1f, float warningTime = 2.0f)
    {
        _owner = owner;
        _data = data;
        _duration = duration;
        _warningTime = warningTime;

        if (overrideScale > 0f)
        {
            _maxScale = overrideScale;
        }

        gameObject.tag = bulletTag;
        gameObject.layer = layer;

        _collider = GetComponent<CircleCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_collider != null)
        {
            _collider.isTrigger = true;
            _collider.radius = data.radius;
            _collider.offset = data.colliderOffset;
        }

        StartCoroutine(TrapRoutine());
    }

    private IEnumerator TrapRoutine()
    {
        // 1. 【予告フェーズ】：範囲警告（半透明）を表示しつつ、まだダメージや弾消しは行わない
        _isActive = false;
        if (_spriteRenderer != null)
        {
            Color warningColor = _data != null ? _data.breakColor : Color.red;
            warningColor.a = 0.3f; // 半透明の警告カラー
            _spriteRenderer.color = warningColor;
        }

        // 予告中はあらかじめフルサイズの枠を表示しておく
        transform.localScale = Vector3.one * _maxScale;

        float warnElapsed = 0f;
        while (warnElapsed < _warningTime)
        {
            warnElapsed += Time.deltaTime;
            yield return null;
        }

        // 2. 【実体化・展開フェーズ】：サイズを一度 0 にリセットし、そこから指定の大きさへ一気に膨らませる！
        _isActive = true;
        if (_spriteRenderer != null)
        {
            Color activeColor = _data != null ? _data.breakColor : Color.white;
            activeColor.a = 0.9f; // 本番の濃い色
            _spriteRenderer.color = activeColor;
        }

        float expandElapsed = 0f;
        while (expandElapsed < _expandTime)
        {
            expandElapsed += Time.deltaTime;
            float currentScale = Mathf.SmoothStep(0f, _maxScale, expandElapsed / _expandTime);
            transform.localScale = Vector3.one * currentScale;
            yield return null;
        }
        transform.localScale = Vector3.one * _maxScale;

        // 本番稼働の持続時間待機
        float stayElapsed = 0f;
        while (stayElapsed < _duration)
        {
            if (!PlayerMove.CanShoot) break;
            stayElapsed += Time.deltaTime;
            yield return null;
        }

        // 3. 【消滅フェーズ】：縮小して消える
        _isActive = false;
        float elapsed = 0f;
        while (elapsed < _shrinkTime)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.SmoothStep(_maxScale, 0, elapsed / _shrinkTime);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleCollisionLogic(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        HandleCollisionLogic(collision);
    }

    /// <summary>
    /// 罠フィールド内の当たり判定（弾消し ＆ 相手への継続ダメージ）
    /// </summary>
    private void HandleCollisionLogic(Collider2D collision)
    {
        if (!_isActive || _owner == null) return;

        // 2. 相手プレイヤーへのダメージ ＆ 滞在による移動制限（プレッシャー）付与
        if (collision.CompareTag("Player") && collision.transform != _owner)
        {
            if (Time.time >= _nextHitEnableTime)
            {
                _nextHitEnableTime = Time.time + _hitInterval;
                collision.SendMessage("OnHit", _data.damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}