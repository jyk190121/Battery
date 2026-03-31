using UnityEngine;

public class MoneyManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static MoneyManager Instance;

    [Header("Player Status")]
    public int currentMoney = 0; // 현재 보유 금액

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddMoney(int amount)
    {
        currentMoney += amount;
        Debug.Log($"<color=green>[정산 완료]</color> {amount}원 획득! (총 잔액: {currentMoney}원)");

        // TODO: UI 매니저가 있다면 여기서 금액 갱신 함수를 호출하세요.
    }
}