using Microsoft.AspNetCore.Http;
namespace Application.PL.Services;



public class FileService : IFileService
{
    private readonly IWebHostEnvironment _environment;
    public FileService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveFile(IFormFile file, string[] allowedExtensions)
    {
        var wwwPath = _environment.WebRootPath;

        // Store inside /images/products
        var path = Path.Combine(wwwPath, "images", "products");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var extension = Path.GetExtension(file.FileName);
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Only {string.Join(",", allowedExtensions)} files allowed");
        }

        string fileName = $"{Guid.NewGuid()}{extension}";
        string fileNameWithPath = Path.Combine(path, fileName);

        using var stream = new FileStream(fileNameWithPath, FileMode.Create);
        await file.CopyToAsync(stream);

        // Always return with leading slash for consistency
        return $"/images/products/{fileName}";
    }

    public void DeleteFile(string relativePath)
    {
        var wwwPath = _environment.WebRootPath;

        // Combine with wwwroot to get full path
        var filePath = Path.Combine(wwwPath, relativePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));

        if (!File.Exists(filePath))
            throw new FileNotFoundException(relativePath);

        File.Delete(filePath);
    }
}