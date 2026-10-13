using LibreLancer.Net;

namespace LLServer;

public class ServerConfig
{
    public string ServerName = "";
    public string ServerDescription = "";
    public string FreelancerPath = "";
    public string? LoginUrl;
    public string DatabasePath = "";
    public int Port = LNetConst.DEFAULT_PORT;
    public int MaxPlayers = 200;
    public int ThreadCount = 0;
    public string? RuntimeStatusFile;
    public string? InstanceId;
    public string? SystemId;
    public string? InstanceEndpoint;

    public void CopyFrom(ServerConfig other)
    {
        ServerName = other.ServerName;
        ServerDescription = other.ServerDescription;
        FreelancerPath= other.FreelancerPath;
        LoginUrl = other.LoginUrl;
        DatabasePath = other.DatabasePath;
        Port = other.Port;
        MaxPlayers = other.MaxPlayers;
        RuntimeStatusFile = other.RuntimeStatusFile;
        InstanceId = other.InstanceId;
        SystemId = other.SystemId;
        InstanceEndpoint = other.InstanceEndpoint;
    }
}
