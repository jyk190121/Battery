using UnityEngine;


//아이템 세팅 Enum------------------------------------------------
public enum HandType { OneHand, TwoHand }

public enum ItemCategory
{
    Scrap,      // 폐지 (판매용)
    Consumable, // 소비형 (물약, 섬광탄 등)
    Durability, // 내구도형 (손전등 등)
    Stat,       // 스탯 영구 상승
    Weapon,     // 무기 (야구배트 등)
    Quest,      // 수집/환원 퀘스트용 (트럭에서 환전 불가)
    Phone,      // 사망자 휴대폰 (회수 패널티 계산용)
    Special     // 기타 특수 아이템
}
public enum SpawnLocation
{
    Floor1,
    Floor2,
    Floor3,
    ScienceRoom,    // 과학실
    PrincipalRoom,  // 교장실
    ArtRoom,        // 미술실
    Infirmary,      // 양호실
    MusicRoom,      // 음악실
    ShopOnly        // 상점 전용 (필드 스폰 제외)
}

public enum ItemAnimType { None, Flashlight, Battle, HeavyItem, Consumable }

//------------------------------------------------


[CreateAssetMenu(fileName = "NewItemData", menuName = "Item System/Item Data")]
public class ItemDataSO : ScriptableObject
{
    public int itemID;
    public string itemName;
    public HandType handType;
    public ItemCategory category;
    public int basePrice;
    public Sprite icon;

    [Header("Motion(기본 : None)")]
    [Tooltip("아이템 특성에 따른 포즈(예시 : HeavyItem == 양손 모션)")]
    public ItemAnimType animType = ItemAnimType.None;

    [Header("Spawn Settings")]
    [Tooltip("해당 층 또는 특정 장소에 랜덤 스폰시킴.")]
    public SpawnLocation spawnLocation = SpawnLocation.Floor1;

    [Header("스폰시 사용될 Prefab")]
    public GameObject itemPrefab;

    [Header("Key Settings")]
    [Tooltip("열쇠 아이템일 경우, 문과 일치해야 하는 ID (예: ScienceRoom_Key)")]
    public string keyID;

}
