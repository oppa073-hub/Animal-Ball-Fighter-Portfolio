using System;
using DG.Tweening;
using UnityEngine;

// 마녀 패턴1의 원형 늪 장판. 플레이어가 안에 있는 동안 도트 데미지 + 감속, 일정 시간 뒤 사라진다.
// 트리거 콜라이더 + 플레이어(Rigidbody)로 진입/이탈을 감지한다.
public class WitchSwampField : MonoBehaviour
{
    [SerializeField] private Transform visual;  //등장/퇴장 스케일 연출용

    private float damagePerTick;
    private float tickInterval;
    private float slowMultiplier;

    private PlayerBallController playerInside;
    private float tickTimer;
    private Action<WitchSwampField> returnedCallback;
    private Vector3 visualBaseScale;

    private void Awake()
    {
        if (visual != null) visualBaseScale = visual.localScale;
    }

    private void OnDisable()
    {
        ReleasePlayer();
        DOTween.Kill(this);
        if (visual != null) visual.DOKill();
        returnedCallback = null;
    }

    public void Activate(float lifeTime, float damagePerTick, float tickInterval, float slowMultiplier, Action<WitchSwampField> onReturned)
    {
        this.damagePerTick = damagePerTick;
        this.tickInterval = Mathf.Max(0.1f, tickInterval);
        this.slowMultiplier = slowMultiplier;
        returnedCallback = onReturned;
        tickTimer = 0f;

        if (visual != null)
        {
            visual.DOKill();
            visual.localScale = new Vector3(0f, visualBaseScale.y, 0f);
            visual.DOScale(visualBaseScale, 0.25f).SetEase(Ease.OutBack);
        }

        DOTween.Kill(this);
        DOVirtual.DelayedCall(lifeTime, Expire, false).SetTarget(this);
    }

    private void Update()
    {
        if (playerInside == null) return;
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing) return;

        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;
            DamageManager.Instance?.ApplyFixedDamage(playerInside.gameObject, damagePerTick, DamageTextType.Poison, false, false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerBallController player = other.GetComponent<PlayerBallController>();
        if (player == null) return;

        playerInside = player;
        tickTimer = 0f;
        player.ApplySlow(this, slowMultiplier);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        ReleasePlayer();
    }

    private void ReleasePlayer()
    {
        if (playerInside != null) playerInside.RemoveSlow(this);
        playerInside = null;
    }

    private void Expire()  //사라지는 연출 후 반환
    {
        ReleasePlayer();

        if (visual != null)
        {
            visual.DOKill();
            visual.DOScale(new Vector3(0f, visualBaseScale.y, 0f), 0.25f).OnComplete(ForceReturnToPool);
        }
        else
        {
            ForceReturnToPool();
        }
    }

    public void ForceReturnToPool()
    {
        ReleasePlayer();

        Action<WitchSwampField> callback = returnedCallback;
        returnedCallback = null;
        callback?.Invoke(this);

        PooledObject pooledObject = GetComponent<PooledObject>();
        if (pooledObject != null) pooledObject.ReturnToPool();
        else gameObject.SetActive(false);
    }
}
