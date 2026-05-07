using System;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

// 💡 논리적 차단 조건
// 1. 이미 서비스에서 Locked로 판단한 경우
// 2. 인원수가 최대치에 도달한 경우 (AvailableSlots == 0)
// 3. (옵션) Properties에 GameState가 "1"인 경우

public class SessionUIEntry : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI roomNameText;      // 방 이름 (왼쪽)
    [SerializeField] private TextMeshProUGUI playerCountText;   // 인원수 (중앙)
    [SerializeField] private Button selectBtn;                 // 참여 버튼 (오른쪽 하단)

    private ISessionInfo _session;
    private Action<ISessionInfo> _onSelected;

    /// <summary>
    /// 방 정보를 받아 UI 텍스트를 갱신하고 버튼 이벤트를 연결합니다.
    /// </summary>
    public void Setup(ISessionInfo session, Action<ISessionInfo> onSelected)
    {
        _session = session;
        _onSelected = onSelected;
        roomNameText.text = session.Name;

        // 💡 2.1.3 버전에서는 세션이 비공개(IsPrivate)가 되면 리스트에서 아예 사라지지만,
        // 사라지기 전 찰나에 잡히는 경우를 위해 조건을 강화합니다.
        bool isStarted = session.IsLocked;

        if (isStarted || session.AvailableSlots == 0)
        {
            playerCountText.text = "<color=red>게임중</color>";
            selectBtn.interactable = false;
        }
        else
        {
            int currentPlayers = session.MaxPlayers - session.AvailableSlots;
            playerCountText.text = $"{currentPlayers} / {session.MaxPlayers}";
            selectBtn.interactable = true;
        }

        selectBtn.onClick.RemoveAllListeners();
        selectBtn.onClick.AddListener(() => _onSelected?.Invoke(_session));
    }

    private void OnJoinClicked()
    {
        Debug.Log($"{_session.Name} 방에 참가를 시도합니다.");
        MultiPlayerSessionManager.Instance.JoinSessionAsync(_session);
    }
}