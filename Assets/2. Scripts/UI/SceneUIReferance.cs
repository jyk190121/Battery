using UnityEngine;
using UnityEngine.UI;

public class SceneUIReference : MonoBehaviour
{
    public static SceneUIReference Instance { get; private set; }

    [Header("UI 요소들")]
    public Image hpImage;
    public Image vignetteImage;
    public Image blindImage;

    private void Awake()
    {
        Instance = this;
    }
}