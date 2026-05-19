using UnityEngine;
using Unity.Netcode;

public class DoorController : NetworkBehaviour
{
    public enum DoorType { Swing, Slide }
    public DoorType doorType;

    [Header("Room Identity")]
    public SpawnLocation roomLocation;
    public Transform questItemSpawnPoint;

    [Header("Settings")]
    public NetworkVariable<bool> isOpen = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isLocked = new NetworkVariable<bool>(false);
    public float speed = 3f;

    [Header("Swing Settings")]
    public float openAngle = 90f;

    [Header("Slide Settings")]
    public Vector3 openOffset = new Vector3(1.2f, 0, 0);

    // ==========================================
    // [추가] 사운드 관련 변수
    // ==========================================
    [Header("사운드 설정")]
    public AudioSource doorAudioSource; // 문에 달려있는 오디오 소스 (3D 설정 필수)
    public AudioClip openSound;         // 열릴 때 소리
    public AudioClip closeSound;        // 닫힐 때 소리
    public AudioClip lockedSound;       // 잠긴 문 덜컹거리는 소리 

    private Vector3 closedPos;
    private Quaternion closedRot;

    public bool CanOpenWithoutKey => !isLocked.Value;

    void Start()
    {
        closedPos = transform.localPosition;
        closedRot = transform.localRotation;
    }

    public override void OnNetworkSpawn()
    {
        // [중요] isOpen 값이 변할 때마다 OnDoorStateChanged 함수를 실행하도록 구독!
        isOpen.OnValueChanged += OnDoorStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        // 메모리 누수 방지를 위해 구독 해제
        isOpen.OnValueChanged -= OnDoorStateChanged;
    }

    // 상태가 변할 때(열리거나 닫힐 때) 모든 클라이언트에서 자동 실행되는 콜백
    private void OnDoorStateChanged(bool previousValue, bool newValue)
    {
        if (doorAudioSource == null) return;

        if (newValue == true) // 문이 열렸다!
        {
            if (openSound != null) doorAudioSource.PlayOneShot(openSound);
        }
        else // 문이 닫혔다!
        {
            if (closeSound != null) doorAudioSource.PlayOneShot(closeSound);
        }
    }

    void Update()
    {
        // 문 움직임 애니메이션 (기존과 동일)
        if (doorType == DoorType.Swing)
        {
            Quaternion targetRot = isOpen.Value ? closedRot * Quaternion.Euler(0, openAngle, 0) : closedRot;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * speed);
        }
        else
        {
            Vector3 targetPos = isOpen.Value ? closedPos + openOffset : closedPos;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * speed);
        }
    }

    public void TryOpen()
    {
        if (IsServer)
        {
            ProcessDoorLogic();
        }
        else
        {
            RequestOpenDoorServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestOpenDoorServerRpc()
    {
        ProcessDoorLogic();
    }

    private void ProcessDoorLogic()
    {
        if (isOpen.Value)
        {
            isOpen.Value = false; // -> 여기서 false가 되면 OnDoorStateChanged가 불리고 닫힘 소리 재생
            return;
        }

        if (isLocked.Value)
        {
            if (isLocked.Value)
            {
                Debug.Log("<color=red>문이 잠겨 있습니다. 맞는 열쇠가 필요합니다.</color>");

                // [추가] 잠긴 문을 열려고 시도했을 때 덜컹거리는 소리를 내고 싶다면?
                // 이건 상태가 변한 게 아니라 시도만 한 것이므로, 서버에서 ClientRpc를 쏴서 재생시킵니다.
                PlayLockedSoundClientRpc();
            }
        }
        else
        {
            isOpen.Value = true;
        }
    }

    [ClientRpc]
    private void PlayLockedSoundClientRpc()
    {
        if (doorAudioSource != null && lockedSound != null)
        {
            doorAudioSource.PlayOneShot(lockedSound);
        }
    }

    [ContextMenu("Force Unlock")] public void ForceUnlock() { if (IsServer) isLocked.Value = false; }
    [ContextMenu("Force Lock")] public void ForceLock() { if (IsServer) isLocked.Value = true; }
}