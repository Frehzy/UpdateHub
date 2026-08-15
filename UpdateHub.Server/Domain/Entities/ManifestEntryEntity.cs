namespace UpdateHub.Server.Domain.Entities;

public class ManifestEntryEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RelativePath { get; set; } = string.Empty;
    public string Md5Hash { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UpdateDetailEntity> UpdateDetails { get; set; } = [];
    public ICollection<FileChangeEntity> FileChanges { get; set; } = [];
}