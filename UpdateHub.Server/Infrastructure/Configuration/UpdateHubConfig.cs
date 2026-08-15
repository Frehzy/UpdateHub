namespace UpdateHub.Server.Infrastructure.Configuration;

public class UpdateHubConfig
{
    public string FilesPath { get; set; } = "/app/files";
    public string DatabasePath { get; set; } = "/app/data/updatehub.db";
    public int ManifestRefreshIntervalSeconds { get; set; } = 5;
    public int FileWatcherDelayMilliseconds { get; set; } = 2000;
    public int MaxDownloadConcurrency { get; set; } = 20;
    public int Md5BufferSizeBytes { get; set; } = 65536;
    public bool EnableStatistics { get; set; } = true;
}