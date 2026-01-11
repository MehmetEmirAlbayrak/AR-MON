using UnityEngine;

[CreateAssetMenu(fileName = "ServerConfig", menuName = "Config/Server Config")]
public class ServerConfig : ScriptableObject
{
    public string serverIP = "http://192.168.1.102:5001";

    public string GetAnalyzeUrl()
    {
        return serverIP + "/analyze_frame";
    }
}
