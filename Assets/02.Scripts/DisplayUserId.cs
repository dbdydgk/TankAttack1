using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class DisplayUserId : MonoBehaviour
{
    public Text userId;
    PhotonView pv; 
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pv = GetComponent<PhotonView>();

        // userId가 null이 아니고, pv.Owner가 존재할 때만 NickName 설정
        if (userId != null && pv != null && pv.Owner != null && pv.Owner.NickName != null)
        {
            userId.text = pv.Owner.NickName;
        }
        else
        {
            Debug.Log("DisplayUserId: Player name or userId Text is not set properly.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
