using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Base Data")]
    [SerializeField] private Player _playerData; // SO 데이터
    public Player Data => _playerData; // 읽기 전용 프로퍼티
    //public bool IsDead { get; private set; }
    // 네트워크 플레이어 사망 여부 체크
    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 컴포넌트들을 미리 캐싱하여 다른 곳에서 쉽게 찾게 할 수도 있습니다.
    public PlayerStateManager StateManager { get; private set; }
    public PlayerInteraction Interaction { get; private set; }

    // 서버와 몬스터가 즉시 참조할 수 있는 전역(Static) 출석부
    public static List<PlayerController> AllPlayers = new List<PlayerController>();

    private void Awake()
    {
        StateManager = GetComponent<PlayerStateManager>();
        Interaction = GetComponent<PlayerInteraction>();
    }

    public override void OnNetworkSpawn()
    {
        if (!AllPlayers.Contains(this))
        {
            AllPlayers.Add(this);
            Debug.Log($"[서버 알림] 플레이어 접속: 현재 인원 {AllPlayers.Count}명");
        }
        isDead.OnValueChanged += OnDeadStatusChanged;
    }

    // 플레이어가 튕기거나 방을 나갈 때 출석부에서 제거합니다.
    public override void OnNetworkDespawn()
    {
        if (AllPlayers.Contains(this))
        {
            AllPlayers.Remove(this);
            Debug.Log($"[서버 알림] 플레이어 퇴장: 남은 인원 {AllPlayers.Count}명");
        }

        base.OnNetworkDespawn();
    }

    // [ServerRpc] 외부(몬스터 등)에서 데미지를 줄 때 호출
    [ServerRpc]
    public void TakeDamageServerRpc(float damage)
    {
        if (isDead.Value) return;

        StateManager.currentHealth.Value -= damage;

        if (StateManager.currentHealth.Value <= 0)
        {
            StateManager.currentHealth.Value = 0;
            Die();
        }
    }

    private void Die()
    {
        isDead.Value = true;
        Debug.Log($"{gameObject.name}가 사망했습니다.");

        // 사망 애니메이션 실행, 콜라이더 끄기, 리스폰 로직 등 처리
        CheckAllPlayersDead();
    }


    void CheckAllPlayersDead()
    {
        if (!IsServer) return;

        bool areAllDead = true;
        foreach (var player in AllPlayers)
        {
            if (!player.isDead.Value)
            {
                areAllDead = false;
                break;
            }
        }

        if (areAllDead)
        {
            Debug.Log("모든 플레이어 사망. 3초 후 로비로 이동합니다.");
            StartCoroutine(ReturnToLobbyWithDelay());
        }
    }
    IEnumerator ReturnToLobbyWithDelay()
    {
        yield return new WaitForSeconds(3f);
        // 모든 플레이어 부활 처리 (로비 가기 전 데이터 세팅)
        foreach (var player in AllPlayers)
        {
            player.RevivePlayer();
        }
        // 로비 씬으로 이동 (NetworkSceneManager 사용 권장)
        //NetworkManager.SceneManager.LoadScene("KJY_Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        GameSceneManager.Instance.LoadNetworkScene("KJY_Lobby");
    }

    public void RevivePlayer()
    {
        if (!IsServer) return;
        isDead.Value = false;
        StateManager.ResetStatus(); // 체력 등 초기화
    }
    
    // 사망 상태가 변했을 때 호출되는 함수

    void OnDeadStatusChanged(bool previousValue, bool newValue)
    {
        if (newValue == true)
        {
            PerformDeathEffects();

            // 관전 모드 시작 (본인인 경우에만)
            if (IsOwner)
            {
                StartCoroutine(StartSpectating());
            }
        }
        else // 부활 시 (isDead.Value가 true -> false가 되었을 때)
        {
            PerformReviveEffects();
        }
    }

    IEnumerator StartSpectating()
    {
        yield return new WaitForSeconds(2.0f); // 사망 애니메이션을 조금 본 뒤 전환

        // 살아있는 다른 플레이어 찾기
        PlayerController targetPlayer = null;
        foreach (var p in AllPlayers)
        {
            if (p != this && !p.isDead.Value)
            {
                targetPlayer = p;
                break;
            }
        }

        if (targetPlayer != null)
        {
            //if (targetPlayer.TryGetComponent<PlayerRotation>(out var targetRot) &&
            //    TryGetComponent<PlayerRotation>(out var myRot))
            //{
            //    if (myRot.vcam != null)
            //    {
            //        myRot.vcam.Follow = targetRot.cameraTarget;
            //        myRot.vcam.LookAt = targetRot.cameraTarget;
            //        Debug.Log($"{targetPlayer.gameObject.name}의 시점을 관전합니다.");
            //    }
            //}

            if (targetPlayer != null)
            {
                if (targetPlayer.TryGetComponent<PlayerRotation>(out var targetRot) &&
                    TryGetComponent<PlayerRotation>(out var myRot))
                {
                    if (myRot.vcam != null)
                    {
                        // [추가] 내 카메라의 마우스 회전 입력을 끄고 정렬함
                        myRot.SetSpectatingMode(true);

                        myRot.vcam.Follow = targetRot.cameraTarget;
                        // LookAt을 빼버리면 카메라가 Follow 대상의 회전(시야 방향)을 그대로 따릅니다.
                        myRot.vcam.LookAt = null;

                        Debug.Log($"{targetPlayer.gameObject.name}의 시점을 관전합니다.");
                    }
                }
            }
        }
    }


    void PerformDeathEffects()
    {
        // 1. 사망 애니메이션 실행
        if (GetComponent<PlayerAnim>() != null)
        {
            GetComponent<PlayerAnim>().PlayDead();
        }

        // 2. 물리 및 충돌체 비활성화
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true; // 물리 엔진 영향 중단

        // 3. 조작 스크립트들 비활성화
        if (TryGetComponent(out PlayerMove move)) move.enabled = false;
        if (TryGetComponent(out PlayerRotation rot)) rot.enabled = false;
        if (TryGetComponent(out PlayerInteraction interact)) interact.enabled = false;
        if (TryGetComponent(out PlayerEquipment equip)) equip.enabled = false;

        // 몬스터가 사망한 플레이어 찾지않도록 레이어 수정
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        // 4. (본인인 경우) 마우스 커서 잠금 해제 및 UI 처리
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // 여기에 "사망하셨습니다" 같은 UI 띄우기 가능
        }
    }

    void PerformReviveEffects()
    {
        Debug.Log($"{gameObject.name}가 부활");

        // 1. 애니메이션 리셋 (누워있는 상태에서 일어나는 상태로)
        if (TryGetComponent(out PlayerAnim anim))
        {
            anim.ResetAnimation();
        }

        // 2. 물리 및 충돌체 다시 켜기
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false; // 물리 다시 적용

        // 3. 레이어 복구 (몬스터가 다시 감지할 수 있게)
        gameObject.layer = LayerMask.NameToLayer("Player");

        // 4. 모든 조작 스크립트 재활성화
        if (TryGetComponent(out PlayerMove move)) move.enabled = true;
        if (TryGetComponent(out PlayerRotation rot))
        {
            rot.enabled = true;
            // 중요: 관전 중이었다면 카메라 타겟을 다시 나(본인)로 돌려놓아야 함
            if (IsOwner && rot.vcam != null)
            {
                rot.SetSpectatingMode(false);

                rot.vcam.Follow = rot.cameraTarget;
                rot.vcam.LookAt = null;

                // 부활 시 카메라 회전값을 현재 내 몸의 정면으로 초기화 (선택 사항)
                //rot.vcam.transform.rotation = transform.rotation;
            }
        }
        if (TryGetComponent(out PlayerInteraction interact)) interact.enabled = true;
        if (TryGetComponent(out PlayerEquipment equip)) equip.enabled = true;

        // 5. 본인인 경우 UI 및 커서 복구
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // "사망" UI가 있었다면 여기서 끄기
        }
    }
}