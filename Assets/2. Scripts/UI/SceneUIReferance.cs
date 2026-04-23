using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SceneUIReference : MonoBehaviour
{
    public static SceneUIReference Instance { get; private set; }

    [Header("UI 요소들")]
    public Image hpImage;
    public Image vignetteImage;
    public Image blindImage;

    [Header("마이크 UI")]
    public Image micLevelFillImage;
    public TextMeshProUGUI micLevelText;

    private void Awake()
    {
        Instance = this;
    }
}