using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Student_Management_System.Services;

public enum PrivateFileKind
{
    StudyMaterial,
    StudentPhoto,
    AdmissionDocument,
    AssignmentAttachment,
    ProfileImage
}

public sealed class PrivateFileStorageOptions
{
    public string RootPath { get; set; } = Path.Combine("App_Data", "PrivateUploads");
    public long MaxStudyMaterialBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxStudentPhotoBytes { get; set; } = 5 * 1024 * 1024;
    public long MaxEncodedStudentPhotoBytes { get; set; } = 3 * 1024 * 1024;
    public int MaxStudentPhotoWidth { get; set; } = 4096;
    public int MaxStudentPhotoHeight { get; set; } = 4096;
}

public sealed class PrivateFileValidationException : Exception
{
    public PrivateFileValidationException(string message) : base(message) { }
    public PrivateFileValidationException(string message, Exception innerException)
        : base(message, innerException) { }
}

public sealed record StoredPrivateFile(
    string StorageKey,
    string ContentType,
    string Extension,
    long Length);

public sealed record OpenedPrivateFile(
    Stream Stream,
    string ContentType,
    string Extension,
    long Length);

public interface IPrivateFileStorage
{
    Task<StoredPrivateFile> SaveStudyMaterialAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<StoredPrivateFile> SaveStudentPhotoAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<StoredPrivateFile> SaveProfileImageAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<StoredPrivateFile> SaveAdmissionDocumentAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<StoredPrivateFile> SaveAssignmentAttachmentAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<OpenedPrivateFile?> OpenReadAsync(
        string? storageKey,
        PrivateFileKind kind,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string? storageKey,
        PrivateFileKind kind,
        CancellationToken cancellationToken = default);

    Task MigrateLegacyFilesAsync(CancellationToken cancellationToken = default);
}

public sealed class PrivateFileStorage : IPrivateFileStorage
{
    private const string MaterialsFolder = "materials";
    private const string StudentPhotosFolder = "students";
    private const string AdmissionDocumentsFolder = "admissions";
    private const string AssignmentAttachmentsFolder = "assignments";
    private const string ProfileImagesFolder = "profiles";
    private const long MaxExpandedOfficeDocumentBytes = 100 * 1024 * 1024;
    private const int MaxOfficeDocumentEntries = 5_000;

    private static readonly IReadOnlyDictionary<string, (string ContentType, string RequiredEntry)>
        OfficeDocumentTypes = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            [".docx"] = (
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "word/document.xml"),
            [".pptx"] = (
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                "ppt/presentation.xml")
        };

    private readonly PrivateFileStorageOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PrivateFileStorage> _logger;
    private readonly IUploadMalwareScanner _malwareScanner;
    private readonly string _rootPath;
    private readonly string _stagingPath;

    public PrivateFileStorage(
        IOptions<PrivateFileStorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<PrivateFileStorage> logger,
        IUploadMalwareScanner malwareScanner)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
        _malwareScanner = malwareScanner;

        if (string.IsNullOrWhiteSpace(_options.RootPath))
            throw new InvalidOperationException("PrivateUploads:RootPath must be configured.");
        if (_options.MaxStudyMaterialBytes <= 0 ||
            _options.MaxStudentPhotoBytes <= 0 ||
            _options.MaxEncodedStudentPhotoBytes <= 0)
            throw new InvalidOperationException("Private upload size limits must be positive.");
        if (_options.MaxStudentPhotoWidth <= 0 || _options.MaxStudentPhotoHeight <= 0)
            throw new InvalidOperationException("Private upload image dimensions must be positive.");

        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(_options.RootPath)
                ? _options.RootPath
                : Path.Combine(_environment.ContentRootPath, _options.RootPath));
        _stagingPath = Path.Combine(_rootPath, ".staging");

        var webRoot = Path.GetFullPath(
            _environment.WebRootPath ??
            Path.Combine(_environment.ContentRootPath, "wwwroot"));
        if (IsWithinDirectory(_rootPath, webRoot))
            throw new InvalidOperationException(
                "PrivateUploads:RootPath must be outside the web root.");

        Directory.CreateDirectory(Path.Combine(_rootPath, MaterialsFolder));
        Directory.CreateDirectory(Path.Combine(_rootPath, StudentPhotosFolder));
        Directory.CreateDirectory(Path.Combine(_rootPath, AdmissionDocumentsFolder));
        Directory.CreateDirectory(Path.Combine(_rootPath, AssignmentAttachmentsFolder));
        Directory.CreateDirectory(Path.Combine(_rootPath, ProfileImagesFolder));
        Directory.CreateDirectory(_stagingPath);
    }

    public async Task<StoredPrivateFile> SaveStudyMaterialAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        if (extension != ".pdf" && !OfficeDocumentTypes.ContainsKey(extension))
            throw new PrivateFileValidationException(
                "Only PDF, DOCX, and PPTX files are allowed.");

        var bytes = await ReadUploadAsync(
            file,
            _options.MaxStudyMaterialBytes,
            "Study material",
            cancellationToken);
        var contentType = ValidateStudyMaterial(bytes, extension);
        var key = CreateRandomStorageKey(PrivateFileKind.StudyMaterial, extension);
        await WriteAtomicAsync(key, bytes, cancellationToken);
        return new StoredPrivateFile(key, contentType, extension, bytes.LongLength);
    }

    public async Task<StoredPrivateFile> SaveStudentPhotoAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var suppliedExtension = NormalizeImageExtension(
            Path.GetExtension(Path.GetFileName(file.FileName)));
        if (suppliedExtension is null)
            throw new PrivateFileValidationException(
                "Only JPEG, PNG, and WebP photos are allowed.");

        var bytes = await ReadUploadAsync(
            file,
            _options.MaxStudentPhotoBytes,
            "Photo",
            cancellationToken);
        var normalizedBytes = NormalizeStudentPhoto(bytes, suppliedExtension, ".jpg");
        if (normalizedBytes.LongLength > _options.MaxEncodedStudentPhotoBytes)
            throw new PrivateFileValidationException(
                "The processed photo is too large. Use a smaller or less detailed image.");

        var key = CreateRandomStorageKey(PrivateFileKind.StudentPhoto, ".jpg");
        await WriteAtomicAsync(key, normalizedBytes, cancellationToken);
        return new StoredPrivateFile(
            key,
            "image/jpeg",
            ".jpg",
            normalizedBytes.LongLength);
    }

    public async Task<StoredPrivateFile> SaveProfileImageAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var suppliedExtension = NormalizeImageExtension(
            Path.GetExtension(Path.GetFileName(file.FileName)));
        if (suppliedExtension is null)
            throw new PrivateFileValidationException(
                "Only JPEG, PNG, and WebP profile images are allowed.");
        var bytes = await ReadUploadAsync(
            file, _options.MaxStudentPhotoBytes, "Profile image", cancellationToken);
        var normalizedBytes = NormalizeStudentPhoto(bytes, suppliedExtension, ".jpg");
        if (normalizedBytes.LongLength > _options.MaxEncodedStudentPhotoBytes)
            throw new PrivateFileValidationException(
                "The processed profile image is too large.");
        var key = CreateRandomStorageKey(PrivateFileKind.ProfileImage, ".jpg");
        await WriteAtomicAsync(key, normalizedBytes, cancellationToken);
        return new StoredPrivateFile(
            key, "image/jpeg", ".jpg", normalizedBytes.LongLength);
    }

    public async Task<StoredPrivateFile> SaveAdmissionDocumentAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        if (extension != ".pdf" && !OfficeDocumentTypes.ContainsKey(extension))
            throw new PrivateFileValidationException("Only PDF, DOCX, and PPTX documents are allowed.");
        var bytes = await ReadUploadAsync(
            file, _options.MaxStudyMaterialBytes, "Admission document", cancellationToken);
        var contentType = ValidateStudyMaterial(bytes, extension);
        var key = CreateRandomStorageKey(PrivateFileKind.AdmissionDocument, extension);
        await WriteAtomicAsync(key, bytes, cancellationToken);
        return new StoredPrivateFile(key, contentType, extension, bytes.LongLength);
    }

    public async Task<StoredPrivateFile> SaveAssignmentAttachmentAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        if (extension != ".pdf" && !OfficeDocumentTypes.ContainsKey(extension))
            throw new PrivateFileValidationException(
                "Assignment attachments must be PDF, DOCX, or PPTX files.");
        var bytes = await ReadUploadAsync(
            file, _options.MaxStudyMaterialBytes, "Assignment attachment", cancellationToken);
        var contentType = ValidateStudyMaterial(bytes, extension);
        var key = CreateRandomStorageKey(PrivateFileKind.AssignmentAttachment, extension);
        await WriteAtomicAsync(key, bytes, cancellationToken);
        return new StoredPrivateFile(key, contentType, extension, bytes.LongLength);
    }

    public Task<OpenedPrivateFile?> OpenReadAsync(
        string? storageKey,
        PrivateFileKind kind,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolveStoragePath(storageKey, kind, out var path, out var extension))
            return Task.FromResult<OpenedPrivateFile?>(null);
        if (!File.Exists(path))
            return Task.FromResult<OpenedPrivateFile?>(null);

        var contentType = GetContentType(extension, kind);
        if (contentType is null)
            return Task.FromResult<OpenedPrivateFile?>(null);

        Stream stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Access = FileAccess.Read,
                Mode = FileMode.Open,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });
        return Task.FromResult<OpenedPrivateFile?>(
            new OpenedPrivateFile(stream, contentType, extension, stream.Length));
    }

    public Task DeleteAsync(
        string? storageKey,
        PrivateFileKind kind,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolveStoragePath(storageKey, kind, out var path, out _))
            return Task.CompletedTask;
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task MigrateLegacyFilesAsync(
        CancellationToken cancellationToken = default)
    {
        await MigrateLegacyFolderAsync(
            PrivateFileKind.StudyMaterial,
            _options.MaxStudyMaterialBytes,
            cancellationToken);
        await MigrateLegacyFolderAsync(
            PrivateFileKind.StudentPhoto,
            _options.MaxStudentPhotoBytes,
            cancellationToken);
    }

    private async Task MigrateLegacyFolderAsync(
        PrivateFileKind kind,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var folder = GetFolder(kind);
        var legacyRoot = Path.Combine(
            _environment.WebRootPath ??
            Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads",
            folder);
        if (!Directory.Exists(legacyRoot))
            return;

        foreach (var sourcePath in Directory.EnumerateFiles(
                     legacyRoot,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var fileInfo = new FileInfo(sourcePath);
                if (fileInfo.Length <= 0 || fileInfo.Length > maxBytes)
                {
                    _logger.LogWarning(
                        "Skipped invalid legacy private file {FileName}: size is outside configured limits.",
                        fileInfo.Name);
                    continue;
                }

                var extension = kind == PrivateFileKind.StudentPhoto
                    ? NormalizeImageExtension(fileInfo.Extension)
                    : fileInfo.Extension.ToLowerInvariant();
                if (extension is null)
                {
                    _logger.LogWarning(
                        "Skipped legacy private file {FileName}: extension is not permitted.",
                        fileInfo.Name);
                    continue;
                }

                var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
                byte[] bytesToStore;
                if (kind is PrivateFileKind.StudyMaterial or PrivateFileKind.AdmissionDocument)
                {
                    ValidateStudyMaterial(bytes, extension);
                    bytesToStore = bytes;
                }
                else
                {
                    bytesToStore = NormalizeStudentPhoto(bytes, extension, extension);
                    if (bytesToStore.LongLength > _options.MaxEncodedStudentPhotoBytes)
                    {
                        _logger.LogWarning(
                            "Skipped legacy student photo {FileName}: processed image is too large.",
                            fileInfo.Name);
                        continue;
                    }
                }

                var storageKey = $"{folder}/{fileInfo.Name}";
                if (!TryResolveStoragePath(storageKey, kind, out var destinationPath, out _))
                    continue;
                if (!File.Exists(destinationPath))
                    await WriteAtomicAsync(storageKey, bytesToStore, cancellationToken);

                File.Delete(sourcePath);
                _logger.LogInformation(
                    "Migrated legacy private file {FileName} out of the web root.",
                    fileInfo.Name);
            }
            catch (PrivateFileValidationException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Skipped invalid legacy private file {FileName}.",
                    Path.GetFileName(sourcePath));
            }
            catch (IOException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not migrate legacy private file {FileName}; it remains blocked from static access.",
                    Path.GetFileName(sourcePath));
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not migrate legacy private file {FileName}; it remains blocked from static access.",
                    Path.GetFileName(sourcePath));
            }
        }
    }

    private byte[] NormalizeStudentPhoto(
        byte[] bytes,
        string suppliedExtension,
        string outputExtension)
    {
        try
        {
            var decoderOptions = new DecoderOptions { MaxFrames = 2 };
            using var source = new MemoryStream(bytes, writable: false);
            var detectedFormat = Image.DetectFormat(decoderOptions, source);
            var detectedExtension = GetDetectedImageExtension(detectedFormat);
            if (detectedExtension is null ||
                !ImageExtensionsMatch(suppliedExtension, detectedExtension))
                throw new PrivateFileValidationException(
                    "The photo contents do not match its file extension.");

            source.Position = 0;
            var imageInfo = Image.Identify(decoderOptions, source);
            if (imageInfo.Width <= 0 ||
                imageInfo.Height <= 0 ||
                imageInfo.Width > _options.MaxStudentPhotoWidth ||
                imageInfo.Height > _options.MaxStudentPhotoHeight)
                throw new PrivateFileValidationException(
                    $"Photo dimensions must not exceed " +
                    $"{_options.MaxStudentPhotoWidth} × {_options.MaxStudentPhotoHeight} pixels.");

            source.Position = 0;
            using var image = Image.Load(decoderOptions, source);
            if (image.Frames.Count != 1)
                throw new PrivateFileValidationException(
                    "Animated or multi-frame photos are not allowed.");

            image.Mutate(operation => operation.AutoOrient());
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            using var destination = new MemoryStream();
            IImageEncoder encoder = outputExtension switch
            {
                ".png" => new PngEncoder(),
                ".webp" => new WebpEncoder { Quality = 85 },
                _ => new JpegEncoder { Quality = 85 }
            };
            image.Save(destination, encoder);
            return destination.ToArray();
        }
        catch (PrivateFileValidationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PrivateFileValidationException(
                "The uploaded photo is not a valid decodable image.",
                exception);
        }
    }

    private static string ValidateStudyMaterial(byte[] bytes, string extension)
    {
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            if (bytes.Length < 10 ||
                !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
                throw new PrivateFileValidationException(
                    "The uploaded file is not a valid PDF document.");

            var tailStart = Math.Max(0, bytes.Length - 2_048);
            if (!Encoding.ASCII.GetString(bytes, tailStart, bytes.Length - tailStart)
                    .Contains("%%EOF", StringComparison.Ordinal))
                throw new PrivateFileValidationException(
                    "The uploaded PDF is incomplete or invalid.");
            return "application/pdf";
        }

        if (!OfficeDocumentTypes.TryGetValue(extension, out var documentType))
            throw new PrivateFileValidationException(
                "Only PDF, DOCX, and PPTX files are allowed.");
        if (bytes.Length < 4 ||
            bytes[0] != 0x50 ||
            bytes[1] != 0x4B ||
            bytes[2] != 0x03 ||
            bytes[3] != 0x04)
            throw new PrivateFileValidationException(
                "The uploaded Office document has an invalid file signature.");

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(
                stream,
                ZipArchiveMode.Read,
                leaveOpen: false,
                entryNameEncoding: Encoding.UTF8);
            if (archive.Entries.Count == 0 ||
                archive.Entries.Count > MaxOfficeDocumentEntries)
                throw new PrivateFileValidationException(
                    "The uploaded Office document has an invalid structure.");

            long expandedBytes = 0;
            var hasContentTypes = false;
            var hasRequiredEntry = false;
            foreach (var entry in archive.Entries)
            {
                expandedBytes = checked(expandedBytes + entry.Length);
                if (expandedBytes > MaxExpandedOfficeDocumentBytes)
                    throw new PrivateFileValidationException(
                        "The uploaded Office document expands beyond the safe limit.");
                if (entry.FullName.Equals(
                        "[Content_Types].xml",
                        StringComparison.Ordinal))
                    hasContentTypes = true;
                if (entry.FullName.Equals(
                        documentType.RequiredEntry,
                        StringComparison.Ordinal))
                    hasRequiredEntry = true;
            }

            if (!hasContentTypes || !hasRequiredEntry)
                throw new PrivateFileValidationException(
                    "The uploaded Office document does not match its file type.");
        }
        catch (PrivateFileValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or IOException or OverflowException)
        {
            throw new PrivateFileValidationException(
                "The uploaded Office document is invalid or corrupted.",
                exception);
        }

        return documentType.ContentType;
    }

    private static async Task<byte[]> ReadUploadAsync(
        IFormFile file,
        long maxBytes,
        string label,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
            throw new PrivateFileValidationException(
                $"{label} must not be empty.");
        if (file.Length > maxBytes)
            throw new PrivateFileValidationException(
                $"{label} exceeds the configured size limit.");

        await using var input = file.OpenReadStream();
        using var output = new MemoryStream(
            capacity: (int)Math.Min(file.Length, maxBytes));
        var buffer = new byte[81_920];
        long totalBytes = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            totalBytes += read;
            if (totalBytes > maxBytes)
                throw new PrivateFileValidationException(
                    $"{label} exceeds the configured size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (totalBytes == 0)
            throw new PrivateFileValidationException(
                $"{label} must not be empty.");
        return output.ToArray();
    }

    private async Task WriteAtomicAsync(
        string storageKey,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var kind = storageKey.StartsWith($"{MaterialsFolder}/", StringComparison.OrdinalIgnoreCase)
            ? PrivateFileKind.StudyMaterial
            : storageKey.StartsWith($"{AdmissionDocumentsFolder}/", StringComparison.OrdinalIgnoreCase)
                ? PrivateFileKind.AdmissionDocument
                : storageKey.StartsWith($"{AssignmentAttachmentsFolder}/", StringComparison.OrdinalIgnoreCase)
                    ? PrivateFileKind.AssignmentAttachment
                    : storageKey.StartsWith($"{ProfileImagesFolder}/", StringComparison.OrdinalIgnoreCase)
                        ? PrivateFileKind.ProfileImage
                        : PrivateFileKind.StudentPhoto;
        if (!TryResolveStoragePath(
                storageKey,
                kind,
                out var destinationPath,
                out _))
            throw new InvalidOperationException("Generated storage key is invalid.");

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var temporaryPath = Path.Combine(
            _stagingPath,
            $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(
                             temporaryPath,
                             new FileStreamOptions
                             {
                                 Access = FileAccess.Write,
                                 Mode = FileMode.CreateNew,
                                 Share = FileShare.None,
                                 Options = FileOptions.Asynchronous |
                                           FileOptions.WriteThrough
                             }))
            {
                await output.WriteAsync(bytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            try
            {
                await _malwareScanner.ScanAsync(temporaryPath, cancellationToken);
            }
            catch (MalwareScanException exception)
            {
                throw new PrivateFileValidationException(exception.Message, exception);
            }
            File.Move(temporaryPath, destinationPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private bool TryResolveStoragePath(
        string? storageKey,
        PrivateFileKind kind,
        out string path,
        out string extension)
    {
        path = "";
        extension = "";
        if (string.IsNullOrWhiteSpace(storageKey))
            return false;

        var normalized = storageKey.Trim().Replace('\\', '/');
        var legacyPrefix = kind switch
        {
            PrivateFileKind.StudyMaterial => "/uploads/materials/",
            PrivateFileKind.StudentPhoto => "/uploads/students/",
            PrivateFileKind.AdmissionDocument => "/uploads/admissions/",
            PrivateFileKind.AssignmentAttachment => "/uploads/assignments/",
            _ => "/uploads/profiles/"
        };
        if (normalized.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            normalized = normalized[legacyPrefix.Length..];
        else
        {
            normalized = normalized.TrimStart('/');
            var expectedPrefix = $"{GetFolder(kind)}/";
            if (!normalized.StartsWith(
                    expectedPrefix,
                    StringComparison.OrdinalIgnoreCase))
                return false;
            normalized = normalized[expectedPrefix.Length..];
        }

        if (normalized.Length == 0 ||
            normalized is "." or ".." ||
            normalized.Contains('/') ||
            normalized.Contains('\\') ||
            normalized.Contains("..", StringComparison.Ordinal) ||
            !string.Equals(
                normalized,
                Path.GetFileName(normalized),
                StringComparison.Ordinal))
            return false;

        extension = kind is PrivateFileKind.StudentPhoto or PrivateFileKind.ProfileImage
            ? NormalizeImageExtension(Path.GetExtension(normalized)) ?? ""
            : Path.GetExtension(normalized).ToLowerInvariant();
        if (GetContentType(extension, kind) is null)
            return false;

        var folderPath = Path.Combine(_rootPath, GetFolder(kind));
        var candidate = Path.GetFullPath(Path.Combine(folderPath, normalized));
        if (!IsWithinDirectory(candidate, folderPath))
            return false;

        path = candidate;
        return true;
    }

    private static string CreateRandomStorageKey(
        PrivateFileKind kind,
        string extension) =>
        $"{GetFolder(kind)}/{Guid.NewGuid():N}{extension}";

    private static string GetFolder(PrivateFileKind kind) =>
        kind switch
        {
            PrivateFileKind.StudyMaterial => MaterialsFolder,
            PrivateFileKind.StudentPhoto => StudentPhotosFolder,
            PrivateFileKind.AdmissionDocument => AdmissionDocumentsFolder,
            PrivateFileKind.AssignmentAttachment => AssignmentAttachmentsFolder,
            PrivateFileKind.ProfileImage => ProfileImagesFolder,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static string? GetContentType(
        string extension,
        PrivateFileKind kind)
    {
        if (kind is PrivateFileKind.StudyMaterial or
            PrivateFileKind.AdmissionDocument or
            PrivateFileKind.AssignmentAttachment)
        {
            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                return "application/pdf";
            return OfficeDocumentTypes.TryGetValue(extension, out var type)
                ? type.ContentType
                : null;
        }

        return NormalizeImageExtension(extension) switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
    }

    private static string? NormalizeImageExtension(string? extension) =>
        extension?.Trim().ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ".jpg",
            ".png" => ".png",
            ".webp" => ".webp",
            _ => null
        };

    private static string? GetDetectedImageExtension(IImageFormat? format)
    {
        if (format is null)
            return null;
        if (format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase))
            return ".jpg";
        if (format.Name.Equals("PNG", StringComparison.OrdinalIgnoreCase))
            return ".png";
        if (format.Name.Equals("WEBP", StringComparison.OrdinalIgnoreCase))
            return ".webp";
        return null;
    }

    private static bool ImageExtensionsMatch(
        string suppliedExtension,
        string detectedExtension) =>
        NormalizeImageExtension(suppliedExtension) ==
        NormalizeImageExtension(detectedExtension);

    private static bool IsWithinDirectory(
        string candidatePath,
        string directoryPath)
    {
        var normalizedDirectory = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return normalizedCandidate.StartsWith(normalizedDirectory, comparison);
    }
}
