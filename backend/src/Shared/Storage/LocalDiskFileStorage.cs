namespace Ats.Shared.Storage;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Local-disk-backed <see cref="IFileStorage"/> implementation. Resolves storage keys under a
/// single configured base directory; this is the ATS's first real `shared/storage`
/// implementation (0004 G-6), resolving `tech-stack.md`'s previously-TBD object storage row.
/// </summary>
public class LocalDiskFileStorage : IFileStorage
{
    private const string DefaultBasePath = "./app-data/cv-attachments";
    private readonly string _basePath;

    public LocalDiskFileStorage(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _basePath = configuration["Storage:CvBasePath"] ?? DefaultBasePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = ResolvePath(storageKey);
        await using var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    // Never trust a storage key as a path fragment (coding-standards.md: "Uploaded files are
    // never served from a path the client controls"). In practice the client never supplies a
    // storage key — ApplicationService always generates it — this is defence in depth.
    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            storageKey.Contains("..", StringComparison.Ordinal) ||
            storageKey.Contains('/') || storageKey.Contains('\\'))
        {
            throw new ArgumentException("Invalid storage key.", nameof(storageKey));
        }

        return Path.Combine(_basePath, storageKey);
    }
}
