using System.IO;
using System.IO.Compression;
using Box.Automation.WSD;
using Box.V2.Models;

namespace ClaudeMd.Services;

/// <summary>
/// Updates one skill folder inside the release zip on Box: downloads v1.0.0.0.zip, puts Storage/&lt;folder&gt;/SKILL.md into it
/// and uploads the zip as a NEW VERSION of the same Box file (same id, Box keeps the older versions).
/// Same login the App Store's BoxFast helper uses (Box.Automation.WSD + Box.V2).
/// </summary>
public static class BoxUploader
{
    public const string FolderId = "408168561389";     // https://broadcom.ent.box.com/folder/408168561389
    public const string FileId = "2401498931156";      // https://broadcom.ent.box.com/file/2401498931156  (v1.0.0.0.zip)
    private const string UserToken = "8826064036";     // same service user as BoxFast

    public enum UploadMode { Update, AddNew }

    public sealed record Result(string ZipName, string Entry, bool Created, string? ModifiedAt, string? ModifiedBy);

    public static async Task<Result> UploadSkillAsync(string folder, byte[] skillMd, UploadMode mode, Action<string> status)
    {
        status("Signing in to Box...");
        var client = await new BoxUserClient().GetUserBoxClient(UserToken);

        var info = await client.FilesManager.GetInformationAsync(FileId, new[] { "name", "etag" });
        status($"Downloading {info.Name}...");
        var ms = new MemoryStream();
        using (var s = await client.FilesManager.DownloadAsync(FileId)) await s.CopyToAsync(ms);
        ms.Position = 0;

        var (entryPath, created) = PatchZip(ms, folder, skillMd, mode);
        ms.Position = 0;

        status("Uploading the new version to Box...");
        // etag = "only if nobody changed the zip since I downloaded it" (Box answers 412 otherwise)
        var file = await client.FilesManager.UploadNewVersionAsync(info.Name, FileId, ms, etag: info.ETag,
            fields: new List<string> { "name", "modified_at", "modified_by" });
        return new Result(info.Name, entryPath, created, file.ModifiedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), file.ModifiedBy?.Name);
    }

    /// <summary>Writes Storage/&lt;folder&gt;/SKILL.md into the zip (inside its single top-level folder). Returns (entry path, created).</summary>
    public static (string Entry, bool Created) PatchZip(Stream zipStream, string folder, byte[] skillMd, UploadMode mode = UploadMode.Update)
    {
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Update, leaveOpen: true);
        var root = zip.Entries.Select(e => e.FullName).Where(n => n.Contains("/Storage/")).Select(n => n[..n.IndexOf('/')]).FirstOrDefault()
                   ?? zip.Entries.Select(e => e.FullName).Where(n => n.Contains('/')).Select(n => n[..n.IndexOf('/')]).FirstOrDefault()
                   ?? "";
        var prefix = root.Length > 0 ? root + "/" : "";
        var dir = $"{prefix}Storage/{folder}/";
        var path = dir + "SKILL.md";

        var existing = zip.GetEntry(path);
        if (mode == UploadMode.AddNew && existing is not null)
            throw new InvalidOperationException($"{folder} already exists in the Box zip. Use 'Update existing' to replace it, or pick another name.");
        if (mode == UploadMode.Update && existing is null)
            throw new InvalidOperationException($"{folder} is not in the Box zip yet. Use 'Add new' to add it.");
        existing?.Delete();
        if (zip.GetEntry(dir) is null) zip.CreateEntry(dir);
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using (var w = entry.Open()) w.Write(skillMd, 0, skillMd.Length);
        return (path, existing is null);
    }
}
