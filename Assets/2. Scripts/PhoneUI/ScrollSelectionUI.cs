using Unity.VectorGraphics.Editor;
using UnityEngine;
using UnityEngine.InputSystem;

// 이 클래스는 직접 사용되지 않고 다른 UI 스크립트들의 '부모' 역할만 합니다.
public abstract class ScrollSelectionUI : MonoBehaviour
{
    protected int currentIndex = 0;
    protected int maxIndex;

    // 마우스 휠 입력 감지 및 방향 결정
    protected void HandleScroll()
    {
        if (Mouse.current == null) return;

        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (scrollY != 0)
        {
            if (scrollY > 0) MoveHighlight(-1);
            else if (scrollY < 0) MoveHighlight(1);
        }
    }

    // 인덱스 계산 및 제한
    private void MoveHighlight(int direction)
    {
        int nextIndex = Mathf.Clamp(currentIndex + direction, 0, maxIndex);

        if (nextIndex != currentIndex)
        {
            currentIndex = nextIndex;
            SoundManager.Instance.PlaySfx(SfxSound.PHONE_SCROLL);
            UpdateHighlightVisuals(); // 시각적 업데이트
            OnIndexChanged();         // 스크롤 변경시 발생하는 추가 작업 (필요한 자식만 사용)
        }
    }

    // UI가 실제로 어떻게 변할지(이동할지, 커질지)는 자식 클래스에서 정의하도록 강제합니다.
    protected abstract void UpdateHighlightVisuals();

    // 인덱스가 바뀌었을 때 갤러리의 메인 사진 갱신 같은 '추가 작업'이 필요한 곳에서만 재정의(Override)해서 씁니다.
    protected virtual void OnIndexChanged() { }
}