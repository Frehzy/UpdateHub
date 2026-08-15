namespace UpdateHub.Server.Domain.Entities;

public class UpdateDetailEntity
{
    public int Id { get; set; }
    public int UpdateRequestId { get; set; }
    public string ManifestEntryId { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? OldMd5Hash { get; set; }
    public string NewMd5Hash { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public UpdateRequestEntity? UpdateRequest { get; set; }
    public ManifestEntryEntity? ManifestEntry { get; set; }
}