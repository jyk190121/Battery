using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayer", menuName = "Player")]
public class Player : ScriptableObject
{
    [Header("Health Settings")]
    public float maxHealth = 100f;

    [Header("Stamina Settings")]
    public float maxStamina = 50f;
    public float staminaConsumeRate = 10f;                  // 초당 소모
    public float staminaRecoverRate = 5f;                   // 초당 회복
    public float staminaExhaustedRecoverRate = 2.5f;        // 0일 때 스테미나 회복 속도
    public float recoverDelay = 2f;                         // 회복 시작 대기 시간

    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    
    public float interactDistance = 1.5f;
}
