using System.Collections.Generic;
using UnityEngine;

// 사진 이미지와 촬영 당시의 판정(메타데이터)을 하나로 묶는 클래스
[System.Serializable]
public class PhotoData
{
    public Texture2D image;          // 메모리에 올라간 실제 사진 이미지

    // --- 판정용 메타데이터 (수정 가능) ---
    public List<string> capturedTargets = new List<string>();

    [Header("Quest Linkage")]
    public List<int> satisfiedQuestIDs = new List<int>();

    public int playersInFrame = 0; // 사진에 찍힌 플레이어 수 
}

// 왜 PhotoDataManager여야 하는가?
// - 사진 데이터는 게임 내 여러 UI(카메라, 갤러리 등)에서 공유되어야 하므로 중앙 집중식 관리가 필요합니다.
// - 사진은 메모리를 많이 차지하는 리소스이므로, 메모리 누수를 방지하기 위해 명시적으로 관리해야 합니다.
// PhotoDataManager는 게임 내에서 사진 데이터를 관리하는 싱글톤 클래스입니다.
// 주요 기능: 사진 추가, 사진 삭제, 모든 사진 초기화(사이클 종료 시)
// [중요] 사진을 추가할 때는 반드시 AddPhoto() 함수를 통해야 하며, 사진을 삭제할 때는 RemovePhoto() 함수를 통해야 합니다.
// [중요] 사진을 삭제할 때는 반드시 RemovePhoto() 함수를 통해야 합니다. 이 함수는 메모리 누수를 방지하기 위해 사용하지 않는 Texture2D를 강제로 파괴하는 역할도 합니다.
// [중요] 사이클이 종료될 때는 ClearAllPhotos() 함수를 호출하여 메모리에 올라간 모든 사진을 초기화해야 합니다. 그렇지 않으면 메모리 누수가 발생할 수 있습니다.
// 기존의 CameraUI, GalleryUI는 CameraUI 스크립트를 보면 GalleryUI.currentCyclePhotos.Add(...) 형태로 갤러리 앱의 명부에 직접 접근해 사진을 쑤셔 넣고 있음.
// 이는 스크립트간의 강한 결합을 초래하고, 사진 데이터의 일관성 유지와 메모리 관리 측면에서 매우 위험한 방식입니다.
// PhotoDataManager를 도입함으로써, CameraUI는 사진을 찍을 때 PhotoDataManager.Instance.AddPhoto(...) 형태로 사진을 추가하고, GalleryUI는 PhotoDataManager.Instance.RemovePhoto(...) 형태로 사진을 삭제하게 됩니다.
// 이렇게 하면 사진 데이터의 일관성을 유지할 수 있고, 메모리 누수를 방지할 수 있으며, 스크립트 간의 결합도를 낮출 수 있습니다.
public class PhotoDataManager : MonoBehaviour
{
    public static PhotoDataManager Instance; // 싱글톤 접근

    [Header("Photo Database")]
    public List<PhotoData> currentPhotos = new List<PhotoData>();
    public readonly int maxPhotos = 4;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // 사진 추가 (카메라에서 호출)
    public bool AddPhoto(PhotoData newPhoto)
    {
        if (currentPhotos.Count >= maxPhotos) return false;

        currentPhotos.Add(newPhoto);
        QuestCameraBridge.Instance.RecalculateLocalDeferredQuests(); // 사진이 추가될 때마다 제출 대기 퀘스트 목록 갱신
        return true;
    }

    // 사진 삭제 (갤러리에서 호출)
    public void RemovePhoto(int index)
    {
        if (index >= 0 && index < currentPhotos.Count)
        {
            // [중요] 메모리 누수 방지: 사용하지 않는 Texture2D는 반드시 강제 파괴해야 함
            if (currentPhotos[index].image != null)
            {
                Destroy(currentPhotos[index].image);
            }
            currentPhotos.RemoveAt(index);
            QuestCameraBridge.Instance.RecalculateLocalDeferredQuests(); // 사진이 삭제될 때마다 제출 대기 퀘스트 목록 갱신
        }
    }

    // 사이클 종료 시 모든 사진 지우기
    public void ClearAllPhotos()
    {
        foreach (var photo in currentPhotos)
        {
            if (photo.image != null) Destroy(photo.image);
        }
        currentPhotos.Clear();
        Debug.Log("[PhotoDataManager] 메모리의 모든 사진이 초기화되었습니다.");
    }
}