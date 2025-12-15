using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class EnemyPatrolBinder : MonoBehaviour, IPunInstantiateMagicCallback
{
    public Transform[] patrolPoints;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        int routeIndex = -1;

        object[] data = info.photonView.InstantiationData;
        if (data != null && data.Length > 0 && data[0] is int i)
            routeIndex = i;

        ApplyRoute(routeIndex);
    }

    void ApplyRoute(int routeIndex)
    {
        if (routeIndex < 0)
        {
            Debug.LogWarning($"[EnemyPatrolBinder] routeIndex=-1 (InstantiationData 없음) / {name}", this);
            return;
        }

        GameMgr gm = FindFirstObjectByType<GameMgr>();
        if (gm == null || gm.patrolRoutes == null || routeIndex >= gm.patrolRoutes.Length || gm.patrolRoutes[routeIndex] == null)
        {
            Debug.LogWarning($"[EnemyPatrolBinder] 잘못된 routeIndex={routeIndex} / {name}", this);
            return;
        }

        var points = gm.patrolRoutes[routeIndex].GetPoints();
        if (points == null || points.Length == 0)
        {
            Debug.LogWarning($"[EnemyPatrolBinder] route에 포인트가 없음 idx={routeIndex} / {name}", this);
            return;
        }

        var ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.SetPatrolPoints(points, randomStartIndex: false);
            Debug.Log($"[EnemyPatrolBinder] 바인딩 성공 route={gm.patrolRoutes[routeIndex].name}, points={points.Length}, role={ai.enemyData?.role}", this);
        }

        patrolPoints = points;
    }
}