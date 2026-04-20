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
            Debug.Log("<color=green>[DSP 파이프라인] 송신기가 데이터를 청크(Chunk) 단위로 전송합니다!</color>");
            isLogPrinted = true;
        }

        // [핵심 수정] 배열을 수천 번 생성하지 않고, 한 번의 덩어리(Chunk)로 묶어서 보냅니다.
        int monoLength = data.Length / channels;
        float[] chunk = new float[monoLength];

        for (int i = 0, j = 0; i < data.Length; i += channels, j++)
        {
            float mixedSample = 0f;
            for (int c = 0; c < channels; c++)
            {
                mixedSample += data[i + c];
            }
            chunk[j] = mixedSample / channels; // 평균값(모노) 계산
        }

        // 묶음 데이터를 한 번에 수신기로 넘김 (스레드 과부하 해결)
        PhoneVoiceReceiver.Instance.FeedAudioData(chunk);
    }
}