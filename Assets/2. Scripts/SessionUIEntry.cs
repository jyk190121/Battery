using System;
using TMPro;
using Unity.Services.Multiplayer;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SessionUIEntry : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI roomNameText;      // 방 이름 (왼쪽)
    [SerializeField] private TextMeshProUGUI playerCountText;   // 인원수 (중앙)
    [SerializeField] private Button selectBtn;                 // 참여 버튼 (오른쪽 하단)

    private ISession _session;
    private Action<ISession> _onSelected;

    /// <summary>
    /// 방 정보를 받아 UI 텍스트를 갱신하고 버튼 이벤트를 연결합니다.
    /// </summary>
    public void Setup(ISession session, Action<ISession> onSelected)
    {
        _session = session;
        _onSelected = onSelected;

        roomNameText.text = session.Name;
        playerCountText.text = $"{session.Players.Count} / {session.MaxPlayers}";

        selectBtn.onClick.RemoveAllListeners();
        selectBtn.onClick.AddListener(() => _onSelected?.Invoke(_session));
    }

    private void OnJoinClicked()
    {
        Debug.Log($"{_session.Name} 방에 참가를 시도합니다.");
        MultiPlayerSessionManager.Instance.JoinSessionAsync(_session);
    }
}