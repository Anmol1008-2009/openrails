using Android.Content;
using Android.Database;
using Android.Net;
using Android.Provider;
using System.IO;
using Uri = Android.Net.Uri;

namespace Orts.Android;

internal static class AndroidContentRootScanner
{
    internal readonly record struct ChildDocument(string Id, string Name, string MimeType);

    public static IReadOnlyList<string> ReadRoutes(ContentResolver resolver, Uri treeUri)
    {
        var rootDocumentId = DocumentsContract.GetTreeDocumentId(treeUri);
        if (string.IsNullOrEmpty(rootDocumentId))
            throw new IOException("Android did not provide a document identifier for the selected folder.");
        var rootChildren = ReadChildren(resolver, treeUri, rootDocumentId);
        if (!rootChildren.Any(child => child.Name.Equals("TRAINS", StringComparison.OrdinalIgnoreCase) && child.MimeType == DocumentsContract.Document.MimeTypeDir))
            throw new DirectoryNotFoundException("The selected folder does not contain a TRAINS directory.");

        var routesFolder = rootChildren.FirstOrDefault(child => child.Name.Equals("ROUTES", StringComparison.OrdinalIgnoreCase) && child.MimeType == DocumentsContract.Document.MimeTypeDir);
        if (string.IsNullOrEmpty(routesFolder.Id))
            throw new DirectoryNotFoundException("The selected folder does not contain a ROUTES directory.");

        return ReadChildren(resolver, treeUri, routesFolder.Id)
            .Where(child => child.MimeType == DocumentsContract.Document.MimeTypeDir)
            .Select(child => child.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal static IReadOnlyList<ChildDocument> ReadChildren(ContentResolver resolver, Uri treeUri, string parentDocumentId)
    {
        var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, parentDocumentId);
        if (childrenUri == null)
            throw new IOException("Android could not create a child-folder query for the selected installation.");
        using ICursor? cursor = resolver.Query(childrenUri, new[]
        {
            DocumentsContract.Document.ColumnDocumentId,
            DocumentsContract.Document.ColumnDisplayName,
            DocumentsContract.Document.ColumnMimeType,
        }, null, null, null);
        if (cursor == null)
            throw new IOException("Android could not list the selected folder.");

        var idColumn = cursor.GetColumnIndexOrThrow(DocumentsContract.Document.ColumnDocumentId);
        var nameColumn = cursor.GetColumnIndexOrThrow(DocumentsContract.Document.ColumnDisplayName);
        var mimeTypeColumn = cursor.GetColumnIndexOrThrow(DocumentsContract.Document.ColumnMimeType);
        var children = new List<ChildDocument>();
        while (cursor.MoveToNext())
        {
            children.Add(new ChildDocument(
                cursor.GetString(idColumn) ?? string.Empty,
                cursor.GetString(nameColumn) ?? string.Empty,
                cursor.GetString(mimeTypeColumn) ?? string.Empty));
        }
        return children;
    }
}
