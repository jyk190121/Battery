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
}