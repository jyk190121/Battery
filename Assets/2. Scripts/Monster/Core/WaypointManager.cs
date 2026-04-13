using UnityEngine;
using System.Collections.Generic;

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
}