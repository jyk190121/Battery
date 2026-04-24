using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

[System.Serializable]
public class PlayerStatusSlot
{
    public TextMeshProUGUI playerNameText; // 닉네임 텍스트
    public Image connectionStatusImage;    // 접속등 (초록/회색)
}

public class TabletPlayerStatusUI : MonoBehaviour
{
    [Header("Player Status UI (1~4P)")]
    public PlayerStatusSlot[] playerSlots = new PlayerStatusSlot[4];

    private void OnEnable()
    {
        // 이벤트 구독
        PlayerNameSync.OnPlayerRosterChanged += RefreshStatus;
        RefreshStatus();
    }

    private void OnDisable()
    {
        // 구독 해제
        PlayerNameSync.OnPlayerRosterChanged -= RefreshStatus;
    }

    public void RefreshStatus()
    {
        // 1. 현재 접속된 모든 플레이어 객체 찾기
        PlayerNameSync[] players = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);

        // 2. ClientId 순서로 정렬 (0, 1, 2, 3...)
        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        // 3. UI 슬롯 업데이트
        for (int i = 0; i < playerSlots.Length; i++)
        {
            var slot = playerSlots[i];

            if (i < players.Length)
            {
                var player = players[i];

                string rawNick = player.NetworkNickname.Value.ToString().Replace("\0", "").Trim();

                // 닉네임 필터링: # 뒷부분 제거
                string cleanNick = rawNick.Contains("#") ? rawNick.Split('#')[0] : rawNick;

                if(slot.playerNameText != null)
                    slot.playerNameText.text = cleanNick;

                if (slot.connectionStatusImage != null)
                    slot.connectionStatusImage.gameObject.SetActive(true);
            }
            else
            {
                slot.playerNameText.text = "Empty";

                if (slot.connectionStatusImage != null)
                    slot.connectionStatusImage.gameObject.SetActive(false);
            }
        }
    }
}