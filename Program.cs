using StudentManagement.Models;
using StudentManagement.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IPatientManager>(_ => new PatientManager(
[
	new() { Id = 1, FullName = "Nguyễn Văn An", DateOfBirth = new DateOnly(1990, 5, 12), Phone = "0901234567", Address = "Đà Nẵng", MedicalHistory = "Tăng huyết áp" },
	new() { Id = 2, FullName = "Trần Thị Bình", DateOfBirth = new DateOnly(1985, 10, 20), Phone = "0912345678", Address = "Quảng Nam", MedicalHistory = "Dị ứng penicillin" },
	new() { Id = 3, FullName = "Lê Minh Chi", DateOfBirth = new DateOnly(2000, 2, 8), Phone = "0923456789", Address = "Đà Nẵng", MedicalHistory = "Không" }
]));
builder.Services.AddSingleton<IDoctorService>(_ => new DoctorService(
[
	new() { Id = 1, FullName = "BS. Nguyễn Minh Tuấn", Specialty = "Nội khoa", Phone = "0901111222", Room = "P.101" },
	new() { Id = 2, FullName = "BS. Lê Thu Hà", Specialty = "Nhi khoa", Phone = "0903333444", Room = "P.102" },
	new() { Id = 3, FullName = "BS. Trần Quốc Bảo", Specialty = "Răng hàm mặt", Phone = "0905555666", Room = "P.103" }
]));
builder.Services.AddSingleton<IAppointmentService>(serviceProvider => new AppointmentService(
[
	new() { Id = 1, PatientId = 1, DoctorId = 1, AppointmentDate = DateTime.Today.AddDays(1).AddHours(9), Reason = "Tái khám huyết áp", Status = AppointmentStatus.Scheduled },
	new() { Id = 2, PatientId = 2, DoctorId = 2, AppointmentDate = DateTime.Today.AddDays(1).AddHours(10), Reason = "Khám tổng quát", Status = AppointmentStatus.Scheduled }
],
serviceProvider.GetRequiredService<IPatientManager>(),
serviceProvider.GetRequiredService<IDoctorService>()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
