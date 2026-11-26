namespace StudentManagement.DTOs.Common;

public class PageResult<T> : PagedResult<T>
{
    public PageResult(List<T> items, int totalCount, int pageNumber, int pageSize)
        : base(items, totalCount, pageNumber, pageSize)
    {
    }
}

