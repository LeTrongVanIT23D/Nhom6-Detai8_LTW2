namespace StudentManagement.DTOs.Common;

public class UserQueryParameters
{
    private const int MaxPageSize = 50;
    private int pageNumber = 1;
    private int pageSize = 10;

    public int PageNumber
    {
        get => pageNumber;
        set => pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => pageSize;
        set => pageSize = value < 1 ? 1 : Math.Min(value, MaxPageSize);
    }

    public string? SearchTerm { get; set; }
    public string? Role { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
}
