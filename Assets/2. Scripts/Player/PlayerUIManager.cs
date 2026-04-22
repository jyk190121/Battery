using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUIManager : NetworkBehaviour
{
    private PlayerStateManager stateManager;

    [Header("UI References")]
    public Image playerHpImage;
    public Image vignetteImage;         // 터널 시야용 이미지
    public AudioSource breathAudio;     // 거친 숨소리

    [Header("Stamina Effect Settings")]
    [Tooltip("스태미너가 100%일 때 크기 (화면 밖으로 구멍이 완전히 빠져나가도록 무식하게 큼)")]
    public float vignetteMaxScale = 45f;
    [Tooltip("스태미너가 0%일 때 크기 (화면 양옆이 잘리지 않는 마지노선)")]
    public float vignetteMinScale = 25f;

    [Header("Gimmick UI")]
    public Image blindImage;

    private RectTransform vignetteRect;
    private bool isInitialized = false;

    public static PlayerUIManager LocalInstance { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }
        LocalInstance = this;

        stateManager = GetComponent<PlayerStateManager>();
        //InitUI();
        RefreshUIReferences();

        if (stateManager != null)
        {
            //stateManager.currentHealth.OnValueChanged += OnHealthChanged;
            //UpdateHealthUI(0f, stateManager.currentHealth.Value);

            UpdateHealthUI(0f, stateManager.currentHealth.Value);

            // 이벤트 구독
            stateManager.currentHealth.OnValueChanged += OnHealthChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (stateManager != null && IsOwner)
        {
            stateManager.currentHealth.OnValueChanged -= OnHealthChanged;
        }

        if (IsOwner) LocalInstance = null;
    }

    //void InitUI()
    //{
    //    if (playerHpImage == null)
    //    {
    //        GameObject go = GameObject.Find("PlayerHpSprite");
    //        if (go != null) playerHpImage = go.GetComponent<Image>();
    //    }

    //    if (vignetteImage == null)
    //    {
    //        GameObject go = GameObject.Find("VignetteImage");
    //        if (go != null) vignetteImage = go.GetComponent<Image>();
    //    }

    //    if (blindImage == null)
    //    {
    //        GameObject go = GameObject.Find("BlindImage");
    //        if (go != null)
    //        {
    //            blindImage = go.GetComponent<Image>();
    //            blindImage.enabled = false; 
    //        }
    //    }

    //    if (vignetteImage != null)
    //    {
    //        vignetteRect = vignetteImage.GetComponent<RectTransform>();
    //        vignetteRect.sizeDelta = new Vector2(800f, 800f);

    //        Color baseColor = vignetteImage.color;
    //        baseColor.a = 1f;
    //        vignetteImage.color = baseColor;
    //    }

    //    if (breathAudio == null)
    //    {
    //        breathAudio = GetComponent<AudioSource>();
    //    }

    //    if (stateManager != null)
    //    {
    //        isInitialized = true;
    //    }
    //}

    void Update()
    {
        if (!isInitialized || !IsOwner) return;

        UpdateStaminaVisuals();
        UpdateAudioFeedback();
    }

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
        float a = 1f;

        if (hp >= 50f)
        {
            g = (hp - 50f) / 50f;
            r = 0f;
        }
        else
        {
            r = (50f - hp) / 50f;
            g = 0f;
        }

        playerHpImage.color = new Color(r, g, b, a);
    }

    private void UpdateStaminaVisuals()
    {
        if (vignetteRect == null) return;

        // 스태미너 비율 계산 (0.0 ~ 1.0)
        float staminaPercent = stateManager.CurrentStamina / stateManager.player.maxStamina;

        // Min(25) ~ Max(45) 사이를 부드럽게 오감
        float targetScale = Mathf.Lerp(vignetteMinScale, vignetteMaxScale, staminaPercent);

        // 크기(Scale) 적용
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

    public void SetBlindScreen(bool isBlind)
    {
        if (blindImage != null)
        {
            blindImage.enabled = isBlind;
        }
    }

    #region 씬이동 시 참조 파괴 방지 스크립트

    public void RefreshUIReferences()
    {
        if (SceneUIReference.Instance != null)
        {
            playerHpImage = SceneUIReference.Instance.hpImage;
            vignetteImage = SceneUIReference.Instance.vignetteImage;
            blindImage = SceneUIReference.Instance.blindImage;

            if (vignetteImage != null)
            {
                vignetteRect = vignetteImage.GetComponent<RectTransform>();
                // 기존 초기화 로직 유지...
            }

            isInitialized = (playerHpImage != null && stateManager != null);
        }
        else
        {
            isInitialized = false;
            Debug.LogWarning("[PlayerUIManager] 현재 씬에서 SceneUIReference를 찾을 수 없습니다.");
        }
    }
    #endregion
}