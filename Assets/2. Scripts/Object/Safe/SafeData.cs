using UnityEngine;
using TMPro;
using Unity.Netcode; // Netcode를 사용하기 위해 추가

// 1. MonoBehaviour 대신 NetworkBehaviour를 상속받습니다.
public class SafeData : NetworkBehaviour
{
    // 2. 모두가 공유할 네트워크 변수 생성 (초기값 0)
    // 값의 변경은 서버만 가능하고, 읽기는 누구나 가능하도록 설정됩니다.
    public NetworkVariable<int> syncedPassword = new NetworkVariable<int>(0);
    public NetworkVariable<int> syncedIndex1 = new NetworkVariable<int>(0);
    public NetworkVariable<int> syncedIndex2 = new NetworkVariable<int>(0);

    public TextMeshPro[] hintList;
    public Door linkedDoor;

    // OnEnable 대신 네트워크 객체가 생성될 때 호출되는 OnNetworkSpawn을 사용합니다.
    public override void OnNetworkSpawn()
    {
        // 3. 서버(호스트)인 경우에만 단 한 번! 랜덤 값을 생성하여 네트워크 변수에 덮어씌웁니다.
        if (IsServer)
        {
            GenerateRandomData();
        }

        // 4. 값이 동기화되거나 변경될 때마다 UI에 적용하도록 이벤트를 구독합니다.
        syncedPassword.OnValueChanged += (oldValue, newValue) => ApplySafeData();

        // 5. (중요) 나중에 늦게 접속한 클라이언트(난입 플레이어)는 
        // 이미 생성된 값을 바로 적용받을 수 있도록 처리합니다.
        if (syncedPassword.Value != 0)
        {
            ApplySafeData();
        }
    }

    // 오직 서버만 실행하는 랜덤 데이터 생성 로직
    private void GenerateRandomData()
    {
        syncedPassword.Value = Random.Range(1000, 10000);

        int idx1 = Random.Range(0, hintList.Length);
        int idx2 = Random.Range(0, hintList.Length);

        while (idx1 == idx2)
        {
            idx2 = Random.Range(0, hintList.Length);
        }

        syncedIndex1.Value = idx1;
        syncedIndex2.Value = idx2;
    }

    // 동기화된 값을 바탕으로 실제로 UI 텍스트를 바꾸고 Door에 비밀번호를 세팅하는 로직
    private void ApplySafeData()
    {
        // 정수형(int)으로 저장된 패스워드를 문자열로 변환하여 사용
        string pwdStr = syncedPassword.Value.ToString();

        // 문에 동기화된 비밀번호 주입
        linkedDoor.SetPassword(pwdStr);

        // 텍스트 초기화
        for (int i = 0; i < hintList.Length; i++)
        {
            hintList[i].text = "";
        }

        string frontHint = pwdStr.Substring(0, 2);
        string backHint = pwdStr.Substring(2, 2);

        // 동기화된 인덱스 위치에 힌트 적용
        hintList[syncedIndex1.Value].text = frontHint + "XX";
        hintList[syncedIndex2.Value].text = "XX" + backHint;

        Debug.Log($"[동기화 완료] 정답 패스워드: {pwdStr} | 힌트 위치: {syncedIndex1.Value}, {syncedIndex2.Value}");
    }
}