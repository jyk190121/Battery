using UnityEngine;

public class Item_Consumable : ItemBase
{
    public void Use()
    {
        // [핵심] 부모 클래스에 구축된 동기화 인터페이스 호출
        RequestUseItem();
    }

    // 서버 승인 후 모든 클라이언트에서 실행되는 연출/효과 로직
    public override void ExecuteUseItem()
    {
        base.ExecuteUseItem();

        Debug.Log($"{itemData.itemName}을(를) 사용했습니다!");

        // 사용 완료 후 본인(오너)이 서버에 삭제 요청
        if (IsOwner)
        {
            RequestDespawn();
        }
    }
}