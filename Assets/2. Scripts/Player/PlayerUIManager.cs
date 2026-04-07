using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class PlayerUIManager : NetworkBehaviour
{
    private PlayerStateManager stateManager;

    [Header("UI References")]
    public Image healthImage;

    [Header("Post Processing")]
    public Volume staminaPostProcessVolume;

    [Header("Audio")]
    public AudioSource breathAudio;

    bool isInitialized = false;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }

        stateManager = GetComponent<PlayerStateManager>();
        InitUI();
    }

    void InitUI()
    {
        if (healthImage == null)
        {
            GameObject go = GameObject.Find("TestHealth");
            if (go != null) healthImage = go.GetComponent<Image>();
        }

        if (breathAudio == null)
        {
            breathAudio = GetComponent<AudioSource>();
        }

        if (stateManager != null)
        {
            isInitialized = true;
        }
    }

    void Update()
    {
        if (!isInitialized || !IsOwner) return;

        UpdateHealthVisuals();
        UpdateStaminaVisuals();
        UpdateAudioFeedback();
    }

    private void UpdateHealthVisuals()
    {
        if (stateManager == null || healthImage == null) return;

        float hp = stateManager.currentHealth.Value;
        Color healthColor;

        if (hp >= 100f) healthColor = Color.green;
        else if (hp >= 66f) healthColor = Color.yellow;
        else if (hp >= 33f) healthColor = new Color(1f, 0.5f, 0f);
        else if (hp >= 1f) healthColor = Color.red;
        else healthColor = Color.black;

        healthImage.color = healthColor;
    }

    private void UpdateStaminaVisuals()
    {
        // 스태미너 소모량에 따라 0 ~ 1 수치화
        float staminaPercent = stateManager.CurrentStamina / stateManager.player.maxStamina;
        float intensity = 1f - staminaPercent;

        // 포스트 프로세싱 볼륨의 전체 강도를 조절하여 화면을 흐리게 만듦
        if (staminaPostProcessVolume != null)
        {
            staminaPostProcessVolume.weight = intensity;
        }
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