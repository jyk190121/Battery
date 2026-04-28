using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 올무벼룩(Snare Flea) 전용 특수 상태로, 플레이어의 머리에 달라붙어 지속 데미지를 주는 상태입니다.
/// 숙주의 시야를 차단하고, 숙주가 죽거나 강제로 떼어질 때까지 찰거머리처럼 붙어있습니다.
/// </summary>
public class AttachedState : MonsterBaseState
{
    // =========================================================
    // 1. 변수 선언부 
    // =========================================================

    private PlayerController _snaredPlayer;
    private float _damageTimer;

    // =========================================================
    // 2. 초기화 함수 
    // =========================================================

    public AttachedState(MonsterController owner) : base(owner)
    {
        this.currentTickInterval = 0.1f;
    }

    public override void Enter()
    {
        base.Enter();

        _damageTimer = 0f;
        _snaredPlayer = null;

        Transform target = owner.scanner.CurrentTarget;
        if (target != null)
        {
            _snaredPlayer = target.GetComponentInParent<PlayerController>();
        }

        // 1. 타겟이 유효한지 1차 검사 (살아있는 플레이어인가?)
        if (_snaredPlayer != null && !_snaredPlayer.isDead.Value)
        {
            _snaredPlayer.isSnared.Value = true;

            // --- [카메라 제어 추가] ---
            // 잡힌 플레이어가 로컬 플레이어(나)라면 카메라를 몬스터용으로 전환
            if (_snaredPlayer.IsOwner && CinemachineController.Instance != null)
            {
                CinemachineController.Instance.SetMonsterCameraActive();
            }
            // --------------------------

            // 2. 에이전트 정지 (숙주를 따라가야 하므로 스스로의 길찾기/물리 이동 차단)
            owner.navAgent.enabled = false;
            // [TODO] 콜라이더를 꺼야 할 경우 아래 주석 해제
            // if (owner.TryGetComponent<Collider>(out var col)) col.enabled = false;

            // 3. 부모-자식 네트워크 동기화 설정 (플레이어 머리에 붙임)
            owner.NetworkObject.TrySetParent(_snaredPlayer.NetworkObject, false);

            // 4. 네트워크 트랜스폼 싱크 지연을 고려하여 1프레임 뒤에 정확한 위치(머리 위)로 로컬 고정
            owner.StartCoroutine(FixPositionNextFrame());

            //// 5. 해당 플레이어의 화면에만 시야 차단(눈뽕/촉수) UI를 띄우도록 RPC 전송
            //owner.TriggerSnareBlindRpc(true, owner.RpcTarget.Single(_snaredPlayer.OwnerClientId, RpcTargetUse.Temp));

            // 6.  충격으로 인해 현재 들고 있는 아이템 떨어뜨리기
            if (_snaredPlayer.TryGetComponent<PlayerInventory>(out var inventory))
            {
                inventory.ForceDropCurrentItemServer();
            }

        }
        else
        {
            // 타겟이 유효하지 않으면 즉시 떨어져서 순찰 상태로 복귀
            owner.ChangeState(MonsterStateType.Patrol);
        }
    }

    public override void Exit()
    {
        base.Exit();

        // 1. 숙주 플레이어 화면 복구 및 상태 해제
        if (_snaredPlayer != null && _snaredPlayer.IsSpawned)
        {
            _snaredPlayer.isSnared.Value = false;
            //owner.TriggerSnareBlindRpc(false, owner.RpcTarget.Single(_snaredPlayer.OwnerClientId, RpcTargetUse.Temp));

            // --- [카메라 제어 추가] ---
            // 풀려날 때 로컬 플레이어라면 다시 메인 카메라로 복구
            if (_snaredPlayer.IsOwner && CinemachineController.Instance != null)
            {
                CinemachineController.Instance.SetMainCameraActive();
            }
            // --------------------------
        }

        // 2. [네트워크 종속 해제] 플레이어 머리에서 강제로 떨어짐
        if (owner.NetworkObject.TryRemoveParent())
        {
            // 바닥에 똑바로 안착할 수 있도록 X, Z축 회전을 0으로 리셋 (Y축 방향은 유지)
            owner.transform.rotation = Quaternion.Euler(0, owner.transform.eulerAngles.y, 0);
        }

        // 3. 서버일 경우 다시 스스로 움직일 수 있도록 네비게이션 에이전트 복구
        owner.navAgent.enabled = owner.IsServer;
        // [TODO] 콜라이더 복구 필요 시 아래 주석 해제
        // if (owner.TryGetComponent<Collider>(out var col)) col.enabled = true;

        _snaredPlayer = null;
        Debug.Log("<color=yellow>[Snare Flea]</color> 플레이어에게서 완전히 분리되어 바닥으로 떨어졌습니다.");
    }


    // =========================================================
    // 3. 유니티 루프 및 AI 틱
    // =========================================================

    /// <summary>
    /// 매 프레임 실행: 타이머를 계산하여 주기적으로 틱 데미지를 입힙니다.
    /// </summary>
    public override void Update()
    {
        base.Update();

        // 1. 방어 코드: 숙주가 로그아웃했거나 이미 죽었다면 즉시 상태 해제
        if (_snaredPlayer == null || _snaredPlayer.isDead.Value)
        {
            Debug.LogWarning("<color=yellow>[Snare Flea]</color> 숙주가 사망했거나 사라졌습니다. 바닥으로 떨어집니다.");
            owner.ChangeState(MonsterStateType.Patrol);
            return;
        }

        // 2. 지속 데미지 (Tick Damage) 계산
        _damageTimer += Time.deltaTime;

        if (_damageTimer >= data.snareTickRate)
        {
            _damageTimer = 0f;

            // 서버 측 플레이어 컨트롤러로 데미지 처리 요청
            _snaredPlayer.TakeDamageServerRpc(data.snareTickDamage);

            Debug.Log($"<color=red>[Snare Flea]</color> 목 조르기! (데미지: {data.snareTickDamage})");
        }
    }

    /// <summary>
    /// 머리에 붙어있는 동안에는 길찾기나 주변 수색(Scanner)을 하지 않으므로 비워둡니다.
    /// </summary>
    protected override void OnTick()
    {
        // 의도적으로 비워둠
    }


    // =========================================================
    // 4. 퍼블릭 함수 (Public Methods)
    // =========================================================

    // 본 스크립트에서는 미사용


    // =========================================================
    // 5. 프라이빗 헬퍼 함수 
    // =========================================================

    /// <summary>
    /// 네트워크 부모 지정(TrySetParent) 직후 위치가 튀는 현상을 막기 위해,
    /// 1프레임 대기 후 플레이어의 머리 높이 로컬 좌표로 고정하는 코루틴입니다.
    /// </summary>
    private IEnumerator FixPositionNextFrame()
    {
        yield return null; // 1프레임 대기 (네트워크 싱크 확보)

        owner.transform.localPosition = new Vector3(-0.15f, 1.6f, 0.1f);
        owner.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }
}