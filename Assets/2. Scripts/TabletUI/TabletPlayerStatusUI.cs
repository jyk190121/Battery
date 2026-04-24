using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using System.Linq;

[System.Serializable]
public class  PlayerStatusSlot
{
    [Tooltip("플레이어 이름을 보여줄 텍스트")]
    public TextMeshProUGUI playerNameText;

    [Tooltip("접속 상태를 나타내는 이미지")]
    public Image connectionStatusImage;
}

public class TabletPlayerStatusUI : MonoBehaviour
{
    [Header("Player Status UI(1~4)")]
    public PlayerStatusSlot[] playerSlots = new PlayerStatusSlot[4];

    private void OnEnable()
    {
        // 플레이어 목록이 변경될 때마다 UI 갱신
        PlayerNameSync.OnplayerRosterChanged += RefreshStatus;
        RefreshStatus();
    }

    private void OnDisable()
    {
        PlayerNameSync.OnplayerRosterChanged -= RefreshStatus;
    }

    public void RefreshStatus()
    {
        // 현재 씬에 존재하는 모든 PlayerNameSync 컴포넌트를 찾음
        PlayerNameSync[] players = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);

        // ClientId 기준으로 오름차순 정렬
        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        for (int i = 0; i < playerSlots.Length; i++)
        {
            var slot = playerSlots[i];
            if (i < players.Length)
            {
                var player = players[i];
                slot.playerNameText.text = player.NetworkNickname.Value.ToString().Split("#")[0];
                slot.connectionStatusImage.gameObject.SetActive(true);
            }
            else
            {
                slot.playerNameText.text = "Empty";
                slot.connectionStatusImage.gameObject.SetActive(false);
            }

        }

    }
}

