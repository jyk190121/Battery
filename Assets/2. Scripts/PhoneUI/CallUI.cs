using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CallUI : ScrollSelectionUI
{
    [Header("References")]
    public PhotonChatManager chatManager;
    public GameObject highlight;
    public GameObject onCall;

    [Header("Phone Book (임시 목록)")]
    public List<string> phoneBookList = new List<string>();

    private readonly float padding = 100f;
    private Vector3 startPosition;

    private void Awake()
    {
        if (highlight != null) startPosition = highlight.transform.localPosition;

        //if (phoneBookList.Count == 0)
        //{
        //    phoneBookList.Add("Player1");
        //    phoneBookList.Add("Player2");
        //    phoneBookList.Add("Player3");
        //    phoneBookList.Add("Player4");
        //}
    }

    private void OnEnable()
    {
        if (onCall.activeSelf) onCall.SetActive(false);

        // 실제 입장한 플레이어 목록으로 전화번호부 갱신
        RefreshPhoneBook();

        maxIndex = Mathf.Max(0, phoneBookList.Count - 1);
        currentIndex = 0;
        UpdateHighlightVisuals();

        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed += HandleBack;
    }

    private void OnDisable()
    {
        if (PhoneUIController.Instance != null)
            PhoneUIController.Instance.OnBackButtonPressed -= HandleBack;
    }

    private void Update()
    {
        if (Mouse.current == null || highlight == null) return;

        HandleScroll();

        if (Mouse.current.leftButton.wasPressedThisFrame) StartCall();
    }

    private void HandleBack()
    {
        PhoneUIController.Instance.ShowScreen(0);
    }

    protected override void UpdateHighlightVisuals()
    {
        Vector3 newPos = startPosition;
        newPos.y -= currentIndex * padding;
        highlight.transform.localPosition = newPos;
    }

    // 전화 걸기 로직
    void StartCall()
    {
        if (phoneBookList.Count == 0) return;

        string targetPlayer = phoneBookList[currentIndex];

        if (chatManager != null)
        {
            // 채팅 서버(신호망)가 아직 준비되지 않았다면 클릭 자체를 무시합니다.
            if (!chatManager.CanChat)
            {
                SoundManager.Instance.PlaySfx(SfxSound.PHONE_ERROR);
                Debug.LogWarning("[Phone] 통신망 연결 중입니다. 1~2초 뒤에 다시 시도해주세요.");
                return; // 여기서 멈춤 (에러 방지)
            }

            if (targetPlayer == chatManager.userName)
            {
                SoundManager.Instance.PlaySfx(SfxSound.PHONE_ERROR);
                Debug.LogWarning("[Phone] 자기 자신에게는 전화를 걸 수 없습니다.");
                return;
            }

            chatManager.SendCallRequest(targetPlayer);
        }

        // 전화 걸 때 사운드 재생
        SoundManager.Instance.PlaySfx(SfxSound.PHONE_SELECT);

        OnCallingUI callingScript = onCall.GetComponent<OnCallingUI>();
        if (callingScript != null)
        {
            callingScript.StartOutgoingCall(targetPlayer);
        }
        else
        {
            onCall.SetActive(true);
        }

        gameObject.SetActive(false);
    }

    // 실제 입장한 플레이어 정보를 가져와 리스트를 채우는 함수
    private void RefreshPhoneBook()
    {
        phoneBookList.Clear();

        PlayerNameSync[] players = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);

        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        foreach (var player in players)
        {
            string nick = player.NetworkNickname.Value.ToString().Split("#")[0];

            // 목록에서 나 자신은 제외하고 싶다면 아래 주석 해제
            // if (player.IsOwner) continue; 

            if (!string.IsNullOrEmpty(nick))
            {
                phoneBookList.Add(nick);
            }
        }

        //// 만약 아무도 없다면 (테스트용)
        //if (phoneBookList.Count == 0)
        //{
        //    phoneBookList.Add("No Players Found");
        //}
    }
}