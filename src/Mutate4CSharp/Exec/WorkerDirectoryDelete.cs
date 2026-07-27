namespace Microsoft.Mutate4CSharp.Exec;

/// <summary>
/// Faithful port of mutate4java's package-private <c>WorkerDirectoryDelete</c>: recursively deletes a
/// worker run directory, surfacing an <see cref="IOException"/> when a file or directory cannot be
/// removed. mutate4java hand-walks the tree with <c>Files.walkFileTree</c> + <c>deleteIfExists</c>; the
/// .NET equivalent is a recursive delete, which likewise throws <see cref="IOException"/> when the OS
/// still holds a handle (e.g. a testhost process keeping a copied DLL open after <c>dotnet test</c>).
/// The retry wrapper in <see cref="WorkerCleanup"/> is what tolerates those transient locks.
/// </summary>
public sealed class WorkerDirectoryDelete
{
    /// <summary>
    /// Recursively deletes <paramref name="root"/> and everything beneath it.
    /// </summary>
    /// <param name="root">The directory tree to delete.</param>
    /// <exception cref="IOException">A file or directory could not be deleted (e.g. it is locked).</exception>
    public void Delete(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Directory.Delete(root, recursive: true);
    }
}
