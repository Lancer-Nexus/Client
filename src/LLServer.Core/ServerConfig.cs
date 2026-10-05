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
    public string? BindAddress;
    public int MaxPlayers = 200;
    public int ThreadCount = 0;
    public string? RuntimeStatusFile;
    public string? DrainFlagFile;
    public string LocalOperatorsFile = "llserver-ops.json";
    public string? InstanceId;
    public string? NpcCoordinatorUrl;
    public string? NpcTransferListenAddress;
    public int NpcTransferPort;
    public string? NpcTransferServerCertificate;
    public string? NpcTransferClientCaCertificate;
    public string? NpcTransferStagingDirectory;
    public string? SystemId;
    public string[] SystemIds = [];
    public string? InstanceEndpoint;
    public int? TestNewCharacterRank;
    public string? TestMissionNickname;

    public void CopyFrom(ServerConfig other)
    {
        ServerName = other.ServerName;
        ServerDescription = other.ServerDescription;
        FreelancerPath= other.FreelancerPath;
        LoginUrl = other.LoginUrl;
        DatabasePath = other.DatabasePath;
        Port = other.Port;
        BindAddress = other.BindAddress;
        MaxPlayers = other.MaxPlayers;
        RuntimeStatusFile = other.RuntimeStatusFile;
        DrainFlagFile = other.DrainFlagFile;
        LocalOperatorsFile = other.LocalOperatorsFile;
        InstanceId = other.InstanceId;
        NpcCoordinatorUrl = other.NpcCoordinatorUrl;
        NpcTransferListenAddress = other.NpcTransferListenAddress;
        NpcTransferPort = other.NpcTransferPort;
        NpcTransferServerCertificate = other.NpcTransferServerCertificate;
        NpcTransferClientCaCertificate = other.NpcTransferClientCaCertificate;
        NpcTransferStagingDirectory = other.NpcTransferStagingDirectory;
        SystemId = other.SystemId;
        SystemIds = (string[])other.SystemIds.Clone();
        InstanceEndpoint = other.InstanceEndpoint;
        TestNewCharacterRank = other.TestNewCharacterRank;
        TestMissionNickname = other.TestMissionNickname;
    }
}
