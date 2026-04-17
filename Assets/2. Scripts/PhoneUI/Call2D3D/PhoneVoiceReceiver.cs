using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PhoneVoiceReceiver : MonoBehaviour
{
    public static PhoneVoiceReceiver Instance;

    private AudioSource audioSource;
    private Queue<float> audioBuffer = new Queue<float>();
    private object bufferLock = new object();

    // [핵심 수정] 유튜브처럼 소리가 끊기지 않게 모아두는 '프리버퍼링' 시스템
    private bool isBuffering = true;
    private int minBufferSize = 4096; // 약 0.1초 분량의 오디오 데이터 (안전장치)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.Play();
    }

    public void FeedAudioData(float[] rawMonoChunk)
    {
        lock (bufferLock)
        {
            for (int i = 0; i < rawMonoChunk.Length; i++)
            {
                // 필요하다면 여기서 곱하는 숫자(3.0f 등)를 키워 볼륨을 더 증폭시킬 수 있습니다.
                audioBuffer.Enqueue(rawMonoChunk[i] * 3.0f);
            }

            if (audioBuffer.Count > 100000) audioBuffer.Clear();
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        lock (bufferLock)
        {
            // 프리버퍼링: 큐에 데이터가 충분히 찰 때까지 무음으로 대기
            if (isBuffering)
            {
                if (audioBuffer.Count >= minBufferSize)
                {
                    isBuffering = false; // 충분히 모였으니 재생 시작!
                }
                else
                {
                    for (int i = 0; i < data.Length; i++) data[i] = 0f;
                    return;
                }
            }

            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;

                if (audioBuffer.Count > 0)
                {
                    sample = audioBuffer.Dequeue();
                }
                else
                {
                    // 재생 도중 데이터가 바닥나면 억지로 끊긴 소리를 내지 않고 다시 버퍼링 모드로 진입
                    isBuffering = true;
                }

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = sample;
                }
            }
        }
    }
}