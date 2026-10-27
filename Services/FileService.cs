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

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);

        var storedFile = new StoredFile
        {
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            Content = memoryStream.ToArray(),
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
