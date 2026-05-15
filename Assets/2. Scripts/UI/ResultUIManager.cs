using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class ResultUIManager : NetworkBehaviour
{
    [Header("UI Elements")]
    public GameObject resultCanvas;       // 결과창 최상위 캔버스 오브젝트
    public Image blackScreen;             // 배경 검은 화면
    public TextMeshProUGUI resultText;    // 정산 결과 텍스트 (예: "수익: 100G / 실적 50pt")
    public Button acceptButton;           // 수락 버튼

    public override void OnNetworkSpawn()
    {
        // 서버의 pending 상태 구독
        GameMaster.Instance.hasPendingResult.OnValueChanged += OnResultStateChanged;

        // 로비 씬 로드 직후, 이미 정산 대기 상태라면 UI 띄우기
        if (GameMaster.Instance.hasPendingResult.Value)
        {
            ShowResultUI();
        }
        else
        {
            resultCanvas.SetActive(false);
        }

        // 수락 버튼은 게임의 흐름을 넘기는 권한이므로 호스트(방장)만 누를 수 있게 하거나 보어주기
        acceptButton.gameObject.SetActive(IsServer);
        acceptButton.onClick.AddListener(OnAcceptClicked);
    }

    public override void OnNetworkDespawn()
    {
        if (GameMaster.Instance != null)
            GameMaster.Instance.hasPendingResult.OnValueChanged -= OnResultStateChanged;
    }

    private void OnResultStateChanged(bool previous, bool current)
    {
        if (current)
        {
            ShowResultUI();
        }
        else
        {
            HideResultUI(); // 서버가 상태를 false로 바꾸면 전원이 동시에 이 함수를 실행
        }
    }

    private void ShowResultUI()
    {
        resultCanvas.SetActive(true);
        resultText.color = Color.white;

        int day = GameMaster.Instance.dayCycleManager.currentDayIndex.Value;
        int income = GameMaster.Instance.pendingIncome.Value;
        int score = GameMaster.Instance.pendingScore.Value;

        resultText.text = $"[Day {day} 정산 완료]\n\n오늘의 수익: {income} G\n획득한 실적: {score} pt";

        // 화면 띄우고 마우스 풀기 (전 클라이언트 실행)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ToggleLocalPlayerControl(false);
    }

    // UI가 닫힐 때 모든 클라이언트가 공통으로 실행할 복구 로직
    private void HideResultUI()
    {
        resultCanvas.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        ToggleLocalPlayerControl(true); // 마우스 및 조작 복구 (전 클라이언트 실행)
    }

    private void OnAcceptClicked()
    {
        if (!IsServer) return;
        acceptButton.interactable = false; // 중복 클릭 방지
        StartCoroutine(ProcessResultSequence());
    }

    private IEnumerator ProcessResultSequence()
    {
        // 1. 임시 보관했던 재화/점수 실제로 더하기
        GameMaster.Instance.ApplyPendingResults();

        int currentDay = GameMaster.Instance.dayCycleManager.currentDayIndex.Value;
        bool isCleared = GameMaster.Instance.performanceManager.CheckWeeklyClear();

        // 2. 4일차이면서 목표 미달성일 경우 -> 실패 연출
        if (currentDay >= 4 && !isCleared)
        {
            GameMaster.Instance.dayCycleManager.ProcessDayEnd(false);

            ShowFailureCutsceneClientRpc();

            // 5초간 검은 화면에서 실패 텍스트 보여주며 대기
            yield return new WaitForSeconds(5.0f);

            GameMaster.Instance.StartNewGame();
        }
        else
        {
            // 3. 성공이거나 1~3일차인 경우 -> 정상적으로 다음 날로 진행
            GameMaster.Instance.dayCycleManager.ProcessDayEnd(isCleared);
        }

        // 4. pending 상태를 false로 변경 -> OnResultStateChanged가 발동하여 전원의 화면을 닫고 조작을 복구함
        GameMaster.Instance.ClearPendingResults();
        acceptButton.interactable = true;
    }

    [Rpc(SendTo.Everyone)]
    private void ShowFailureCutsceneClientRpc()
    {
        acceptButton.gameObject.SetActive(false);
        resultText.color = Color.red;
        resultText.text = "실적 미달성...\n\n계약이 해지되었습니다.\n모든 것을 잃고 처음으로 돌아갑니다.";
    }

    private void ToggleLocalPlayerControl(bool isEnabled)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var myPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (myPlayerObject != null)
            {
                if (myPlayerObject.TryGetComponent(out PlayerMove move))
                    move.SetControlLock(!isEnabled);

                if (myPlayerObject.TryGetComponent(out PlayerRotation rot))
                    rot.enabled = isEnabled;

                if (myPlayerObject.TryGetComponent(out PlayerInteraction interact))
                    interact.enabled = isEnabled;

                if (myPlayerObject.TryGetComponent(out PlayerEquipment equip))
                    equip.enabled = isEnabled;
            }
        }
    }
}