using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoomData : MonoBehaviour
{
    [HideInInspector] //외부 접근을 위해 public으로 선언했지만 Inspector에는 안보임
    public string roomName="";
    [HideInInspector]
    public int connectPlayer = 0;
    [HideInInspector]
    public int maxPlayer = 0;

    public Text textRoomName; //룸 이름을 표시할 TextUI
    public Text textConnectInfo; //현재인원수/최대인원수 표시할 TextUI
    public void DispRoomData()
    {
        textRoomName.text = roomName; //룸 제목
        textConnectInfo.text = "(" + connectPlayer.ToString() + "/" + maxPlayer.ToString() + ")"; //현재인원수/최대인원수
    }
}
