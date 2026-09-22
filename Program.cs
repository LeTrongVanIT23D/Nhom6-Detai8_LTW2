using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using StudentManagement.Data;
using StudentManagement.Models;
using StudentManagement.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

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
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPatientManager, PatientManager>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<ISupportService, SupportService>();

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
