namespace Ecosphere.Infrastructure.Infrastructure.Services.Interfaces;

public interface IStunTurnServer
{
    Task<bool> StartAsync(int stunPort = 3478, int turnPort = 3479);
    Task StopAsync();
    bool IsRunning { get; }
    string GetPublicIpAddress();
    StunServerConfig GetStunConfig();
    TurnServerConfig GetTurnConfig(long userId, int validityHours = 0);
}

public class StunServerConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public List<string> Urls { get; set; } = new();
}

public class TurnServerConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public List<string> Urls { get; set; } = new();
    public string Username { get; set; } = string.Empty;
    public string Credential { get; set; } = string.Empty;
}
