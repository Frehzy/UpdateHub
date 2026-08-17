namespace UpdateHub.BackendServer.Application.Sync;

/// <summary>
/// Сведения о себе, которые скрипт присылает при обращении.
/// </summary>
/// <param name="Hostname">Сетевое имя компьютера.</param>
/// <param name="HardwareFingerprint">Отпечаток железа.</param>
/// <param name="OsVersion">Версия операционной системы.</param>
/// <param name="KernelVersion">Версия ядра.</param>
/// <param name="Architecture">Архитектура процессора.</param>
/// <param name="CpuInfo">Модель процессора.</param>
/// <param name="MemoryGb">Объём оперативной памяти в гигабайтах.</param>
/// <param name="DiskGb">Объём диска в гигабайтах.</param>
/// <param name="MacAddress">MAC-адрес основного интерфейса.</param>
public sealed record ClientReport(
    string? Hostname = null,
    string? HardwareFingerprint = null,
    string? OsVersion = null,
    string? KernelVersion = null,
    string? Architecture = null,
    string? CpuInfo = null,
    int? MemoryGb = null,
    int? DiskGb = null,
    string? MacAddress = null);
