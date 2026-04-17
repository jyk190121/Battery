using UnityEngine;

public class AvatarVoiceSender : MonoBehaviour
{
    private bool isCalling = false;

    public void SetCallMode(bool state)
    {
        isCalling = state;
    }

    // Unity 오디오 엔진이 아바타의 3D 스피커로 소리를 내보낼 때 
    // 그 소리 파동(data)을 그대로 들고 이 함수를 지나갑니다.
    private void OnAudioFilterRead(float[] data, int channels)
    {
        // 1. 전화 중이 아니거나 수신기가 없으면 아무것도 안 하고 그냥 지나보냄 (정상 3D 재생)
        if (!isCalling || PhoneVoiceReceiver.Instance == null) return;

        // 2. 전화 중이면? 내 3D 소리는 그대로 내보내면서, 
        // 동시에 수신기(전화기)의 파이프라인으로 파형을 똑같이 복사해서 던져줌!
        PhoneVoiceReceiver.Instance.FeedAudioData(data);
    }
}