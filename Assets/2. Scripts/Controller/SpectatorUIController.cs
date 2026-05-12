using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class SpectatorUIController : MonoBehaviour
{
    public static SpectatorUIController Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject spectatorRootCanvas; // 관전자 UI 전체 부모
    public TextMeshProUGUI nicknameText;    // 관전 대상 닉네임 텍스트
    public Image spectatorLabelImage;      // '관전자 모드' 이미지

    private void Awake()
    {
        Instance = this;
        // 시작 시에는 무조건 끈다
        if (spectatorRootCanvas != null) spectatorRootCanvas.SetActive(false);
    }

    public void ToggleUI(bool isVisible)
    {
        if (spectatorRootCanvas != null) spectatorRootCanvas.SetActive(isVisible);
    }

    public void UpdateNickname(string name)
    {
        if (nicknameText != null) nicknameText.text = name;
    }
}