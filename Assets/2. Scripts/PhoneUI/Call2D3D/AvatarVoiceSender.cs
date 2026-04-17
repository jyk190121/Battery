using UnityEngine;

public class AvatarVoiceSender : MonoBehaviour
{
    private bool isCalling = false;
    private bool isLogPrinted = false;

    public void SetCallMode(bool state)
    {
        isCalling = state;
        isLogPrinted = false;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!isCalling || PhoneVoiceReceiver.Instance == null) return;

        if (!isLogPrinted)
        {
            Debug.Log("<color=green>[DSP 파이프라인] 송신기가 채널을 동기화하여 데이터를 넘기기 시작했습니다!</color>");
            isLogPrinted = true;
        }

        // [핵심 수정] 몇 채널이든 상관없이 모든 소리를 합쳐서 '모노(1채널)' 데이터로 압축하여 전송
        for (int i = 0; i < data.Length; i += channels)
        {
            float mixedSample = 0f;
            for (int c = 0; c < channels; c++)
            {
                mixedSample += data[i + c];
            }
            mixedSample /= channels; // 평균값 산출

            PhoneVoiceReceiver.Instance.FeedAudioData(new float[] { mixedSample });
        }
    }
}