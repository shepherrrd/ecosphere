namespace Ecosphere.Infrastructure.Data.Models;

public class IceServerConfig
{
    public List<string> Urls { get; set; } = new();
    public string? Username { get; set; }
    public string? Credential { get; set; }
}
