using UnityEngine;

/// <summary>
/// 인형이 플레이어에게 들켰을 때 괴성을 지르고 소멸하는 상태입니다.
/// </summary>
public class ScreamState : MonsterBaseState
{
    private float _screamTimer;
    private bool _hasScreamed;
    private Transform _targetPlayer;

    public ScreamState(MonsterController owner) : base(owner) { }

    public override void Enter()
    {
        base.Enter();
        _screamTimer = 0f;
        _hasScreamed = false;

        _targetPlayer = owner.scanner.CurrentTarget;

        // 1. 발 묶기 (가만히 서서 비명 지름)
        if (owner.navAgent != null && owner.navAgent.isOnNavMesh)
        {
            owner.navAgent.isStopped = true;
            owner.navAgent.velocity = Vector3.zero;
        }

         owner.animHandler.PlayScream();
    }

    public override void Update()
    {
        base.Update();

        _screamTimer += Time.deltaTime;

        // 진입 직후 딱 한 번만 소리 발생 로직 실행
        if (!_hasScreamed)
        {
            _hasScreamed = true;
            ExecuteScreamLogic();
        }

        // 3초 지속 후 소멸(죽음) 판정
        if (_screamTimer >= 3.0f)
        {
            Debug.Log("<color=gray>[Doll]</color> 비명 3초 경과. 인형이 바스라지며 소멸합니다.");

            // 일반 공격으론 안 죽는 무적이므로, 여기서 강제로 상태를 넘기거나 체력을 0으로 만듭니다.
            owner.ChangeState(MonsterStateType.Dead);
        }
    }

    private void ExecuteScreamLogic()
    {
        // 1. [로컬 타겟 전용 사운드] 
        // 대상 플레이어에게만 ClientRpc를 보내어 귀청이 떨어질 듯한 UI 사운드를 재생시킵니다.
        if (_targetPlayer != null && _targetPlayer.TryGetComponent<PlayerController>(out var playerController))
        {
            // owner.PlayScreamSoundClientRpc(playerController.OwnerClientId); 
            // (이 Rpc 함수는 MonsterController에 작성하시면 됩니다)
            Debug.Log($"<color=red>[Doll]</color> {playerController.name}의 클라이언트 화면에 끔찍한 비명 소리 재생!");
        }

        // 2. [서버 어그로 데이터] 
        // 실제 오디오 볼륨은 0이지만, AI 시스템(EnvironmentScanner)이 들을 수 있는
        // 엄청난 크기의 노이즈(반경 50m)를 발생시켜 맵 전체의 몬스터를 이쪽으로 끌어당깁니다.
        if (EnemyManager.Instance != null)
        {
            Vector3 noiseOrigin = owner.transform.position;

            foreach (EnvironmentScanner scanner in EnemyManager.Instance.ActiveScanners)
            {
                if (scanner.owner == this.owner) continue; // 나 자신은 무시

                // 실내 판정(isIndoorMonster)을 true로 주어 같은 공간의 몹들이 듣게 함
                scanner.OnHeardSound(noiseOrigin, 50f, true);
            }

            Debug.Log("<color=magenta>[Doll]</color> 엄청난 비명 소리가 발생해 주변 몬스터들의 어그로를 끌었습니다!");
        }
    }
}