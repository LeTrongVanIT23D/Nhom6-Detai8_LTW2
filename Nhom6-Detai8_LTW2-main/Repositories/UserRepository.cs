using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public List<User> GetAll() => _db.Users.AsNoTracking().OrderBy(user => user.Id).ToList();

    public async Task<PagedResult<User>> GetAllAsync(UserQueryParameters queryParams)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();

        // 1. SEARCH (Tìm kiếm tương đối)
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(term)
                                  || u.Email.ToLower().Contains(term));
        }

        // 2. FILTER (Lọc chính xác)
        if (!string.IsNullOrWhiteSpace(queryParams.Role))
        {
            query = query.Where(u => u.Role == queryParams.Role);
        }

        //if (queryParams.IsActive.HasValue)
        //{
        //    query = query.Where(u => u.IsActive == queryParams.IsActive.Value);
        //}

        // 3. SORTING
        query = queryParams.SortBy?.ToLower() switch
        {
            "email" => queryParams.IsDescending
                ? query.OrderByDescending(u => u.Email)
                : query.OrderBy(u => u.Email),

            "createdat" => queryParams.IsDescending
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt),

            _ => queryParams.IsDescending
                ? query.OrderByDescending(u => u.Username)
                : query.OrderBy(u => u.Username) // Mặc định sort theo UserName
        };

        // 4. COUNT (Tổng số dòng thỏa điều kiện search/filter)
        var totalCount = await query.CountAsync();

        // 5. PAGINATION (Chỉ lấy đúng PageSize dòng từ DB)
        var items = await query
            .Skip((queryParams.PageNumber - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<User>(items, totalCount, queryParams.PageNumber, queryParams.PageSize);
    }

    public User? GetById(int id) => _db.Users.FirstOrDefault(user => user.Id == id);

    public bool UsernameExists(string username)
    {
        var normalizedUsername = username.Trim().ToLower();
        return _db.Users.Any(user => user.Username.ToLower() == normalizedUsername);
    }

    public bool DoctorExists(int doctorId) => _db.Doctors.Any(doctor => doctor.Id == doctorId);

    public bool DoctorAssignedToAnotherUser(int doctorId, int userId) =>
        _db.Users.Any(user => user.DoctorId == doctorId && user.Id != userId);

    public void Add(User user) => _db.Users.Add(user);

    public void Remove(User user) => _db.Users.Remove(user);

    public void SaveChanges() => _db.SaveChanges();
}
