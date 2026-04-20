using UnityEngine;

/// <summary>
/// 몬스터의 스탯, 생성 비용, AI 설정 및 특수 기믹 수치를 정의하는 통합 데이터 에셋입니다.
/// </summary>
[CreateAssetMenu(fileName = "NewMonsterData", menuName = "Monster/MonsterData")]
public class MonsterData : ScriptableObject
{
    // =========================================================
    // 1. 기본 스탯 및 스폰 (Core & Spawn)
    // =========================================================
    [Header("--- Core & Spawn Settings ---")]

    [Tooltip("이 몬스터의 실제 프리팹 (EnemyManager가 생성할 때 사용)")]
    public GameObject monsterPrefab;

    [Tooltip("몬스터의 기본 최대 체력")]
    [Min(1f)] public float maxHealth = 100f;

    [Space(5)]
    [Tooltip("이 몬스터가 스폰될 때 소모하는 스테이지 예산(점수)")]
    [Min(1)] public int spawnCost = 2;

    [Tooltip("맵에 동시에 존재할 수 있는 최대 마리 수")]
    [Min(1)] public int maxSpawnCount = 2;

    [Tooltip("스폰 확률 가중치 (값이 높을수록 자주 등장함)")]
    [Range(0f, 100f)] public float spawnWeight = 50f;


    // =========================================================
    // 2. 이동 및 전투 (Movement & Combat)
    // =========================================================
    [Space(10)]
    [Header("--- Movement & Combat ---")]

    [Min(0f)] public float patrolSpeed = 3.5f; // 순찰 속도
    [Min(0f)] public float chaseSpeed = 4.5f; // 추격 속도

    [Space(5)]
    [Min(0f)] public float attackDamage = 21f;  // 공격력
    [Min(0f)] public float attackRange = 2f;   // 공격 범위
    [Min(0f)] public float attackCooldown = 1.5f; // 공격 쿨타임

    [Tooltip("공격 애니메이션의 실제 재생 시간")]
    [Min(0f)] public float attackAnimDuration = 2.0f;

    [Tooltip("공격 시작 후 플레이어를 따라다니며 쳐다보는 유도 시간")]
    [Min(0f)] public float attackTrackingTime = 0.7f;


    // =========================================================
    // 3. 감각 및 수색 (Detection & Search)
    // =========================================================
    [Space(10)]
    [Header("--- Detection & Search ---")]

    [Min(0f)] public float viewRange = 12f;  // 시야 사거리
    [Range(0f, 360f)] public float viewAngle = 90f;  // 시야 각도
    [Min(0f)] public float hearingRange = 15f;  // 청각 감지 사거리

    [Tooltip("플레이어가 시야에서 사라져도 위치를 기억하고 추적하는 시간(초)")]
    [Min(0f)] public float visionMemoryTime = 3f;

    [Space(5)]
    [Tooltip("수색 상태를 유지하는 총 시간")]
    [Min(0f)] public float maxSearchDuration = 15.0f;

    [Tooltip("수색 중 한 곳에 도착한 뒤 두리번거리는 시간")]
    [Min(0f)] public float searchPauseDuration = 3.0f;

    [Tooltip("예측 지점 근처에서 수색할 Waypoint를 찾는 반경")]
    [Min(0f)] public float searchNodeRadius = 15f;


    // =========================================================
    // 4. AI 사고 및 행동 패턴 (AI & Behavior)
    // =========================================================
    [Space(10)]
    [Header("--- AI & Behavior ---")]

    [Tooltip("순찰 시 최소 대기 시간")]
    [Min(0f)] public float minWaitTime = 2f;

    [Tooltip("순찰 시 최대 대기 시간")]
    [Min(0f)] public float maxWaitTime = 5f;

    [Tooltip("순찰 시 목적지에 도달하지 못하고 끼어있는 최대 허용 시간")]
    [Min(0f)] public float maxPatrolMoveTime = 10.0f;

    [Space(5)]
    [Tooltip("AI가 생각하는 주기 (기본 0.2초 = 1초에 5번 연산)")]
    [Min(0.01f)] public float aiTickInterval = 0.2f;

    [Tooltip("공격/상호작용 시 빠른 판단 주기")]
    [Min(0.01f)] public float fastTickInterval = 0.05f;

    [Tooltip("예측 추격 시간 (몇 초 앞을 예상해서 뛸 것인지)")]
    [Min(0f)] public float predictiveChaseTime = 0.8f;

    [Tooltip("플레이어의 이동 방향을 토대로 미래 위치를 예측할 시간(초)")]
    [Min(0f)] public float predictionTime = 1.5f;

    [Tooltip("경로 재탐색 기준 거리 (타겟이 이만큼 움직여야 경로를 새로 땀)")]
    [Min(0f)] public float pathUpdateThreshold = 1.0f;

    [Tooltip("순찰 중 문을 만났을 때 열고 지나갈 확률 (0.0 ~ 1.0)")]
    public float patrolDoorOpenChance = 0.2f;

    [Tooltip("문을 열지 않기로 결정했을 때, 다른 행동을 하기 전까지의 쿨타임 (초)")]
    public float doorIgnoreCooldown = 1.0f;

    [Tooltip("순찰 시 다음 목적지를 정할 때 요구되는 최소 이동 거리 (m)")]
    public float minPatrolDistance = 10f;


    // =========================================================
    // 5. 시스템 및 네트워크 최적화 (System & Network)
    // =========================================================
    [Space(10)]
    [Header("--- System & Network Optimization ---")]

    [Tooltip("경계도가 0에서 1까지 차오르는 속도 배율")]
    [Min(0f)] public float alertnessIncreaseRate = 1.5f;

    [Tooltip("플레이어를 놓쳤을 때 경계도가 떨어지는 속도 배율")]
    [Min(0f)] public float alertnessDecreaseRate = 0.5f;

    [Space(5)]
    [Tooltip("경계도 네트워크 동기화 주기 (초)")]
    [Min(0.01f)] public float alertnessSyncInterval = 0.2f;

    [Tooltip("경계도 변화가 이 수치 이상일 때만 강제 동기화 (네트워크 최적화용)")]
    [Min(0f)] public float alertnessThreshold = 0.05f;

    [Space(5)]
    [Tooltip("스턴 지속시간 배율 (1.0 = 정상, 0.5 = 기절 시간 절반, 0 = 면역)")]
    [Min(0f)] public float stunDurationMultiplier = 1.0f;


    // =========================================================
    // 6. 특수 기믹 (Gimmick Settings)
    // =========================================================
    [Space(10)]
    [Header("--- Gimmick: General ---")]
    [Tooltip("코일헤드처럼 특수 기믹이 플레이어 시야를 감지하는 최대 거리")]
    [Min(0f)] public float gimmickCheckDistance = 40f;

    [Space(10)]
    [Header("--- Gimmick: Snare Flea ---")]
    [Tooltip("천장에 붙을 확률 (순찰 지점 도착 시)")]
    [Range(0f, 1f)] public float ceilingAttachChance = 0.5f;

    [Tooltip("천장으로 인식할 레이어 (보통 Default나 Environment)")]
    public LayerMask ceilingLayerMask;

    [Tooltip("천장을 찾기 위해 위로 레이캐스트를 쏠 최대 높이")]
    [Min(0f)] public float ceilingCheckDistance = 8f;

    [Tooltip("천장에 붙은 후, 아래로 떨어질 플레이어 감지 반경 (두께)")]
    [Min(0f)] public float dropTriggerRadius = 1.5f;

    [Tooltip("머리에 붙어있을 때 틱 데미지가 들어가는 주기(초)")]
    [Min(0f)] public float snareTickRate = 1.5f;

    [Tooltip("1틱당 들어가는 데미지 량")]
    [Min(0f)] public float snareTickDamage = 10f;

    [Tooltip("맞아서 떨어졌을 때 패닉 상태로 도망치는 시간(초)")]
    [Min(0f)] public float fleeDuration = 6.0f;

    [Tooltip("올무벼룩이 순찰 도중 천장을 올려다보는 간격 (초)")]
    public float fleaCeilingCheckInterval = 5.0f;
}