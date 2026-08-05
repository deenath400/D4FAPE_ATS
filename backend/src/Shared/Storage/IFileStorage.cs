namespace Ats.Shared.Storage;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Persists and retrieves opaque file content addressed by a server-generated storage key.
/// Callers never derive a storage key from client-supplied input (coding-standards.md:
/// "Uploaded files are never served from a path the client controls").
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string storageKey, Stream content, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
