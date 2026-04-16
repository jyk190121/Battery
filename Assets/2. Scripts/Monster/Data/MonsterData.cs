using UnityEngine;

[CreateAssetMenu(fileName = "NewMonsterData", menuName = "Monster/MonsterData")]
public class MonsterData : ScriptableObject
{
    [Header("Health Settings")]
    [Tooltip("몬스터의 기본 최대 체력")]
    public float maxHealth = 100f;

    [Header("Spawn Settings")]
    [Tooltip("이 몬스터가 스폰될 때 소모하는 스테이지 예산(점수)")]
    public int spawnCost = 2;
    [Tooltip("스폰 확률 가중치 (값이 높을수록 자주 등장함)")]
    public float spawnWeight = 50f;
    [Tooltip("맵에 동시에 존재할 수 있는 최대 마리 수")]
    public int maxSpawnCount = 2;
    [Tooltip("이 몬스터의 실제 프리팹 (EnemyManager가 생성할 때 사용)")]
    public GameObject monsterPrefab;

    [Header("Movement")]
    public float patrolSpeed = 3.5f;        // 순찰 속도
    public float chaseSpeed = 4.5f;         // 추격 속도

    [Header("Detection")] 
    public float viewRange = 12f;           // 시야 사거리
    public float viewAngle = 90f;           // 시야 각
    public float hearingRange = 15f;        // 청각 감지 사거리
    [Tooltip("플레이어가 시야에서 사라져도 위치를 기억하고 추적하는 시간 (초)")]
    public float visionMemoryTime = 3f;

    [Header("Combat")]
    public float attackDamage = 21f;        // 공격력
    public float attackRange = 2f;          // 공격 범위
    public float attackCooldown = 1.5f;     // 공격 쿨타임
    [Tooltip("공격 애니메이션의 실제 재생 시간")]
    public float attackAnimDuration = 2.0f;
    [Tooltip("공격 시작 후 플레이어를 따라다니며 쳐다보는 유도 시간")]
    public float attackTrackingTime = 0.7f;

    [Header("Patrol Settings")]
    [Tooltip("순찰 시 최소 대기 시간")]
    public float minWaitTime = 2f; 
    [Tooltip("순찰 시 최대 대기 시간")]
    public float maxWaitTime = 5f; 
    [Tooltip("순찰 시 목적지에 도달하지 못하고 끼어있는 최대 허용 시간")]
    public float maxPatrolMoveTime = 10.0f;
    [Tooltip("수색 상태를 유지하는 총 시간")]
    public float maxSearchDuration = 15.0f;
    [Tooltip("수색 중 한 곳에 도착한 뒤 두리번거리는 시간")]
    public float searchPauseDuration = 3.0f;

    [Header("Ai Settings")]
    [Tooltip("AI가 생각하는 주기 (기본 0.2초 = 1초에 5번 연산)")]
    public float aiTickInterval = 0.2f;
    public float fastTickInterval = 0.05f;   // 공격/상호작용 시 빠른 판단 주기
    [Tooltip("예측 추격 시간 (몇 초 앞을 예상해서 뛸 것인지)")]
    public float predictiveChaseTime = 0.8f;
    [Tooltip("경로 재탐색 기준 거리 (타겟이 이만큼 움직여야 경로를 새로 땀)")]
    public float pathUpdateThreshold = 1.0f;

    [Header("Advanced Search Settings")]
    [Tooltip("플레이어의 이동 방향을 토대로 미래 위치를 예측할 시간(초)")]
    public float predictionTime = 1.5f;
    [Tooltip("예측 지점 근처에서 수색할 Waypoint를 찾는 반경")]
    public float searchNodeRadius = 15f;

    [Header("Alertness & Optimization")]
    [Tooltip("경계도가 0에서 1까지 차오르는 속도 배율")]
    public float alertnessIncreaseRate = 1.5f;
    [Tooltip("플레이어를 놓쳤을 때 경계도가 떨어지는 속도 배율")]
    public float alertnessDecreaseRate = 0.5f;

    [Tooltip("경계도 네트워크 동기화 주기 (초)")]
    public float alertnessSyncInterval = 0.2f;
    [Tooltip("경계도 변화가 이 수치 이상일 때만 강제 동기화 (네트워크 최적화용)")]
    public float alertnessThreshold = 0.05f;

    // ----- 기믹 몬스터 설정

    [Header("Gimmick Settings")]
    [Tooltip("코일헤드처럼 특수 기믹이 플레이어 시야를 감지하는 최대 거리")]
    public float gimmickCheckDistance = 40f;

    [Header("Snare Flea Settings")]
    [Tooltip("천장을 찾기 위해 위로 레이캐스트를 쏠 최대 높이")]
    public float ceilingCheckDistance = 8f;
    [Tooltip("천장에 붙은 후, 아래로 떨어질 플레이어 감지 반경 (두께)")]
    public float dropTriggerRadius = 1.5f;
    [Tooltip("천장으로 인식할 레이어 (보통 Default나 Environment)")]
    public LayerMask ceilingLayerMask;
    [Tooltip("천장에 붙을 확률 (순찰 지점 도착 시)")]
    [Range(0f, 1f)] public float ceilingAttachChance = 0.5f;
    [Tooltip("머리에 붙어있을 때 틱 데미지가 들어가는 주기(초)")]
    public float snareTickRate = 1.5f;
    [Tooltip("1틱당 들어가는 데미지 량")]
    public float snareTickDamage = 10f;
    [Tooltip("맞아서 떨어졌을 때 패닉 상태로 도망치는 시간(초)")]
    public float fleeDuration = 6.0f;
}