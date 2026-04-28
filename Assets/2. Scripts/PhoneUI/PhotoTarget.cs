using UnityEngine;

public class PhotoTarget : MonoBehaviour
{
    public string targetIdentifier; // 예: "RedCar", "BlueTree", "Player1"
    public int questID; //퀘스트 시스템과 연동하기 위한 고유 번호

    public int baseScore = 10;
}