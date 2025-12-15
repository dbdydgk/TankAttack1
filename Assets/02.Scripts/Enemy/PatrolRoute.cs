using UnityEngine;

public class PatrolRoute : MonoBehaviour
{
    [Tooltip("비워두면 자식 Transform들을 순서대로 자동 수집")]
    public Transform[] points;

    public Transform[] GetPoints()
    {
        if (points != null && points.Length > 0) return points;

        // 자식들을 순서대로 모아 points로 사용
        int n = transform.childCount;
        points = new Transform[n];
        for (int i = 0; i < n; i++)
            points[i] = transform.GetChild(i);

        return points;
    }
}