using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("사망 연계 설정")]
    public GameObject droppedPhonePrefab; // 바닥에 남겨질 콜라이더/ 폰 프리팹
    public GameObject playerModel;        // 플레이어 모델


    [Header("Base Data")]
    [SerializeField] private Player _playerData; // SO 데이터
    public Player Data => _playerData; // 읽기 전용 프로퍼티
    //public bool IsDead { get; private set; }
    // 네트워크 플레이어 사망 여부 체크
    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 네트워크 플레이어 상호작용 불가 체크
    public NetworkVariable<bool> isSnared = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // 네트워크 플레이어 실내/외 체크
    public NetworkVariable<bool> isInsideFacility = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 컴포넌트들을 미리 캐싱하여 다른 곳에서 쉽게 찾게 할 수도 있습니다.
    public PlayerStateManager StateManager { get; private set; }
    public PlayerInteraction Interaction { get; private set; }
    PlayerRotation playerRotation;

    // 서버와 몬스터가 즉시 참조할 수 있는 전역(Static) 출석부
    public static List<PlayerController> AllPlayers = new List<PlayerController>();
    private int _currentSpectateIndex = -1;
    private PlayerAnim playerAnim;

    private void Awake()
    {
        StateManager = GetComponent<PlayerStateManager>();
        Interaction = GetComponent<PlayerInteraction>();
        playerRotation = GetComponent<PlayerRotation>();
        playerAnim = GetComponent<PlayerAnim>();
    }

    public override void OnNetworkSpawn()
    {
        // 이전 씬의 쓰레기 데이터 정리
        AllPlayers.RemoveAll(p => p == null);

        if (!AllPlayers.Contains(this))
        {
            AllPlayers.Add(this);
            Debug.Log($"[서버 알림] 플레이어 접속: 현재 인원 {AllPlayers.Count}명");
        }

        StateManager.currentHealth.OnValueChanged += OnHealthChanged;

        // isDead 값이 바뀔 때마다 실행될 콜백 등록
        isDead.OnValueChanged += OnDeadStatusChanged;

        // 초기 상태 적용 (씬 로딩 시 이미 죽어있는 상태일 경우 대비)
        if (isDead.Value) OnDeadStatusChanged(false, true);

        if (IsServer)
        {
            //// 로비 리셋 로직
            //if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "KJY_Lobby")
            //{
            //    isDead.Value = false;
            //    StateManager.currentHealth.Value = Data.maxHealth;
            //}

            // 씬 이름에 상관없이 스폰 시점에는 살아있는 상태로 시작하게 하는 것이 안전합니다.
            isDead.Value = false;
            if (StateManager != null && Data != null)
            {
                StateManager.currentHealth.Value = Data.maxHealth;
            }
        }
        //isDead.OnValueChanged += OnDeadStatusChanged;
    }

    // 플레이어가 튕기거나 방을 나갈 때 출석부에서 제거합니다.
    public override void OnNetworkDespawn()
    {
        if (AllPlayers.Contains(this))
        {
            AllPlayers.Remove(this);
        }

        StateManager.currentHealth.OnValueChanged -= OnHealthChanged;
        isDead.OnValueChanged -= OnDeadStatusChanged;
        base.OnNetworkDespawn();
    }
    void OnHealthChanged(float oldValue, float newValue)
    {
        // 서버에서만 사망 판정
        if (IsServer)
        {
            if (!isDead.Value && newValue <= 0)
            {
                Die();
            }
        }

        // UI 업데이트는 PlayerUIManager에서 이 이벤트를 별도로 구독하고 있으므로 
        // 여기서 직접 UI를 건드릴 필요는 없으나, OnNetworkSpawn에서 구독이 잘 되었는지 확인이 필요합니다.
    }

    void Update()
    {
        if (IsOwner && isDead.Value)
        {
            HandleSpectateInput();

            CheckCurrentTargetStatus();
        }
    }
    void CheckCurrentTargetStatus()
    {
        if (playerRotation == null) return;

        // 현재 보고 있는 대상을 가져옴
        PlayerRotation currentTargetRot = playerRotation.GetSpectatingTarget();

        if (currentTargetRot != null)
        {
            // 타겟의 PlayerController 컴포넌트 확인
            if (currentTargetRot.TryGetComponent<PlayerController>(out var targetController))
            {
                // 만약 내가 보고 있는 그 사람도 죽었다면?
                if (targetController.isDead.Value)
                {
                    Debug.Log($"관전 대상 {targetController.gameObject.name}이(가) 사망하여 다음 타겟을 찾습니다.");
                    SwitchToNextTarget();
                }
            }
        }
        else
        {
            // 타겟이 null인데(예: 튕김) 아직 내가 관전 모드라면 다시 찾기 시도
            SwitchToNextTarget();
        }
    }

    void HandleSpectateInput()
    {
        // 마우스 좌클릭 시 다음 타겟으로 전환
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            SwitchToNextTarget();
        }
    }
    void SwitchToNextTarget()
    {
        if (AllPlayers.Count <= 1) return; // 나밖에 없으면 리턴

        // 현재 인덱스부터 시작해서 리스트를 한 바퀴 돌며 살아있는 플레이어 탐색
        for (int i = 0; i < AllPlayers.Count; i++)
        {
            _currentSpectateIndex = (_currentSpectateIndex + 1) % AllPlayers.Count;
            PlayerController targetPlayer = AllPlayers[_currentSpectateIndex];

            // 1. 나(본인)이 아니고 2. 죽지 않은 플레이어라면
            if (targetPlayer != this && !targetPlayer.isDead.Value)
            {
                if (targetPlayer.TryGetComponent<PlayerRotation>(out var targetRot))
                {
                    playerRotation.SetSpectatingTarget(targetRot);
                    Debug.Log($"[관전 전환] {targetPlayer.gameObject.name} 시점으로 전환");
                    return; // 찾았으므로 종료
                }
            }
        }
    }

    // [ServerRpc] 외부(몬스터 등)에서 데미지를 줄 때 호출
    //[ServerRpc(RequireOwnership = false)]
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TakeDamageServerRpc(float damage)
    {
        if (isDead.Value) return;

        StateManager.currentHealth.Value -= damage;

        //if (StateManager.currentHealth.Value <= 0)
        //{
        //    StateManager.currentHealth.Value = 0;
        //    Die();
        //}
    }

    private void Die()
    {
        if (!IsServer) return;

        isDead.Value = true;
        Debug.Log($"{gameObject.name}가 사망했습니다.");

        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        //if (AllPlayers.Contains(this))
        //{
        //    AllPlayers.Remove(this);
        //}

        //// 1. 내 모델을 끈다 (내 화면에서 내 몸이 안 보이게)
        //if (playerModel != null) playerModel.SetActive(false);

        // 2. 관전 모드 활성화 (회전 스크립트에 알림)
        if (playerRotation != null) playerRotation.SetSpectatingMode(true);

        // 2. 관전 대상 탐색 및 카메라 전환 로직 실행
        //StartCoroutine(StartSpectating());
        //if (IsOwner)
        //{
        //    // 이미 실행 중인 관전 루틴이 있다면 정지 (중복 방지)
        //    StopAllCoroutines();
        //    StartCoroutine(StartSpectating());
        //}

        if (TryGetComponent(out PlayerInventory inventory))
        {
            inventory.DropAllItemsOnDeathServer();
        }

        SpawnDroppedPhoneClientRpc(transform.position, transform.rotation);

        // 사망 애니메이션 실행, 콜라이더 끄기, 리스폰 로직 등 처리
        CheckAllPlayersDead();

       
    }

    [ClientRpc]
    private void SpawnDroppedPhoneClientRpc(Vector3 pos, Quaternion rot)
    {
        // PlayerEquipment에 구현된 기능을 활용하거나 별도의 프리팹을 생성
        if (TryGetComponent(out PlayerEquipment equip))
        {
            // 현재 들고 있는 폰이 있다면 파괴하고 바닥용 모델 생성
            equip.DestroySmartPhoneModel();
        }

        if (IsServer)
        {

            // 바닥에 떨어질 별도의 'DroppedPhone' 프리팹이 있다면 Instantiate
            // (간단하게 하려면 기존 smartphoneModel을 부모 없이 생성)
            if (droppedPhonePrefab != null)
            {
                // 바닥에 파묻히지 않게 위치만 살짝 보정
                Vector3 spawnPos = pos + Vector3.up * 0.1f;


                GameObject dropped = Instantiate(droppedPhonePrefab, pos, rot);

                // 3. 아이템 주인 정보 기록 (사망한 플레이어의 ID)
                if (dropped.TryGetComponent(out Item_Phone phoneItem))
                {
                    phoneItem.originalOwnerId = OwnerClientId;
                }

                // 네트워크 스폰
                dropped.GetComponent<NetworkObject>().Spawn();

            }
            // 바닥에 놓인 느낌을 주기 위해 물리(Rigidbody)나 콜라이더를 켜주는 로직이 필요할 수 있습니다.
        }
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
        yield return new WaitForSeconds(2f);

        if (IsServer)
        {
            // 1. 모든 몬스터 정리 (로비 가기 전 서버에서 실행)
            MonsterController[] remainingMonsters = FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
            foreach (var monster in remainingMonsters)
            {
                if (monster.NetworkObject != null && monster.NetworkObject.IsSpawned)
                {
                    monster.NetworkObject.Despawn(); // 서버에서 디스폰하면 모든 클라에서 사라짐
                }
            }

            for (int i = AllPlayers.Count - 1; i >= 0; i--)
            {
                if (AllPlayers[i] != null)
                {
                    AllPlayers[i].RevivePlayer();
                }
            }

            // 2. 모든 플레이어 부활 처리
            //foreach (var player in AllPlayers)
            //{
            //    player.RevivePlayer();
            //}

            var playersToRevive = new List<PlayerController>(AllPlayers);
            foreach (var player in playersToRevive)
            {
                player.RevivePlayer();
            }

            GameSessionManager.Instance.CleanupAllItemsInScene();
            yield return new WaitForSeconds(1f);

            // 3. 로비 씬으로 이동
            GameSceneManager.Instance.LoadNetworkScene("KJY_Lobby");
        }
        //// 모든 플레이어 부활 처리 (로비 가기 전 데이터 세팅)
        //foreach (var player in AllPlayers)
        //{
        //    player.RevivePlayer();
        //}
        //// 로비 씬으로 이동 (NetworkSceneManager 사용 권장)
        ////NetworkManager.SceneManager.LoadScene("KJY_Lobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        //GameSceneManager.Instance.LoadNetworkScene("KJY_Lobby");
    }

    public void RevivePlayer()
    {
        if (!IsServer) return;
        isDead.Value = false;

        gameObject.layer = LayerMask.NameToLayer("Player");

        //// 4. [추가] 부활 시 다시 추적 대상 리스트에 추가
        //if (!AllPlayers.Contains(this))
        //{
        //    AllPlayers.Add(this);
        //    Debug.Log($"[서버] {gameObject.name}가 추적 대상 리스트에 다시 추가됨.");
        //}

        //StateManager.ResetStatus();
        if (StateManager != null)
        {
            StateManager.currentHealth.Value = Data.maxHealth; // 명시적으로 HP 풀로 채움
            StateManager.ResetStatus();
        }
        ReviveClientRpc();
    }

    [ClientRpc]
    private void ReviveClientRpc()
    {
        // OnDeadStatusChanged가 false일 때 처리를 하지만, 
        // 여기서 한번 더 확실하게 관전 코루틴을 중지하고 카메라를 복구합니다.
        StopAllCoroutines();
        if (IsOwner && playerRotation != null)
        {
            playerRotation.SetSpectatingMode(false);
            playerRotation.SetSpectatingTarget(null);
        }
    }

    // 사망 상태가 변했을 때 호출되는 함수

    void OnDeadStatusChanged(bool previousValue, bool newValue)
    {
        if (playerAnim == null) playerAnim = GetComponent<PlayerAnim>();

        if (newValue) // newValue가 true이면 사망
        {
            playerAnim.PlayDead();
            PerformDeathEffects();

            StartCoroutine(HideModelAfterDelay(1.5f));

            // 관전 모드 시작 (본인인 경우에만)
            if (IsOwner)
            {
                StartCoroutine(StartSpectating());
            }
        }
        else
        {
            StopAllCoroutines();

            if (playerModel != null) playerModel.SetActive(true);

            playerAnim.PlayRevive();
            playerAnim.ResetAnimation();
            PerformReviveEffects();
        }
    }
    IEnumerator HideModelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (playerModel != null) playerModel.SetActive(false);
    }


    IEnumerator StartSpectating()
    {
        yield return new WaitForSeconds(1.5f); // 사망 애니메이션을 조금 본 뒤 전환

        _currentSpectateIndex = -1; // 초기화하여 리스트 처음부터 찾게 함
        SwitchToNextTarget();

        //// 살아있는 다른 플레이어 찾기
        //PlayerRotation target = FindSpectatableTarget();
        //if (target != null)
        //{
        //    // PlayerRotation에 새로 만든 동기화 함수 호출
        //    playerRotation.SetSpectatingTarget(target);
        //    Debug.Log($"[관전] {target.gameObject.name} 시점으로 전환합니다.");
        //}

        PlayerRotation target = playerRotation.GetSpectatingTarget();

        if (target != null)
        {
            Debug.Log($"[관전 시작] 첫 타겟 {target.gameObject.name} 시점 즉시 동기화");

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
            if (target != null)
            {
                if (target.TryGetComponent<PlayerRotation>(out var targetRot) &&
                    TryGetComponent<PlayerRotation>(out var myRot))
                {
                    if (myRot.vcam != null)
                    {
                        // [추가] 내 카메라의 마우스 회전 입력을 끄고 정렬함
                        myRot.SetSpectatingMode(true);

                        myRot.vcam.Follow = targetRot.cameraTarget;
                        // LookAt을 빼버리면 카메라가 Follow 대상의 회전(시야 방향)을 그대로 따릅니다.
                        myRot.vcam.LookAt = null;

                        Debug.Log($"{target.gameObject.name}의 시점을 관전합니다.");
                    }
                    myRot.vcam.ForceCameraPosition(targetRot.cameraTarget.position, targetRot.cameraTarget.rotation);
                }

            }
        }

        yield return new WaitForSeconds(1.0f);
    }

    //PlayerRotation FindSpectatableTarget()
    //{
    //    //foreach (var p in AllPlayers)
    //    //{
    //    //    // 내가 아니고 죽지 않은 플레이어
    //    //    if (p != this && !p.isDead.Value)
    //    //    {
    //    //        if (p.TryGetComponent<PlayerRotation>(out var targetRot)) return targetRot;
    //    //    }
    //    //}

    //    // 살아있는 다른 플레이어 찾기
    //    foreach (var player in AllPlayers) // static List 등으로 관리되는 리스트
    //    {
    //        //if (player != this && !player.isDead.Value)
    //        //{
    //        //    GetComponent<PlayerRotation>().SetSpectatingTarget(player.GetComponent<PlayerRotation>());
    //        //    break;
    //        //}
    //        // 1. 내가 아니고 2. 리스트에 유효하며 3. 살아있는 플레이어 탐색
    //        if (player != null && player != this && !player.isDead.Value)
    //        {
    //            if (player.TryGetComponent<PlayerRotation>(out var targetRot))
    //                return targetRot;
    //        }
    //    }
    //    return null;
    //}


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
        //if (TryGetComponent(out PlayerRotation rot)) rot.enabled = false;
        if (TryGetComponent(out PlayerInteraction interact)) interact.enabled = false;
        if (TryGetComponent(out PlayerEquipment equip)) equip.enabled = false;

        if (IsOwner)
        {
            // 몬스터가 사망한 플레이어를 찾지 않도록 레이어 수정
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            // 이 아래에서 PlayerRotation의 카메라 눕히기 로직이 호출된다면 
            // 오직 죽은 당사자의 화면에서만 카메라가 돌아갑니다.
        }
    }

    void PerformReviveEffects()
    {
        Debug.Log($"{gameObject.name}가 부활");

        if (playerModel != null) playerModel.SetActive(true);

        // 1. 애니메이션 리셋 (누워있는 상태에서 일어나는 상태로)
        if (TryGetComponent(out PlayerAnim anim))
        {
            anim.ResetAnimation();
        }

        // 2. 물리 및 충돌체 다시 켜기
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        { 
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
        }

        // 3. 레이어 복구 (몬스터가 다시 감지할 수 있게)
        gameObject.layer = LayerMask.NameToLayer("Player");

        // 4. 모든 조작 스크립트 재활성화
        if (TryGetComponent(out PlayerMove move)) move.enabled = true;
        if (TryGetComponent(out PlayerRotation rot))
        {
            //rot.enabled = true;

            if (IsOwner)
            {
                // 1. 관전 모드 종료
                rot.SetSpectatingMode(false);
                // 2. 카메라 타겟을 본인으로 초기화 (null 전달 시 본인 타겟으로 복구됨)
                rot.SetSpectatingTarget(null);
            }

            // 중요: 관전 중이었다면 카메라 타겟을 다시 나(본인)로 돌려놓아야 함
            //if (IsOwner && rot.vcam != null)
            //{
            //    rot.SetSpectatingMode(false);

            //    rot.vcam.Follow = rot.cameraTarget;
            //    rot.vcam.LookAt = null;

            //    // 부활 시 카메라 회전값을 현재 내 몸의 정면으로 초기화 (선택 사항)
            //    //rot.vcam.transform.rotation = transform.rotation;
            //}
        }
        if (TryGetComponent(out PlayerInteraction interact)) interact.enabled = true;
        if (TryGetComponent(out PlayerEquipment equip)) equip.enabled = true;

      
        //// 5. 본인인 경우 UI 및 커서 복구
        //if (IsOwner)
        //{
        //    Cursor.lockState = CursorLockMode.Locked;
        //    Cursor.visible = false;
        //    // "사망" UI가 있었다면 여기서 끄기
        //}
    }

    [ClientRpc]
    public void TeleportToSpawnClientRpc(Vector3 position, Quaternion rotation)
    {
        // Owner(본인)만 본인의 위치를 제어할 수 있는 권한이 있음
        if (IsOwner)
        {
            if (TryGetComponent(out Unity.Netcode.Components.NetworkTransform nt))
            {
                // 본인이 호출하므로 에러가 발생하지 않음
                nt.Teleport(position, rotation, transform.localScale);
            }
            else
            {
                // NetworkTransform이 없다면 일반 transform 수정
                transform.position = position;
                transform.rotation = rotation;
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReportNoiseServerRpc(Vector3 noisePos, float noiseLevel, bool isInside)
    {
        if (EnemyManager.Instance == null) return;

        foreach (var scanner in EnemyManager.Instance.ActiveScanners)
        {
            if (scanner != null)
            {
                // 소리의 크기뿐만 아니라, 소리가 발생한 장소(실내/실외) 정보도 몬스터에게 넘겨줍니다
                scanner.OnHeardSound(noisePos, noiseLevel, isInside);
            }
        }
    }

    [ServerRpc]
    public void SetInsideFacilityServerRpc(bool value)
    {
        // 서버 권한으로 값을 변경합니다.
        isInsideFacility.Value = value;
    }
}