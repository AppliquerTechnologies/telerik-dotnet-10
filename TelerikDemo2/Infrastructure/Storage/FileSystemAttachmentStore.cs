namespace TelerikDemo2.Infrastructure.Storage;

// Files under App_Data/uploads/{orderId}.
public sealed class FileSystemAttachmentStore : IAttachmentStore
{
    private readonly string _root;

    public FileSystemAttachmentStore(IWebHostEnvironment env) =>
        _root = Path.Combine(env.ContentRootPath, "App_Data", "uploads");

    public IReadOnlyList<StoredFile> List(int orderId)
    {
        var dir = OrderDir(orderId);
        if (!Directory.Exists(dir))
        {
            return Array.Empty<StoredFile>();
        }
        return new DirectoryInfo(dir).GetFiles()
            .OrderBy(f => f.Name)
            .Select(f => new StoredFile(f.Name, f.Length))
            .ToList();
    }

    public async Task SaveAsync(int orderId, string fileName, Stream content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(OrderDir(orderId));
        await using var target = File.Create(PathFor(orderId, fileName));
        await content.CopyToAsync(target, ct);
    }

    public bool Delete(int orderId, string fileName)
    {
        var path = PathFor(orderId, fileName);
        if (!File.Exists(path))
        {
            return false;
        }
        File.Delete(path);
        return true;
    }

    public Stream Open(int orderId, string fileName)
    {
        var path = PathFor(orderId, fileName);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    // GetFileName drops any directory part, so a name cannot escape the order folder.
    private string PathFor(int orderId, string fileName) =>
        Path.Combine(OrderDir(orderId), Path.GetFileName(fileName ?? string.Empty));

    private string OrderDir(int orderId) => Path.Combine(_root, orderId.ToString());
}
