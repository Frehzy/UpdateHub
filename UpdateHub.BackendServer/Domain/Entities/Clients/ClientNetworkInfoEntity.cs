namespace UpdateHub.BackendServer.Domain.Entities.Clients;

/// <summary>
/// Сетевой адрес, с которого обращался компьютер.
/// </summary>
/// <remarks>
/// Адрес берётся из <c>HttpContext.Connection.RemoteIpAddress</c>, а не из тела
/// запроса, поэтому клиент не может назвать произвольный IP.
/// </remarks>
public class ClientNetworkInfoEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Компьютер, которому принадлежит адрес.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>IP-адрес.</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>MAC-адрес, если клиент его сообщил.</summary>
    public string? MacAddress { get; set; }

    /// <summary>Имя сетевого интерфейса, если клиент его сообщил.</summary>
    public string? NetworkInterface { get; set; }

    /// <summary>Момент последнего обращения с этого адреса.</summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>Признак того, что адрес используется в настоящее время.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Компьютер (навигационное свойство).</summary>
    public ClientEntity? Client { get; set; }
}
