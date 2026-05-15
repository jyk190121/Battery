using UnityEngine;

public class PhotoTarget : MonoBehaviour
{
    [Header("식별자 (Monster, Player, Item)")]
    public string targetIdentifier;

    [Header("퀘스트 직접 연동 시 ID (필요없으면 0)")]
    public int questID = 0;
}