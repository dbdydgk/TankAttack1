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
        userId.text = pv.Owner.NickName;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
