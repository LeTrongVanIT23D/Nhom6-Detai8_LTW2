using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.Json.Serialization;
using StudentManagement.Models;
using StudentManagement.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
	options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/login.html";
		options.AccessDeniedPath = "/forbidden.html";
		options.Cookie.Name = "Medika.Auth";
		options.SlidingExpiration = true;
		options.ExpireTimeSpan = TimeSpan.FromHours(8);
		options.Events.OnRedirectToLogin = context =>
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return Task.CompletedTask;
		};
		options.Events.OnRedirectToAccessDenied = context =>
		{
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			return Task.CompletedTask;
		};
	});
builder.Services.AddAuthorization();
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
builder.Services.AddSingleton<IMedicalRecordService>(serviceProvider => new MedicalRecordService(
	serviceProvider.GetRequiredService<IPatientManager>(),
	serviceProvider.GetRequiredService<IDoctorService>()));
builder.Services.AddSingleton<IScheduleService>(serviceProvider => new ScheduleService(
	serviceProvider.GetRequiredService<IDoctorService>()));
builder.Services.AddSingleton<ISupportService, SupportService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "Medika Clinic API v1");
	options.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
	var path = context.Request.Path.Value?.ToLowerInvariant();
	var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
	var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

	if ((path is "/" or "/index.html") && !isAuthenticated)
	{
		context.Response.Redirect("/login.html");
		return;
	}

	if (path is "/admin.html" && (!isAuthenticated || role != "Admin"))
	{
		context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
		return;
	}

	if (path is "/doctor.html" && (!isAuthenticated || role != "Doctor"))
	{
		context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
		return;
	}

	if (path is "/user.html" && (!isAuthenticated || role != "User"))
	{
		context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
		return;
	}

	if (path is "/support.html" && (!isAuthenticated || role is not ("Support" or "Admin")))
	{
		context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
		return;
	}

	if ((path is "/" or "/index.html") && role is not ("Receptionist" or "Admin"))
	{
		context.Response.Redirect(role == "Doctor" ? "/doctor.html" : "/user.html");
		return;
	}

	await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
