using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TelerikDemo2.Common;
using TelerikDemo2.Domain;
using TelerikDemo2.Infrastructure.Persistence;
using TelerikDemo2.Infrastructure.Storage;

namespace TelerikDemo2.Features.Orders;


// Plain stream instead of IFormFile so this handler does not depend on ASP.NET.
public sealed record AttachmentUpload(string FileName, long Length, Stream Content);

public sealed record UploadAttachments(int OrderId, IReadOnlyList<AttachmentUpload> Files) : IRequest<Result>;

public sealed class UploadAttachmentsHandler(AppDbContext db, IAttachmentStore store, IOptions<AttachmentOptions> options)
    : IRequestHandler<UploadAttachments, Result>
{
    public async Task<Result> HandleAsync(UploadAttachments request, CancellationToken ct)
    {
        if (!await db.Orders.AnyAsync(o => o.Id == request.OrderId, ct))
        {
            return Result.Fail(string.Empty, OrderRules.NotFoundMessage);
        }

        var rules = options.Value;
        foreach (var file in request.Files)
        {
            var name = Path.GetFileName(file.FileName);
            if (file.Length == 0)
            {
                return Result.Fail("files", "No file was received.");
            }
            if (file.Length > rules.MaxBytes)
            {
                return Result.Fail("files", $"File is larger than {rules.MaxBytes / 1024 / 1024} MB.");
            }
            if (!rules.AllowedExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            {
                return Result.Fail("files", "Allowed types: " + string.Join(", ", rules.AllowedExtensions));
            }
        }

        foreach (var file in request.Files)
        {
            await store.SaveAsync(request.OrderId, Path.GetFileName(file.FileName), file.Content, ct);
        }
        return Result.Ok();
    }
}


public sealed record RemoveAttachments(int OrderId, IReadOnlyList<string> FileNames) : IRequest<Result>;

public sealed class RemoveAttachmentsHandler(IAttachmentStore store) : IRequestHandler<RemoveAttachments, Result>
{
    public Task<Result> HandleAsync(RemoveAttachments request, CancellationToken ct)
    {
        foreach (var name in request.FileNames)
        {
            store.Delete(request.OrderId, name);
        }
        return Task.FromResult(Result.Ok());
    }
}


// Null when the file does not exist. The caller disposes the stream.
public sealed record DownloadAttachment(int OrderId, string FileName) : IRequest<AttachmentDownload>;

public sealed record AttachmentDownload(string FileName, Stream Content);

public sealed class DownloadAttachmentHandler(IAttachmentStore store) : IRequestHandler<DownloadAttachment, AttachmentDownload>
{
    public Task<AttachmentDownload> HandleAsync(DownloadAttachment request, CancellationToken ct)
    {
        var stream = store.Open(request.OrderId, request.FileName);
        return Task.FromResult(stream is null ? null : new AttachmentDownload(Path.GetFileName(request.FileName), stream));
    }
}
