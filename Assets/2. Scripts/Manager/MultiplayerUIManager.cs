using TMPro;
using UnityEngine;

public class MultiplayerUIManager : MonoBehaviour
{
    // 씬에 상관없이 접근 가능하도록 static 참조 (단, 씬 전환 시 갱신됨)
    public static MultiplayerUIManager Instance { get; private set; }

    [Header("Player Slots")]
    public TextMeshProUGUI[] playerSlotTexts;
    //public GameObject[] playerReadyIcons; // 준비 상태 아이콘 등

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        RefreshPlayerList();
    }

    // 씬 내의 모든 플레이어 정보를 긁어와서 UI 갱신
    public void RefreshPlayerList()
    {
        // 1. 모든 슬롯 초기화
        for (int i = 0; i < playerSlotTexts.Length; i++)
        {
            playerSlotTexts[i].text = " ";
            //if (playerReadyIcons.Length > i) playerReadyIcons[i].SetActive(false);
        }

        // 2. NetworkManager에 접속된 모든 클라이언트 확인
        // 주의: NetworkManager.Singleton.ConnectedClients는 서버에서만 유효할 때가 많으므로
        // 씬에 생성된 PlayerNameSync(NetworkBehaviour) 객체들을 찾는 것이 클라이언트 입장에서 더 확실합니다.
        PlayerNameSync[] players = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);

        // ClientID 순서대로 정렬 (입장 순서 보장)
        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        for (int i = 0; i < players.Length; i++)
        {
            if (i >= playerSlotTexts.Length) break;

            // 만약 준비 상태 변수가 있다면 여기서 처리
            // if (playerReadyIcons.Length > i) playerReadyIcons[i].SetActive(players[i].IsReady.Value);
            string nickname = players[i].NetworkNickname.Value.ToString();
            string displayName = players[i].IsOwner ? $"{nickname} (Me)" : nickname;

            SetFontSizeByLength(playerSlotTexts[i], nickname);
            playerSlotTexts[i].text = displayName;
        }
    }

    // 플레이어가 씬에 스폰될 때 호출할 함수
    public void UpdatePlayerName(int slotIndex, string name, bool isMe)
    {
        if (slotIndex < playerSlotTexts.Length)
        {
            // 폰트 사이즈 조절 로직 적용
            SetFontSizeByLength(playerSlotTexts[slotIndex], name);
            playerSlotTexts[slotIndex].text = isMe ? $"{name} (Me)" : name;
        }
    }

    void SetFontSizeByLength(TextMeshProUGUI textMesh, string originalName)
    {
        if (textMesh == null) return;

        // 기준은 (Me)가 붙지 않은 순수 닉네임 길이로 판단하는 것이 정확합니다.
        if (originalName.Length <= 8)
        {
            textMesh.fontSize = 36f;
        }
        else // 9글자 ~ 12글자
        {
            textMesh.fontSize = 20f;
        }
    }
}