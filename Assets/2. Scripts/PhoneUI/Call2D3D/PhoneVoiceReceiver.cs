using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PhoneVoiceReceiver : MonoBehaviour
{
    public static PhoneVoiceReceiver Instance;

    private AudioSource audioSource;
    private Queue<float> audioBuffer = new Queue<float>();
    private object bufferLock = new object();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.Play();
    }

    public void FeedAudioData(float[] rawMonoData)
    {
        lock (bufferLock)
        {
            for (int i = 0; i < rawMonoData.Length; i++)
            {
                // 소리가 너무 작을 수 있으니 볼륨(Gain)을 2배로 증폭해서 버퍼에 넣습니다.
                audioBuffer.Enqueue(rawMonoData[i] * 2.0f);
            }

            if (audioBuffer.Count > 100000) audioBuffer.Clear();
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        lock (bufferLock)
        {
            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;
                if (audioBuffer.Count > 0)
                {
                    sample = audioBuffer.Dequeue(); // 모노 데이터 1개 꺼내기
                }

                // 꺼낸 1개의 소리를 L, R 양쪽 귀에 똑같이 복사
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = sample;
                }
            }
        }
    }
}