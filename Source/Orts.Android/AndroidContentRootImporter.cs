using Android.Content;
using Android.Net;
using Android.Provider;
using System.Diagnostics;
using System.IO;
using Uri = Android.Net.Uri;

namespace Orts.Android;

internal sealed record ImportResult(string RootPath, long FilesCopied, long BytesCopied, TimeSpan Duration);

internal static class AndroidContentRootImporter
{
    const int BufferSize = 1024 * 1024;

    public static ImportResult Import(
        ContentResolver resolver,
        Uri treeUri,
        string destinationParent,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationParent);
        var stagingPath = Path.Combine(destinationParent, ".import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingPath);
        var stopwatch = Stopwatch.StartNew();
        long filesCopied = 0;
        long bytesCopied = 0;
        try
        {
            var rootDocumentId = DocumentsContract.GetTreeDocumentId(treeUri);
            if (string.IsNullOrEmpty(rootDocumentId))
                throw new IOException("Android did not provide a document identifier for the selected folder.");
            var routes = AndroidContentRootScanner.ReadRoutes(resolver, treeUri);
            if (routes.Count == 0)
                throw new DirectoryNotFoundException("The selected MSTS installation does not contain any route folders.");

            CopyDirectory(resolver, treeUri, rootDocumentId, stagingPath, progress, cancellationToken, ref filesCopied, ref bytesCopied);

            var importPath = Path.Combine(destinationParent, "MSTS-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            if (Directory.Exists(importPath))
                importPath += "-" + Guid.NewGuid().ToString("N");
            Directory.Move(stagingPath, importPath);
            stopwatch.Stop();
            return new ImportResult(importPath, filesCopied, bytesCopied, stopwatch.Elapsed);
        }
        catch
        {
            try
            {
                Directory.Delete(stagingPath, recursive: true);
            }
            catch (IOException cleanupError)
            {
                global::Android.Util.Log.Warn(nameof(AndroidContentRootImporter), $"Could not remove incomplete import '{stagingPath}': {cleanupError.Message}");
            }
            catch (UnauthorizedAccessException cleanupError)
            {
                global::Android.Util.Log.Warn(nameof(AndroidContentRootImporter), $"Could not remove incomplete import '{stagingPath}': {cleanupError.Message}");
            }
            throw;
        }
    }

    static void CopyDirectory(
        ContentResolver resolver,
        Uri treeUri,
        string sourceDocumentId,
        string destinationPath,
        IProgress<string> progress,
        CancellationToken cancellationToken,
        ref long filesCopied,
        ref long bytesCopied)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var child in AndroidContentRootScanner.ReadChildren(resolver, treeUri, sourceDocumentId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationName = ValidateEntryName(child.Name);
            var childPath = Path.Combine(destinationPath, destinationName);
            if (child.MimeType == DocumentsContract.Document.MimeTypeDir)
            {
                Directory.CreateDirectory(childPath);
                CopyDirectory(resolver, treeUri, child.Id, childPath, progress, cancellationToken, ref filesCopied, ref bytesCopied);
                continue;
            }

            var documentUri = DocumentsContract.BuildDocumentUriUsingTree(treeUri, child.Id);
            if (documentUri == null)
                throw new IOException($"Android could not create a readable document URI for '{child.Name}'.");
            using var source = resolver.OpenInputStream(documentUri)
                ?? throw new IOException($"Android could not open '{child.Name}' for reading.");
            using var destination = new FileStream(childPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.SequentialScan);
            var buffer = new byte[BufferSize];
            int bytesRead;
            long lastProgressBytes = bytesCopied;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                destination.Write(buffer, 0, bytesRead);
                bytesCopied += bytesRead;
                if (bytesCopied - lastProgressBytes >= 32L * BufferSize)
                {
                    progress.Report($"Importing MSTS content: {filesCopied} files, {bytesCopied / (1024d * 1024d):F0} MiB copied.");
                    lastProgressBytes = bytesCopied;
                }
            }
            filesCopied++;
            if (filesCopied == 1 || filesCopied % 25 == 0)
                progress.Report($"Importing MSTS content: {filesCopied} files copied.");
        }
    }

    static string ValidateEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar) || name.Contains('\\'))
            throw new IOException($"The selected content contains an unsafe or unsupported file name: '{name}'.");
        return name;
    }
}
