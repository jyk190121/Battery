using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIManager : NetworkBehaviour
{
    private PlayerStateManager stateManager;

    [Header("UI References")]
    public Image vignetteImage;         // 터널 시야용 이미지 (Material의 _VignetteAmount 조절 권장)
    public Image staminaBarImage;       // 스테미너 게이지 (Fill Amount용)
    public AudioSource breathAudio;     // 거친 숨소리

    bool isInitialized = false;

    // NetworkSpawn 시점에 초기화 시도
    public override void OnNetworkSpawn()
    {
        // 내 캐릭터가 아니면 이 컴포넌트 자체를 꺼버림 (성능 및 오작동 방지)
        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }

        stateManager = GetComponent<PlayerStateManager>();

        // 씬에서 UI 요소들 찾기
        InitUI();
    }

    void InitUI()
    {
        // 1. 스테미나 모두 소진 시 이미지 보이기
        if (vignetteImage == null)
        {
            GameObject go = GameObject.Find("TestView");
            if (go != null) vignetteImage = go.GetComponent<Image>();
        }

        // 2. 스테미너 바 이미지 찾기 (새로 추가)
        if (staminaBarImage == null)
        {
            // 씬에 있는 스테미너 게이지 이미지의 이름을 확인하여 넣어주세요.
            GameObject go = GameObject.Find("TestStamina");
            if (go != null) staminaBarImage = go.GetComponent<Image>();
        }

        // 2. 오디오 소스 (플레이어 오브젝트에 붙어있다고 가정)
        if (breathAudio == null)
        {
            breathAudio = GetComponent<AudioSource>();
        }

        // 필수 참조 확인 (staminaBarImage는 없을 수도 있으니 선택적 확인)
        if (stateManager != null)
        {
            isInitialized = true;
            Debug.Log("[PlayerUIManager] 초기화 완료!");
        }
    }

    void Update()
    {
        // 초기화가 안 되었거나 내 캐릭터가 아니면 리턴
        if (!isInitialized || !IsOwner) return;

        UpdateHealthVisuals();
        UpdateStaminaVisuals();
        UpdateAudioFeedback();
    }

    private void UpdateHealthVisuals()
    {
        if (stateManager == null) return;

        float hp = stateManager.currentHealth.Value;
        Color healthColor;

        // 기획 수치 반영
        if (hp >= 100) healthColor = Color.white;
        else if (hp >= 51) healthColor = Color.yellow;
        else if (hp >= 21) healthColor = new Color(1f, 0.5f, 0f); // 주황색
        else healthColor = Color.red;

        // 예: 체력바나 텍스트가 있다면 여기서 색상 변경
        // healthBar.color = healthColor;
    }

    private void UpdateStaminaVisuals()
    {
        if (vignetteImage == null) return;

        float staminaPercent = stateManager.CurrentStamina / stateManager.player.maxStamina;

        // 스태미너가 낮을수록 투명도(Alpha)를 높임 (최대 0.8)
        float intensity = 1f - staminaPercent;
        vignetteImage.color = new Color(0, 0, 0, intensity * 0.8f);

        if (staminaBarImage != null)
        {
            // Image Type이 'Filled'로 설정되어 있어야 합니다.
            staminaBarImage.fillAmount = staminaPercent;

            // 보너스: 스테미너가 0인(Exhausted) 상태일 때 바 색상을 변경하여 경고할 수도 있습니다.
            staminaBarImage.color = stateManager.IsExhausted ? Color.red : Color.white;
        }

    }

    private void UpdateAudioFeedback()
    {
        if (breathAudio == null) return;

        float staminaPercent = stateManager.CurrentStamina / stateManager.player.maxStamina;

        // 50% 이하이거나, 지침 상태(isExhausted)에서 회복 중일 때
        if (staminaPercent <= 0.5f || stateManager.IsExhausted)
        {
            if (!breathAudio.isPlaying) breathAudio.Play();

            // 0.5~0 사이의 값을 0~1로 변환하여 볼륨 조절
            float targetVolume = Mathf.Clamp01((0.5f - staminaPercent) * 2f);
            breathAudio.volume = targetVolume;
        }
        else
        {
            if (breathAudio.isPlaying) breathAudio.Stop();
        }
    }
}