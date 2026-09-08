using StudentManagement.Models;

namespace StudentManagement.Services;

public class SupportService : ISupportService
{
    private readonly List<SupportMessage> messages = [];

    public List<SupportMessage> GetAll(int? userId = null)
    {
        var query = messages.AsEnumerable();
        if (userId.HasValue) query = query.Where(item => item.UserId == userId.Value);
        return query.OrderByDescending(item => item.CreatedAt).ToList();
    }

    public SupportMessage Add(int userId, SupportMessage message)
    {
        message.Id = messages.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1;
        message.UserId = userId;
        messages.Add(message);
        return message;
    }

    public void Reply(int id, string reply, SupportMessageStatus status)
    {
        var message = messages.FirstOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu hỗ trợ.");
        message.Reply = reply.Trim();
        message.Status = status;
    }
}