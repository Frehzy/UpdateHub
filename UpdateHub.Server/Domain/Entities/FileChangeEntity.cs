using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Domain.Entities;

public class FileChangeEntity
{
    public int Id { get; set; }
    public string? ManifestEntryId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public FileChangeType ChangeType { get; set; }
    public string? OldMd5Hash { get; set; }
    public string? NewMd5Hash { get; set; }
    public DateTime ChangeTimestamp { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; } = false;

    public ManifestEntryEntity? ManifestEntry { get; set; }
}