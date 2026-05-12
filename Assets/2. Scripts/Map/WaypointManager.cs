using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 구역(Zone) 명확한 분리를 위한 열거형.
public enum MapZone
{
    School,
    SpiritualWorld,
    None
}

[System.Serializable]
public struct ZoneWaypointData
{
    public MapZone zone;
    [Tooltip("해당 구역의 웨이포인트들이 자식으로 들어있는 부모 Transform 객체")]
    public Transform parentObject;
}

public class WaypointManager : MonoBehaviour
{
    [Header("Zone Configuration")]
    [Tooltip("각 구역별 부모 객체를 등록해주세요.")]
    public List<ZoneWaypointData> zoneSettings = new List<ZoneWaypointData>();

    // O(1) 탐색을 위한 딕셔너리 캐싱
    private Dictionary<MapZone, List<Transform>> _zoneWaypoints = new Dictionary<MapZone, List<Transform>>();

    private void Awake()
    {
        foreach (var setting in zoneSettings)
        {
            if (setting.parentObject == null) continue;

            List<Transform> points = new List<Transform>();
            foreach (Transform child in setting.parentObject)
            {
                points.Add(child);
            }

            _zoneWaypoints.Add(setting.zone, points);
            Debug.Log($"<color=lime>[WaypointManager]</color> {setting.zone} 구역에 {points.Count}개의 거점을 캐싱 완료했습니다.");
        }
    }

    /// <summary>
    /// [새로운 API] 특정 구역의 웨이포인트 리스트 전체를 반환합니다. (Search/Flee State 전용)
    /// </summary>
    public List<Transform> GetWaypointsInZone(MapZone zone)
    {
        if (_zoneWaypoints.TryGetValue(zone, out List<Transform> points))
        {
            return points;
        }
        return new List<Transform>(); // 에러 방지용 빈 리스트 반환
    }

    //public Transform GetRandomWaypoint(MapZone zone)
    //{
    //    if (!_zoneWaypoints.TryGetValue(zone, out List<Transform> points) || points.Count == 0) return null;
    //    return points[Random.Range(0, points.Count)];
    //}

    public Transform GetFarWaypoint(Vector3 currentPos, MapZone zone, float minDistance = 20f)
    {
        if (!_zoneWaypoints.TryGetValue(zone, out List<Transform> points) || points.Count == 0) return null;

        //var farPoints = points.Where(wp => Vector3.Distance(currentPos, wp.position) > minDistance).ToList();

        //if (farPoints.Count == 0) return GetRandomWaypoint(zone);

        //return farPoints[Random.Range(0, farPoints.Count)];

        var farPoints = points
        .Where(wp => wp != null && wp.gameObject != null) // 살아있는 객체만 골라냄
        .Where(wp => Vector3.Distance(currentPos, wp.position) > minDistance)
        .ToList();

        if (farPoints.Count == 0) return GetRandomWaypoint(zone);

        return farPoints[Random.Range(0, farPoints.Count)];
    }

    public Transform GetRandomWaypoint(MapZone zone)
    {
        if (!_zoneWaypoints.TryGetValue(zone, out List<Transform> points)) return null;

        // 살아있는 포인트만 필터링
        var validPoints = points.Where(p => p != null).ToList();
        if (validPoints.Count == 0) return null;

        return validPoints[Random.Range(0, validPoints.Count)];
    }
}