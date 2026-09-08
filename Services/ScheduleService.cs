using StudentManagement.Models;

namespace StudentManagement.Services;

public class ScheduleService : IScheduleService
{
    private readonly List<DoctorSchedule> schedules = [];
    private readonly IDoctorService doctorService;

    public ScheduleService(IDoctorService doctorService) { this.doctorService = doctorService; }

    public List<DoctorSchedule> Get(int doctorId) => schedules.Where(item => item.DoctorId == doctorId).OrderBy(item => item.DayOfWeek).ToList();

    public DoctorSchedule Add(int doctorId, DoctorSchedule schedule)
    {
        doctorService.GetById(doctorId);
        schedule.Id = schedules.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1;
        schedule.DoctorId = doctorId;
        schedules.Add(schedule);
        return schedule;
    }

    public void Remove(int id)
    {
        var schedule = schedules.FirstOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("Không tìm thấy lịch làm việc.");
        schedules.Remove(schedule);
    }
}