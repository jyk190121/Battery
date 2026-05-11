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
        if (current) ShowResultUI();
        else resultCanvas.SetActive(false); // 서버가 상태를 해제하면 전체 클라이언트 UI 꺼짐
    }

    private void ShowResultUI()
    {
        resultCanvas.SetActive(true);
        resultText.color = Color.white;

        int day = GameMaster.Instance.dayCycleManager.currentDayIndex.Value;
        int income = GameMaster.Instance.pendingIncome.Value;
        int score = GameMaster.Instance.pendingScore.Value;

        resultText.text = $"[Day {day} 정산 완료]\n\n오늘의 수익: {income} G\n획득한 실적: {score} pt";

        // TODO: 여기서 플레이어 이동/조작을 막는 로직(PlayerController.enabled = false 등) 호출 가능
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ToggleLocalPlayerControl(false); // 플레이어 이동/카메라 회전 차단
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
            //  StartNewGame이 작동할 수 있도록 먼저 현재 세션(isSessionActive)을 강제 종료합니다.
            GameMaster.Instance.dayCycleManager.ProcessDayEnd(false);

            // 전 클라이언트에게 실패 연출 명령
            ShowFailureCutsceneClientRpc();

            // 5초간 검은 화면에서 실패 텍스트 보여주며 대기
            yield return new WaitForSeconds(5.0f);

            // 하드 리셋 (이제 세션이 종료되었으므로 정상적으로 1주차 1일차 데이터로 덮어씌워짐)
            GameMaster.Instance.StartNewGame();
        }
        else
        {
            // 3. 성공이거나 1~3일차인 경우 -> 정상적으로 다음 날로 진행
            GameMaster.Instance.dayCycleManager.ProcessDayEnd(isCleared);
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        ToggleLocalPlayerControl(true); // 플레이어 이동/카메라 회전 복구

        // 4. 모든 작업이 끝났으므로 UI 닫기 및 플레이어 조작 복구
        GameMaster.Instance.ClearPendingResults();
        acceptButton.interactable = true;
    }

    [Rpc(SendTo.Everyone)]
    private void ShowFailureCutsceneClientRpc()
    {
        acceptButton.gameObject.SetActive(false);
        resultText.color = Color.red;
        resultText.text = "실적 미달성...\n\n계약이 해지되었습니다.\n모든 것을 잃고 처음으로 돌아갑니다.";

        // TODO: 여기서 섬뜩한 효과음이나 게임 오버 사운드 재생
    }

    private void ToggleLocalPlayerControl(bool isEnabled)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var myPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (myPlayerObject != null)
            {
                // 1. 이동 정지 및 마우스 강제 잠금 로직 우회
                if (myPlayerObject.TryGetComponent(out PlayerMove move))
                {
                    // isEnabled가 false(UI 켜짐)이면 Lock은 true가 되어야 함
                    move.SetControlLock(!isEnabled);
                }

                // 2. 마우스를 움직일 때 카메라가 돌아가는 현상 방지
                if (myPlayerObject.TryGetComponent(out PlayerRotation rot))
                {
                    rot.enabled = isEnabled;
                }

                // 3. UI 조작 중 좌클릭/상호작용으로 인한 오작동 방지
                if (myPlayerObject.TryGetComponent(out PlayerInteraction interact))
                    interact.enabled = isEnabled;

                if (myPlayerObject.TryGetComponent(out PlayerEquipment equip))
                    equip.enabled = isEnabled;
            }
        }
    }
}