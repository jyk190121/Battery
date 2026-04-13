using UnityEngine;
using System.Collections.Generic;

public enum SfxSound
{
    UI_CLICK,




    // 휴대폰 관련 사운드
    // 1. 휴대폰 기본 조작
    PHONE_OPEN,           // 폰 꺼낼 때 (Q키)
    PHONE_CLOSE,          // 폰 집어넣을 때 (Q키)
    PHONE_SCROLL,         // 스크롤 이동 (마우스 휠)
    PHONE_SELECT,         // 항목 선택 / 앱 진입 (우클릭)
    PHONE_RETURN,         // 뒤로 가기 (C키)
    PHONE_ERROR,          // 조작 불가 / 경고 (자기자신 전화, 통신망 미연결 등)

    // 2. 통화 앱 (Call)
    PHONE_CALLALARM,      // 전화 걸려올 때 벨소리 (수신)
    PHONE_DIAL,           // 전화 걸 때 연결음 "뚜루루루" (발신)
    PHONE_ACCEPT,         // 전화 받을 때 "딸깍"
    PHONE_REJECT,         // 전화 끊거나 거절할 때 "뚝"
    PHONE_BUSY,           // 상대방 통화 중일 때 "뚜- 뚜-"

    // 3. 메시지 앱 (Message)
    PHONE_MESSAGE_ALARM,  // 폰 내려져 있을 때 문자 알림음
    PHONE_TYPING_START,   // 입력창 활성화
    PHONE_TYPING,         // 타자 칠 때 "타닥"
    PHONE_MESSAGE_SEND,   // 내가 문자 보낼 때 
    PHONE_MESSAGE_RECEIVE,// 채팅방 안에서 문자 받을 때 

    // 4. 카메라 & 갤러리 앱 (Camera & Gallery)
    PHONE_TAKEPHOTO,      // 사진 찍을 때 "찰칵"
    PHONE_PHOTOMODECHANGE,// 전후면 카메라 전환
    PHONE_PHOTOFULLALERT, // 용량 꽉 찼을 때 팝업 경고음
    PHONE_GALLERYDELETE,  // 우클릭 홀드 중 게이지 차오르는 소리 (지속음)
    PHONE_GALLERYDELETED,  // 삭제 완료 소리

    // 몬스터 관련 사운드
    // 1. 환경
    VENT_CREAK            // 환풍구 열리는 소리
}

public enum BgmSound
{
    TITLE,
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [System.Serializable]
    public struct SfxData
    {
        public SfxSound sound;
        public AudioClip clip;
    }

    [System.Serializable]
    public struct BgmData
    {
        public BgmSound sound;
        public AudioClip clip;
    }

    // sfx와 bgm을 관리하기 위한 리스트와 오디오 소스
    [Header("Sound Settings")]
    [SerializeField] private List<SfxData> sfxList = new List<SfxData>();
    [SerializeField] private List<BgmData> bgmList = new List<BgmData>();

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;       // 단발성 효과음 재생용 (UI 클릭, 문자 입력 등)
    [SerializeField] private AudioSource loopSfxSource;   // 반복성/제어필요 사운드 (벨소리, 다이얼 등)
    [SerializeField] private AudioSource bgmSource;       // 배경음악 재생용

    private Dictionary<SfxSound, AudioClip> sfxDictionary = new Dictionary<SfxSound, AudioClip>();
    private Dictionary<BgmSound, AudioClip> bgmDictionary = new Dictionary<BgmSound, AudioClip>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDictionaries();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeDictionaries()
    {
        foreach(var sfx in sfxList)
        {
            if(!sfxDictionary.ContainsKey(sfx.sound))
            {
                sfxDictionary.Add(sfx.sound, sfx.clip);
            }
        }

        foreach(var bgm in bgmList)
        {
            if(!bgmDictionary.ContainsKey(bgm.sound))
            {
                bgmDictionary.Add(bgm.sound, bgm.clip);
            }
        }
    }

    #region 사운드 재생 메서드
    // 단발성 효과음 재생
    public void PlaySfx(SfxSound sound)
    {
        if(sfxDictionary.TryGetValue(sound, out AudioClip clip))
        {
            sfxSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning($"SFX Sound {sound} 없음");
        }
    }

    // 반복성 (벨소리, 통화연결음 등 - Loop 활성화 및 Play 사용)
    public void PlayLoopSfx(SfxSound sound)
    {
        if (sfxDictionary.TryGetValue(sound, out AudioClip clip))
        {
            loopSfxSource.clip = clip;
            loopSfxSource.loop = true;
            loopSfxSource.Play();
        }
        else Debug.LogWarning($"Loop SFX Sound {sound} 없음");
    }

    // 반복 재생 중지 (전화 받을 때, 끊을 때 호출)
    public void StopLoopSfx()
    {
        if (loopSfxSource.isPlaying)
        {
            loopSfxSource.Stop();
        }
    }

    public void PlayBgm(BgmSound type)
    {
        if(bgmDictionary.TryGetValue(type, out AudioClip clip))
        {
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }
        else
        {
            Debug.LogWarning($"BGM Sound {type} 없음");
        }
    }

    public void StopBgm()
    {
         bgmSource.Stop();
    }

    // 3D 사운드 재생을 위해 외부에서 오디오 클립만 가져갈 수 있게 
    public AudioClip GetSfxClip(SfxSound sound)
    {
        if (sfxDictionary.TryGetValue(sound, out AudioClip clip))
        {
            return clip;
        }
        Debug.LogWarning($"SFX Sound {sound} 없음");
        return null;
    }

    #endregion
}
