using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WaypointManager : MonoBehaviour
{
    public List<Transform> waypoints = new List<Transform>();

    private void Awake()
    {
        // 자식으로 등록된 모든 Transform을 웨이포인트로 등록
        foreach (Transform child in transform)
        {
            waypoints.Add(child);
        }
    }

    public Transform GetRandomWaypoint()
    {
        if (waypoints.Count == 0) return null;
        return waypoints[Random.Range(0, waypoints.Count)];
    }

    public Transform GetFarWaypoint(Vector3 currentPos, float minDistance = 20f)
    {
        if (waypoints.Count == 0) return null;

        // 1. 현재 몬스터의 위치에서 'minDistance'보다 멀리 있는 거점들만 리스트로 추려냅니다.
        var farPoints = waypoints.Where(wp => Vector3.Distance(currentPos, wp.position) > minDistance).ToList();

        // 2. 만약 맵이 좁거나, 몬스터가 너무 구석에 있어서 20m 밖에 거점이 아예 없다면?
        if (farPoints.Count == 0)
        {
            // 게임이 멈추는 것을 방지하기 위해 쿨하게 전체 거점 중 랜덤으로 반환 (안전장치)
            Debug.LogWarning("<color=orange>[Waypoint]</color> 먼 거점이 없어 무작위 거점을 선택합니다.");
            return GetRandomWaypoint();
        }

        // 3. 멀리 있는 거점들 중에서 랜덤으로 하나를 선택하여 반환합니다.
        return farPoints[UnityEngine.Random.Range(0, farPoints.Count)];
    }
}