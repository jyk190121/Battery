using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Multiplayer;

public class SessionUIEntry : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI roomNameText;      // 방 이름 (왼쪽)
    [SerializeField] private TextMeshProUGUI playerCountText;   // 인원수 (중앙)
    [SerializeField] private Button joinButton;                 // 참여 버튼 (오른쪽 하단)
    [SerializeField] private Button resetButton;                // 방 리스트 리프레시 버튼 (왼쪽 하단)


    private ISession _session;

    /// <summary>
    /// 방 정보를 받아 UI 텍스트를 갱신하고 버튼 이벤트를 연결합니다.
    /// </summary>
    public void Setup(ISession session)
    {
        _session = session;

        // 텍스트 설정
        roomNameText.text = session.Name;
        playerCountText.text = $"{session.Players.Count} / {session.MaxPlayers}";

        // 버튼 이벤트 초기화 및 할당
        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnJoinClicked()
    {
        Debug.Log($"{_session.Name} 방에 참가를 시도합니다.");
        MultiPlayerSessionManager.Instance.JoinSessionAsync(_session);
    }
}