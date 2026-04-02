using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayer", menuName = "Player")]
public class Player : ScriptableObject
{
    float HP = 100f;
    public float interactDistance = 1.5f;
}
