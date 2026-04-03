using UnityEngine;
using System.Collections.Generic;

public class ItemSpawnPoint : MonoBehaviour
{
    [Header("지역 설정")]
    public SpawnLocation location;

    // 이 구역에 속한 실제 좌표들 (자식들)
    [SerializeField] private List<Transform> points = new List<Transform>();

    // 스포너가 요청할 때 자식 리스트를 넘겨주는 통로
    public List<Transform> GetPoints() => points;

    //스스로 자식들을 찾아서 리스트를 갱신하는 로직 (기능 분산)
    public void UpdateChildPoints()
    {
        points.Clear();
        if (transform.childCount > 0)
        {
            foreach (Transform child in transform)
            {
                points.Add(child);
            }
        }
        else
        {
            points.Add(this.transform);
        }
    }

    private void OnDrawGizmos()
    {
        switch (location)
        {
            case SpawnLocation.ScienceRoom: Gizmos.color = Color.green; break;
            case SpawnLocation.PrincipalRoom: Gizmos.color = Color.red; break;
            case SpawnLocation.ArtRoom: Gizmos.color = Color.yellow; break;
            default: Gizmos.color = Color.cyan; break;
        }

        // 자식들 위치에도 작은 구체를 그려서 시각화합니다.
        foreach (Transform p in points)
        {
            if (p != null) Gizmos.DrawWireSphere(p.position, 0.2f);
        }
        Gizmos.DrawSphere(transform.position, 0.4f); // 부모는 좀 더 크게
    }
}