using UnityEngine;

[CreateAssetMenu(fileName = "NewMonsterData", menuName = "Monster/MonsterData")]
public class MonsterData : ScriptableObject
{
    [Header("Movement")]
    public float patrolSpeed = 3.5f;
    public float chaseSpeed = 4.5f;

    [Header("Detection")]
    public float viewRange = 12f;
    public float viewAngle = 90f;
    public float hearingRange = 15f;

    [Header("Combat")]
    public float attackDamage = 21f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    [Header("Patrol Settings")]
    [Tooltip("순찰 시 최소 대기 시간")]
    public float minWaitTime = 2f; // 최소 대기 시간
    [Tooltip("순찰 시 최대 대기 시간")]
    public float maxWaitTime = 5f; // 최대 대기 시간
    [Tooltip("순찰 시 목적지에 도달하지 못하고 끼어있는 최대 허용 시간")]
    public float maxPatrolMoveTime = 10.0f;
    [Tooltip("수색 상태를 유지하는 총 시간")]
    public float maxSearchDuration = 15.0f;
    [Tooltip("수색 중 한 곳에 도착한 뒤 두리번거리는 시간")]
    public float searchPauseDuration = 3.0f;
    [Tooltip("공격 애니메이션의 실제 재생 시간")]
    public float attackAnimDuration = 2.0f;
    [Tooltip("공격 시작 후 플레이어를 따라다니며 쳐다보는 유도 시간")]
    public float attackTrackingTime = 0.7f;

    [Header("Ai Settings")]
    [Tooltip("AI가 생각하는 주기 (기본 0.2초 = 1초에 5번 연산)")]
    public float aiTickInterval = 0.2f;
    [Tooltip("예측 추격 시간 (몇 초 앞을 예상해서 뛸 것인지)")]
    public float predictiveChaseTime = 0.8f;
    [Tooltip("경로 재탐색 기준 거리 (타겟이 이만큼 움직여야 경로를 새로 땀)")]
    public float pathUpdateThreshold = 1.0f;




}