        using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PhoneVoiceReceiver : MonoBehaviour
{
    public static PhoneVoiceReceiver Instance;

    private AudioSource audioSource;
    private Queue<float> audioBuffer = new Queue<float>();
    private object bufferLock = new object(); // 스레드 충돌 방지용 자물쇠

    private void Awake()
    {
        if (Instance == null) Instance = this;
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 0f; // 무조건 2D 귀 안에서 들림

        // [중요 팁] Unity에서 OnAudioFilterRead를 작동시키려면 AudioSource가 '재생 중'이어야 합니다.
        // 클립이 없어도 Play()를 호출해줍니다. (안 될 경우 1초짜리 무음 AudioClip을 넣고 Loop를 돌리세요)
        audioSource.Play();
    }

    // 아바타 스피커(송신기)가 이 함수를 통해 파형 데이터를 밀어넣어 줍니다.
    public void FeedAudioData(float[] rawData)
    {
        lock (bufferLock)
        {
            for (int i = 0; i < rawData.Length; i++)
            {
                audioBuffer.Enqueue(rawData[i]);
            }

            // 메모리 폭발 방지: 버퍼가 너무 쌓이면 오래된 데이터 폐기 (약 1초 분량)
            if (audioBuffer.Count > 100000) audioBuffer.Clear();
        }
    }

    // Unity 오디오 엔진이 스피커로 소리를 내보내기 직전에 호출하는 마법의 함수
    private void OnAudioFilterRead(float[] data, int channels)
    {
        lock (bufferLock)
        {
            for (int i = 0; i < data.Length; i++)
            {
                // 버퍼에 씹어먹을 데이터가 있으면 내보내고, 없으면 무음(0) 처리
                if (audioBuffer.Count > 0)
                    data[i] = audioBuffer.Dequeue();
                else
                    data[i] = 0f;
            }
        }
    }
}