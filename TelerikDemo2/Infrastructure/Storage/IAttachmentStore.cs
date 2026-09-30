namespace TelerikDemo2.Infrastructure.Storage;

public sealed record StoredFile(string Name, long SizeBytes);


public interface IAttachmentStore
{
    IReadOnlyList<StoredFile> List(int orderId);
    Task SaveAsync(int orderId, string fileName, Stream content, CancellationToken ct = default);
    bool Delete(int orderId, string fileName);
    Stream Open(int orderId, string fileName);
}

public sealed class AttachmentOptions
{
    public const string SectionName = "Attachments";

    public long MaxBytes { get; set; } = 5 * 1024 * 1024;
    // No default: the configuration binder appends to an existing array. Values are in appsettings.json.
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
}
