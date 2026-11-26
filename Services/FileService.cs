using Microsoft.AspNetCore.Http;
using StudentManagement.Models;
using StudentManagement.Repositories;
using static StudentManagement.DTOs.FileUploadDbDto;

namespace StudentManagement.Services;

public class FileService : IFileService
{
    private readonly IFileRepository _fileRepository;
    private const long MaxFileSizeInBytes = 1 * 1024 * 1024; // 1 MB = 1,048,576 bytes

    public FileService(IFileRepository fileRepository)
    {
        _fileRepository = fileRepository;
    }

    public async Task<FileResponseDto> UploadFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File không hợp lệ.");

        if (file.Length > MaxFileSizeInBytes)
            throw new ArgumentException("Dung lượng file vượt quá giới hạn cho phép (tối đa 1MB).");

        if (file.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new ArgumentException("Chỉ chấp nhận ảnh JPEG, PNG hoặc WebP.");

        var fileName = Path.GetFileName(file.FileName);

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var fileContent = memoryStream.ToArray();

        if (!HasValidImageSignature(fileContent, file.ContentType))
            throw new ArgumentException("Nội dung file không khớp với định dạng ảnh đã chọn.");

        var storedFile = new StoredFile
        {
            FileName = fileName,
            ContentType = file.ContentType,
            Content = fileContent,
            Length = file.Length
        };

        var createdFile = await _fileRepository.AddAsync(storedFile);

        return new FileResponseDto
        {
            Id = createdFile.Id,
            FileName = createdFile.FileName,
            Size = createdFile.Length,
            UploadedAt = createdFile.UploadedAt
        };
    }

    private static bool HasValidImageSignature(byte[] content, string contentType) => contentType switch
    {
        "image/jpeg" => content.Length >= 3 &&
                        content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
        "image/png" => content.Length >= 8 &&
                       content.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/webp" => content.Length >= 12 &&
                        content.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                        content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    public async Task<FileDownloadDto?> GetFileForDownloadAsync(int id)
    {
        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null) return null;

        return new FileDownloadDto
        {
            Content = file.Content,
            ContentType = file.ContentType,
            FileName = file.FileName
        };
    }
}
