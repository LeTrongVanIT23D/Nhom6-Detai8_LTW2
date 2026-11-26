using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.DTOs.Common;
using StudentManagement.Models;

namespace StudentManagement.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext context;

    public UserRepository(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<PagedResult<User>> GetAllAsync(UserQueryParameters queryParams)
    {
        var query = context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var searchTerm = queryParams.SearchTerm.Trim();
            query = query.Where(user =>
                user.Username.Contains(searchTerm) ||
                user.Email.Contains(searchTerm) ||
                user.FullName.Contains(searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Role))
        {
            var role = queryParams.Role.Trim();
            query = query.Where(user => user.Role == role);
        }

        var totalCount = await query.CountAsync();
        var orderedQuery = queryParams.SortBy?.Trim().ToLowerInvariant() switch
        {
            "fullname" => queryParams.IsDescending
                ? query.OrderByDescending(user => user.FullName).ThenBy(user => user.Id)
                : query.OrderBy(user => user.FullName).ThenBy(user => user.Id),
            "email" => queryParams.IsDescending
                ? query.OrderByDescending(user => user.Email).ThenBy(user => user.Id)
                : query.OrderBy(user => user.Email).ThenBy(user => user.Id),
            "role" => queryParams.IsDescending
                ? query.OrderByDescending(user => user.Role).ThenBy(user => user.Id)
                : query.OrderBy(user => user.Role).ThenBy(user => user.Id),
            "createdat" => queryParams.IsDescending
                ? query.OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
                : query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id),
            _ => queryParams.IsDescending
                ? query.OrderByDescending(user => user.Username).ThenBy(user => user.Id)
                : query.OrderBy(user => user.Username).ThenBy(user => user.Id)
        };

        var skip = (long)(queryParams.PageNumber - 1) * queryParams.PageSize;
        var items = skip >= totalCount
            ? new List<User>()
            : await orderedQuery
                .Skip((int)skip)
                .Take(queryParams.PageSize)
                .ToListAsync();

        return new PagedResult<User>(items, totalCount, queryParams.PageNumber, queryParams.PageSize);
    }

    public User? GetById(int id) => context.Users.FirstOrDefault(user => user.Id == id);

    public bool UsernameExists(string username)
    {
        var normalizedUsername = username.Trim().ToLower();
        return context.Users.Any(user => user.Username.ToLower() == normalizedUsername);
    }

    public bool DoctorExists(int doctorId) => context.Doctors.Any(doctor => doctor.Id == doctorId);

    public bool DoctorAssignedToAnotherUser(int doctorId, int userId) =>
        context.Users.Any(user => user.DoctorId == doctorId && user.Id != userId);

    public void LinkDoctorUser(int? doctorId, int userId)
    {
        var prevDocs = context.Doctors.Where(d => d.UserId == userId).ToList();
        foreach (var d in prevDocs) d.UserId = null;

        if (doctorId.HasValue)
        {
            var doc = context.Doctors.FirstOrDefault(d => d.Id == doctorId.Value);
            if (doc != null) doc.UserId = userId;
        }
    }

    public void Add(User user) => context.Users.Add(user);

    public void Remove(User user) => context.Users.Remove(user);

    public void SaveChanges() => context.SaveChanges();
}
