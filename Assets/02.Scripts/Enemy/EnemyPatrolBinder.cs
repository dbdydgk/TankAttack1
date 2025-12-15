using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class EnemyPatrolBinder : MonoBehaviour, IPunInstantiateMagicCallback
{
    public PatrolRoute currentRoute;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = info.photonView.InstantiationData;

        int routeIndex = -1;
        if (data != null && data.Length > 0 && data[0] is int)
            routeIndex = (int)data[0];

        GameMgr gm = FindFirstObjectByType<GameMgr>();
        if (gm == null)
        {
            Debug.LogWarning("[EnemyPatrolBinder] GameMgr not found");
            return;
        }

        if (gm.patrolRoutes == null || routeIndex < 0 || routeIndex >= gm.patrolRoutes.Length)
        {
            Debug.LogWarning($"[EnemyPatrolBinder] Invalid routeIndex={routeIndex}, routes={(gm.patrolRoutes == null ? 0 : gm.patrolRoutes.Length)}");
            return;
        }

        currentRoute = gm.patrolRoutes[routeIndex];
        if (currentRoute == null)
        {
            Debug.LogWarning("[EnemyPatrolBinder] currentRoute is null");
            return;
        }

        Transform[] points = currentRoute.GetPoints();
        if (points == null || points.Length == 0)
        {
            Debug.LogWarning($"[EnemyPatrolBinder] Route has no points: {currentRoute.name}");
            return;
        }

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai == null)
        {
            Debug.LogWarning("[EnemyPatrolBinder] EnemyAI not found on same object");
            return;
        }

        ai.SetPatrolPoints(points); // 이게 핵심 (목적지까지 세팅됨)
        Debug.Log($"[EnemyPatrolBinder] Applied route={currentRoute.name}, points={points.Length}, routeIndex={routeIndex}");
    }
}