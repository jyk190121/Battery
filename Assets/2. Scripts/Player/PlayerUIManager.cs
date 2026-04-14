using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIManager : NetworkBehaviour
{
    private PlayerStateManager stateManager;

    [Header("UI References")]
    public Image playerHpImage;
    public Image vignetteImage;         // 터널 시야용 이미지 (가운데가 뚫린 흐릿한 PNG 할당)
    public AudioSource breathAudio;     // 거친 숨소리

    [Header("Stamina Effect Settings")]
    [Tooltip("스태미너가 100%일 때 비네팅 이미지의 크기 (화면 밖으로 밀어내기 위해 크게 설정)")]
    public float vignetteMaxScale = 3f;
    [Tooltip("스태미너가 0%일 때 비네팅 이미지의 크기 (화면을 조여오기 위해 기본 1 정도로 설정)")]
    public float vignetteMinScale = 1f;

    private RectTransform vignetteRect;
    private bool isInitialized = false;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }

        stateManager = GetComponent<PlayerStateManager>();
        InitUI();

        if (stateManager != null)
        {
            // 1. HP 값이 바뀔 때만 호출되도록 이벤트 구독
            stateManager.currentHealth.OnValueChanged += OnHealthChanged;

            // 초기 체력 세팅 (스폰 직후 1회 갱신)
            UpdateHealthUI(0f, stateManager.currentHealth.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        // 메모리 누수를 막기 위한 이벤트 해제
        if (stateManager != null && IsOwner)
        {
            stateManager.currentHealth.OnValueChanged -= OnHealthChanged;
        }
    }

    void InitUI()
    {
        if (playerHpImage == null)
        {
            GameObject go = GameObject.Find("PlayerHpSprite");
            if (go != null) playerHpImage = go.GetComponent<Image>();
        }

        //if (vignetteImage == null)
        //{
        //    GameObject go = GameObject.Find("TestView");
        //    if (go != null) vignetteImage = go.GetComponent<Image>();
        //}

        //// 비네팅 이미지의 스케일을 조절하기 위해 RectTransform 미리 캐싱
        //if (vignetteImage != null)
        //{
        //    vignetteRect = vignetteImage.GetComponent<RectTransform>();

        //    // 알파값에 따라 생성되는 것이 아니므로, 알파값을 항상 1(최대)로 고정
        //    Color baseColor = vignetteImage.color;
        //    baseColor.a = 1f;
        //    vignetteImage.color = baseColor;
        //}

        //if (breathAudio == null)
        //{
        //    breathAudio = GetComponent<AudioSource>();
        //}

        //if (stateManager != null)
        //{
        //    isInitialized = true;
        //}
    }

    void Update()
    {
        if (!isInitialized || !IsOwner) return;

        UpdateStaminaVisuals();
        UpdateAudioFeedback();
    }

    // --- 체력 관련 (피격/회복 등 수치가 변할 때만 딱 한 번 실행됨) ---
    private void OnHealthChanged(float previousValue, float newValue)
    {
        UpdateHealthUI(previousValue, newValue);
    }

    private void UpdateHealthUI(float prevValue, float hp)
    {
        if (stateManager == null || playerHpImage == null) return;

        float r = 0f;
        float g = 0f;
        float b = 0f;
        float a = 1f; // 알파는 항상 100% (보이게)

        // Unity의 Color는 0~255가 아닌 0.0 ~ 1.0f를 사용합니다.
        if (hp >= 50f)
        {
            // 50 ~ 100 구간: G가 0에서 1(255)로 점점 증가
            g = (hp - 50f) / 50f;
            r = 0f;
        }
        else
        {
            // 0 ~ 49 구간: R이 서서히 증가
            // R이 100(%)으로 색이 변한다고 하셨으므로 최대 1.0f(255)로 맵핑했습니다.
            // (만약 유니티의 0~255 수치 중 진짜 "100"을 의미하신 거라면 r = ((50f - hp) / 50f) * (100f/255f); 로 수정하시면 됩니다.)
            r = (50f - hp) / 50f;
            g = 0f;
        }

        playerHpImage.color = new Color(r, g, b, a);

        //hp = stateManager.currentHealth.Value;
        //Color healthColor;

        //// --- 기획 수치 반영 로직 ---
        //if (hp >= 100f)
        //{
        //    healthColor = Color.white; // 양호
        //}
        //else if (hp >= 51f)
        //{
        //    healthColor = Color.yellow; // 경상
        //}
        //else if (hp >= 21f)
        //{
        //    healthColor = new Color(1f, 0.5f, 0f); // 중상 (주황색 직접 정의)
        //}
        //else if (hp >= 1f)
        //{
        //    healthColor = Color.red; // 위험
        //}
        //else
        //{
        //    healthColor = Color.gray; // 사망 시 (선택 사항)
        //}

        //// 실제 이미지 색상 변경
        //playerHpImage.color = healthColor;
    }

    // --- 스태미나 관련 (매 프레임 실행되어 자연스럽게 조여옴) ---
    private void UpdateStaminaVisuals()
    {
        if (vignetteRect == null) return;

        float staminaPercent = stateManager.CurrentStamina / stateManager.player.maxStamina;

        // 투명도(Alpha) 대신 크기(Scale)를 조절하여 주변부의 흐릿한 이미지가 몰려오는 연출
        float targetScale = Mathf.Lerp(vignetteMinScale, vignetteMaxScale, staminaPercent);
        vignetteRect.localScale = new Vector3(targetScale, targetScale, 1f);
    }

    private void UpdateAudioFeedback()
    {
        if (breathAudio == null) return;

        float staminaPercent = stateManager.CurrentStamina / stateManager.player.maxStamina;

        if (staminaPercent <= 0.5f || stateManager.IsExhausted)
        {
            if (!breathAudio.isPlaying) breathAudio.Play();

            float targetVolume = Mathf.Clamp01((0.5f - staminaPercent) * 2f);
            breathAudio.volume = targetVolume;
        }
        else
        {
            if (breathAudio.isPlaying) breathAudio.Stop();
        }
    }
}