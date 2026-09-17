using MoeezMobile.Api.Models.Common;

namespace MoeezMobile.Api.Services;

public interface IFileStorageService
{
    /// <summary>Saves a product image and returns its relative URL (e.g. /uploads/products/xxx.jpg).</summary>
    Task<string> SaveProductImageAsync(IFormFile file, CancellationToken ct = default);

    void DeleteIfExists(string? relativeUrl);
}

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly string _folder;
    private readonly long _maxBytes;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    public FileStorageService(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _folder = config["Uploads:ProductImagesPath"] ?? "uploads/products";
        _maxBytes = long.TryParse(config["Uploads:MaxImageBytes"], out var v) ? v : 2 * 1024 * 1024;
    }

    public async Task<string> SaveProductImageAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0)
            throw new BusinessException(MessageKeys.ImageEmpty);

        if (file.Length > _maxBytes)
            throw new BusinessException(MessageKeys.ImageTooLarge, _maxBytes / (1024 * 1024));

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedTypes.ContainsKey(ext))
            throw new BusinessException(MessageKeys.ImageWrongType);

        await using (var probe = file.OpenReadStream())
        {
            if (!await HasImageSignatureAsync(probe, ext, ct))
                throw new BusinessException(MessageKeys.ImageNotValid);
        }

        var root = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;

        var directory = Path.Combine(root, _folder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(directory, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream, ct);
        }

        return $"/{_folder.Trim('/')}/{fileName}";
    }

    public void DeleteIfExists(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return;

        // Only ever delete inside the configured uploads folder.
        var expectedPrefix = $"/{_folder.Trim('/')}/";
        if (!relativeUrl.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)) return;

        var fileName = Path.GetFileName(relativeUrl);
        if (string.IsNullOrWhiteSpace(fileName)) return;

        var root = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;

        var fullPath = Path.Combine(root, _folder.Replace('/', Path.DirectorySeparatorChar), fileName);

        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (IOException)
        {
            // A stale image on disk is not worth failing the request over.
        }
    }

    /// <summary>Checks the magic bytes so a renamed .exe cannot be stored as a product image.</summary>
    private static async Task<bool> HasImageSignatureAsync(Stream stream, string ext, CancellationToken ct)
    {
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
        if (read < 4) return false;

        return ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            ".webp" => read >= 12
                       && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                       && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            _ => false
        };
    }
}
