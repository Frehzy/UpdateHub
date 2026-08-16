namespace UpdateHub.Server.Domain.Enums;

/// <summary>Характеристика компьютера, изменение которой попадает в историю.</summary>
public enum ClientChangeType
{
    /// <summary>Изменилось сетевое имя.</summary>
    HostnameChanged,

    /// <summary>Изменилась версия операционной системы.</summary>
    OsVersionChanged,

    /// <summary>Изменилась модель процессора.</summary>
    CpuInfoChanged,

    /// <summary>Изменился объём оперативной памяти.</summary>
    MemoryChanged,

    /// <summary>Изменился объём диска.</summary>
    DiskChanged,

    /// <summary>Изменилась архитектура процессора.</summary>
    ArchitectureChanged,

    /// <summary>Изменилась версия ядра.</summary>
    KernelVersionChanged,

    /// <summary>Изменился отпечаток железа — возможна замена комплектующих.</summary>
    HardwareFingerprintChanged,

    /// <summary>Изменился IP-адрес.</summary>
    IpChanged,

    /// <summary>Изменился MAC-адрес.</summary>
    MacAddressChanged,

    /// <summary>Изменился сетевой интерфейс.</summary>
    NetworkInterfaceChanged,

    /// <summary>Компьютер переведён в другую группу.</summary>
    GroupChanged,

    /// <summary>Компьютер заблокирован.</summary>
    Blocked,

    /// <summary>Компьютер разблокирован.</summary>
    Unblocked,

    /// <summary>Компьютер зарегистрирован в системе.</summary>
    Registered,

    /// <summary>Выполнен вход пользователя.</summary>
    LoggedIn
}
