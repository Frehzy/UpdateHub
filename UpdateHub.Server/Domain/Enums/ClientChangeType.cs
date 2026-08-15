namespace UpdateHub.Server.Domain.Enums;

public enum ClientChangeType
{
    // Из ClientComputerInfo
    HostnameChanged,
    OsVersionChanged,
    CpuInfoChanged,
    MemoryChanged,
    DiskChanged,
    ArchitectureChanged,
    KernelVersionChanged,

    // Из ClientNetworkInfo
    IpChanged,
    MacAddressChanged,
    NetworkInterfaceChanged,

    // Из Clients
    GroupChanged,
    Blocked,
    Unblocked,

    // Из ClientSessions
    SessionCreated,
    SessionClosed
}