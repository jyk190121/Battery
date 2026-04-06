using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class PlayerStateManager : NetworkBehaviour
{
    public Player player;

    // 네트워크 변수: 체력 (서버만 수정 가능, 모두가 읽기 가능)
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>();

    // 스테미너는 본인(Owner)이 계산하고 서버에 알리거나, 로컬에서만 관리해도 무방(반응성 우선)
    float currentStamina;
    bool isExhausted = false;          // 스테미너 0 상태 여부
    float lastRunTime;
    bool isRecoveringFromZero = false; // 0 도달 후 회복 패널티 중인지 여부

    public float CurrentStamina => currentStamina;
    public bool IsExhausted => isExhausted;
    PlayerMove moveScript;
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = player.maxHealth;
        }
        currentStamina = player.maxStamina;
        moveScript = GetComponent<PlayerMove>();
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleStamina();
    }

    private void HandleStamina()
    {
        // 1. 현재 실제로 달리고 있는지 판단 (입력 + 속도 체크)
        bool isRunning = moveScript.IsMoving &&
                         UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed &&
                         !isExhausted;

        if (isRunning)
        {
            // [소모] 초당 10 소모
            currentStamina -= player.staminaConsumeRate * Time.deltaTime;
            lastRunTime = Time.time;

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                isExhausted = true;
                isRecoveringFromZero = true; // 패널티 시작
            }
        }
        else
        {
            // [회복] 회복 딜레이 2초 체크
            if (Time.time - lastRunTime >= player.recoverDelay)
            {
                // 패널티 중이면 0.5배(2.5), 아니면 일반(5) 회복
                float actualRecoverRate = isRecoveringFromZero ?
                                          player.staminaExhaustedRecoverRate :
                                          player.staminaRecoverRate;

                currentStamina += actualRecoverRate * Time.deltaTime;

                // 지침 상태 해제 (예: 최소 20%는 차야 다시 뛸 수 있음)
                if (isExhausted && currentStamina >= player.maxStamina * 0.2f)
                {
                    isExhausted = false;
                }

                // 완전히 회복되면 패널티 완전 해제
                if (currentStamina >= player.maxStamina)
                {
                    currentStamina = player.maxStamina;
                    isRecoveringFromZero = false;
                }
            }
        }

        currentStamina = Mathf.Clamp(currentStamina, 0, player.maxStamina);

      //  print($"현재 체력 : {currentHealth} , 현재 스테미너 {currentStamina}");
    }

    [ServerRpc]
    public void TakeDamageServerRpc(float damage)
    {
        currentHealth.Value -= damage;
    }
}