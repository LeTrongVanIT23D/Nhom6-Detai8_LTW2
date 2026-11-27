using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using StudentManagement.Data;
using StudentManagement.Hubs;
using StudentManagement.Middleware;
using StudentManagement.Models;
using StudentManagement.Repositories;
using StudentManagement.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();
    });
    builder.Configuration.AddUserSecrets<Program>(optional: true);
var dataProtectionPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MedikaClinic",
    "DataProtectionKeys");
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("MedikaClinic")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
if (OperatingSystem.IsWindows())
    dataProtection.ProtectKeysWithDpapi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseSqlServer(connectionString));

builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddControllers().AddJsonOptions(options =>
options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Medika Clinic API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập trực tiếp chuỗi Token thu được sau khi Login."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình.");
var issuer = jwtSettings["Issuer"]
    ?? throw new InvalidOperationException("JwtSettings:Issuer chưa được cấu hình.");
var audience = jwtSettings["Audience"]
    ?? throw new InvalidOperationException("JwtSettings:Audience chưa được cấu hình.");
var expiryMinutes = jwtSettings.GetValue<int?>("ExpiryMinutes")
    ?? throw new InvalidOperationException("JwtSettings:ExpiryMinutes chưa được cấu hình.");

if (Encoding.UTF8.GetByteCount(secretKey) < 32)
    throw new InvalidOperationException("JwtSettings:SecretKey phải có ít nhất 32 byte.");
if (expiryMinutes <= 0)
    throw new InvalidOperationException("JwtSettings:ExpiryMinutes phải lớn hơn 0.");

const string cookieOrBearerScheme = "CookieOrBearer";
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = cookieOrBearerScheme;
    options.DefaultAuthenticateScheme = cookieOrBearerScheme;
    options.DefaultChallengeScheme = cookieOrBearerScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddPolicyScheme(cookieOrBearerScheme, cookieOrBearerScheme, options =>
{
    options.ForwardDefaultSelector = context =>
        context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? JwtBearerDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
})
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
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPatientManager, PatientManager>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<ISupportService, SupportService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDoctorLeaveService, DoctorLeaveService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<BankTransferService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        var migrationSql = @"
        SET QUOTED_IDENTIFIER ON;
        SET ANSI_NULLS ON;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Doctors') AND name = 'Degree')
            ALTER TABLE Doctors ADD Degree NVARCHAR(MAX) NOT NULL DEFAULT 'Bác sĩ CKI';

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Doctors') AND name = 'ExperienceYears')
            ALTER TABLE Doctors ADD ExperienceYears INT NOT NULL DEFAULT 5;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Doctors') AND name = 'ProfileImageUrl')
            ALTER TABLE Doctors ADD ProfileImageUrl NVARCHAR(500) NOT NULL DEFAULT '';

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Doctors') AND name = 'IsActive')
            ALTER TABLE Doctors ADD IsActive BIT NOT NULL DEFAULT 1;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsLocked')
            ALTER TABLE Users ADD IsLocked BIT NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CreatedAt')
            ALTER TABLE Users ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Phone')
            ALTER TABLE Users ADD Phone NVARCHAR(30) NOT NULL DEFAULT '';

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'DoctorId')
            ALTER TABLE Users ADD DoctorId INT NULL;

        IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Users_Doctors_DoctorId')
            EXEC(N'ALTER TABLE Users ADD CONSTRAINT FK_Users_Doctors_DoctorId FOREIGN KEY (DoctorId) REFERENCES Doctors(Id)');

        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_DoctorId' AND object_id = OBJECT_ID('Users'))
            EXEC(N'CREATE UNIQUE INDEX IX_Users_DoctorId ON Users(DoctorId) WHERE DoctorId IS NOT NULL');

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Patients') AND name = 'InsuranceNumber')
            ALTER TABLE Patients ADD InsuranceNumber NVARCHAR(MAX) NOT NULL DEFAULT '';

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'BloodPressure')
            ALTER TABLE MedicalRecords ADD BloodPressure NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'HeartRate')
            ALTER TABLE MedicalRecords ADD HeartRate INT NULL;
        ELSE IF EXISTS (SELECT * FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE c.object_id = OBJECT_ID('MedicalRecords') AND c.name = 'HeartRate' AND t.name LIKE '%varchar%')
        BEGIN
            EXEC(N'UPDATE MedicalRecords SET HeartRate = NULL WHERE HeartRate = '''' OR ISNUMERIC(HeartRate) = 0;');
            ALTER TABLE MedicalRecords ALTER COLUMN HeartRate INT NULL;
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'Temperature')
            ALTER TABLE MedicalRecords ADD Temperature FLOAT NULL;
        ELSE IF EXISTS (SELECT * FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE c.object_id = OBJECT_ID('MedicalRecords') AND c.name = 'Temperature' AND t.name LIKE '%varchar%')
        BEGIN
            EXEC(N'UPDATE MedicalRecords SET Temperature = NULL WHERE Temperature = '''' OR ISNUMERIC(Temperature) = 0;');
            ALTER TABLE MedicalRecords ALTER COLUMN Temperature FLOAT NULL;
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'Weight')
            ALTER TABLE MedicalRecords ADD Weight FLOAT NULL;
        ELSE IF EXISTS (SELECT * FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE c.object_id = OBJECT_ID('MedicalRecords') AND c.name = 'Weight' AND t.name LIKE '%varchar%')
        BEGIN
            EXEC(N'UPDATE MedicalRecords SET Weight = NULL WHERE Weight = '''' OR ISNUMERIC(Weight) = 0;');
            ALTER TABLE MedicalRecords ALTER COLUMN Weight FLOAT NULL;
        END;

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reviews')
        BEGIN
            CREATE TABLE Reviews (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                DoctorId INT NOT NULL,
                PatientId INT NOT NULL,
                UserId INT NULL,
                AppointmentId INT NULL,
                Rating INT NOT NULL,
                Comment NVARCHAR(MAX) NOT NULL DEFAULT '',
                IsHidden BIT NOT NULL DEFAULT 0,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_Reviews_Doctors_DoctorId FOREIGN KEY (DoctorId) REFERENCES Doctors(Id),
                CONSTRAINT FK_Reviews_Patients_PatientId FOREIGN KEY (PatientId) REFERENCES Patients(Id),
                CONSTRAINT FK_Reviews_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION,
                CONSTRAINT FK_Reviews_Appointments_AppointmentId FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id) ON DELETE NO ACTION
            );
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'TreatmentPlan')
            ALTER TABLE MedicalRecords ADD TreatmentPlan NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'FollowUpDate')
            ALTER TABLE MedicalRecords ADD FollowUpDate DATETIME2 NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'LabTests')
            ALTER TABLE MedicalRecords ADD LabTests NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
        BEGIN
            CREATE TABLE AuditLogs (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Action NVARCHAR(MAX) NOT NULL,
                PerformedBy NVARCHAR(MAX) NOT NULL,
                Role NVARCHAR(MAX) NOT NULL,
                Details NVARCHAR(MAX) NOT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
            );
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AuditLogs') AND name = 'CreatedAt')
        BEGIN
            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AuditLogs') AND name = 'Timestamp')
            BEGIN
                EXEC(N'ALTER TABLE AuditLogs ADD CreatedAt DATETIME2 NULL');
                EXEC(N'UPDATE AuditLogs SET CreatedAt = [Timestamp] WHERE CreatedAt IS NULL');
                EXEC(N'ALTER TABLE AuditLogs ALTER COLUMN CreatedAt DATETIME2 NOT NULL');
                EXEC(N'ALTER TABLE AuditLogs ADD CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT GETDATE() FOR CreatedAt');
            END
            ELSE
            BEGIN
                ALTER TABLE AuditLogs ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
            END;
        END;

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Invoices')
        BEGIN
            CREATE TABLE Invoices (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                AppointmentId INT NOT NULL,
                PatientId INT NOT NULL,
                Amount DECIMAL(18,2) NOT NULL,
                Status NVARCHAR(MAX) NOT NULL,
                PaymentMethod NVARCHAR(MAX) NOT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                PaidAt DATETIME2 NULL,
                Notes NVARCHAR(MAX) NOT NULL
            );
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'PaidAt')
            ALTER TABLE Invoices ADD PaidAt DATETIME2 NULL;

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StoredFiles')
        BEGIN
            CREATE TABLE StoredFiles (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                FileName NVARCHAR(MAX) NOT NULL,
                ContentType NVARCHAR(MAX) NOT NULL,
                Content VARBINARY(MAX) NOT NULL,
                Length BIGINT NOT NULL,
                UploadedAt DATETIME2 NOT NULL DEFAULT GETDATE()
            );
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Doctors') AND name = 'UserId')
            ALTER TABLE Doctors ADD UserId INT NULL;

        IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Doctors_Users_UserId')
            EXEC(N'ALTER TABLE Doctors ADD CONSTRAINT FK_Doctors_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id)');

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'RewardPoints')
            ALTER TABLE Users ADD RewardPoints INT NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Patients') AND name = 'RewardPoints')
            ALTER TABLE Patients ADD RewardPoints INT NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingFee')
            ALTER TABLE Appointments ADD BookingFee DECIMAL(18,2) NOT NULL DEFAULT 100000;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'DiscountAmount')
            ALTER TABLE Appointments ADD DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'PointsUsed')
            ALTER TABLE Appointments ADD PointsUsed INT NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingFeeIsPaid')
            ALTER TABLE Appointments ADD BookingFeeIsPaid BIT NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingFeePaymentMethod')
            ALTER TABLE Appointments ADD BookingFeePaymentMethod NVARCHAR(30) NOT NULL DEFAULT '';

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingFeePaidAt')
            ALTER TABLE Appointments ADD BookingFeePaidAt DATETIME2 NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingFeePaymentReference')
            ALTER TABLE Appointments ADD BookingFeePaymentReference NVARCHAR(100) NOT NULL DEFAULT '';

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingFeeRefundDue')
            ALTER TABLE Appointments ADD BookingFeeRefundDue BIT NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingFeeRefunded')
            ALTER TABLE Appointments ADD BookingFeeRefunded BIT NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingFeeForfeited')
            ALTER TABLE Appointments ADD BookingFeeForfeited BIT NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'BookingSource')
        BEGIN
            ALTER TABLE Appointments ADD BookingSource NVARCHAR(20) NOT NULL DEFAULT 'InPerson';
            EXEC(N'UPDATE Appointments SET BookingSource = CASE WHEN UserId IS NULL THEN ''InPerson'' ELSE ''Online'' END;');
        END;

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OtcMedicines')
        BEGIN
            CREATE TABLE OtcMedicines (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(MAX) NOT NULL,
                Category NVARCHAR(MAX) NOT NULL,
                Price DECIMAL(18,2) NOT NULL,
                Unit NVARCHAR(MAX) NOT NULL DEFAULT 'Hộp',
                Description NVARCHAR(MAX) NOT NULL DEFAULT '',
                UsageInstructions NVARCHAR(MAX) NOT NULL DEFAULT '',
                StockQuantity INT NOT NULL DEFAULT 100,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
            );
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OtcMedicines') AND name = 'ImageUrl')
            ALTER TABLE OtcMedicines ADD ImageUrl NVARCHAR(500) NOT NULL DEFAULT '';

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OtcOrders')
        BEGIN
            CREATE TABLE OtcOrders (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                UserId INT NOT NULL,
                ReceiverName NVARCHAR(200) NOT NULL,
                ReceiverPhone NVARCHAR(30) NOT NULL,
                DeliveryAddress NVARCHAR(500) NOT NULL DEFAULT '',
                FulfillmentMethod NVARCHAR(20) NOT NULL,
                Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
                PaymentMethod NVARCHAR(30) NOT NULL,
                IsPaid BIT NOT NULL DEFAULT 0,
                PaymentConfirmedAt DATETIME2 NULL,
                PaymentReference NVARCHAR(100) NOT NULL DEFAULT '',
                SubTotal DECIMAL(18,2) NOT NULL,
                DiscountAmount DECIMAL(18,2) NOT NULL,
                FinalAmount DECIMAL(18,2) NOT NULL,
                PointsUsed INT NOT NULL DEFAULT 0,
                PointsEarned INT NOT NULL DEFAULT 0,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                CompletedAt DATETIME2 NULL,
                CONSTRAINT FK_OtcOrders_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OtcOrders') AND name = 'PaymentConfirmedAt')
            ALTER TABLE OtcOrders ADD PaymentConfirmedAt DATETIME2 NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OtcOrders') AND name = 'PaymentReference')
            ALTER TABLE OtcOrders ADD PaymentReference NVARCHAR(100) NOT NULL DEFAULT '';

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OtcOrderItems')
        BEGIN
            CREATE TABLE OtcOrderItems (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                OtcOrderId INT NOT NULL,
                MedicineId INT NOT NULL,
                MedicineName NVARCHAR(MAX) NOT NULL,
                MedicineUnit NVARCHAR(MAX) NOT NULL,
                UnitPrice DECIMAL(18,2) NOT NULL,
                Quantity INT NOT NULL,
                LineTotal DECIMAL(18,2) NOT NULL,
                CONSTRAINT FK_OtcOrderItems_OtcOrders_OtcOrderId FOREIGN KEY (OtcOrderId) REFERENCES OtcOrders(Id) ON DELETE CASCADE,
                CONSTRAINT FK_OtcOrderItems_OtcMedicines_MedicineId FOREIGN KEY (MedicineId) REFERENCES OtcMedicines(Id)
            );
        END;

        -- Đồng bộ UserId cho Doctors đã được gắn với Users
        EXEC(N'UPDATE d SET d.UserId = u.Id
        FROM Doctors d
        INNER JOIN Users u ON u.DoctorId = d.Id
        WHERE d.UserId IS NULL;');
        ";

        db.Database.ExecuteSqlRaw(migrationSql);

        var docUser = db.Users.FirstOrDefault(u => u.Username == "doctor");
        var firstDoc = db.Doctors.FirstOrDefault(d => d.IsActive);
        if (docUser != null && firstDoc != null)
        {
            bool changed = false;
            if (docUser.DoctorId == null)
            {
                docUser.DoctorId = firstDoc.Id;
                changed = true;
            }
            if (firstDoc.UserId == null)
            {
                firstDoc.UserId = docUser.Id;
                changed = true;
            }
            if (changed) db.SaveChanges();
        }

        if (!db.OtcMedicines.Any())
        {
            db.OtcMedicines.AddRange(
                new OtcMedicine { Name = "Panadol Extra Đỏ", Category = "Hạ sốt - Giảm đau", Price = 45000, Unit = "Hộp (15 vỉ x 12 viên)", Description = "Giảm đau nhanh, hạ sốt hiệu quả, không gây buồn ngủ.", UsageInstructions = "Người lớn uống 1-2 viên/lần, cách 4-6 giờ.", StockQuantity = 150 },
                new OtcMedicine { Name = "Paracetamol 500mg", Category = "Hạ sốt - Giảm đau", Price = 25000, Unit = "Hộp (10 vỉ x 10 viên)", Description = "Thuốc giảm đau hạ sốt thiết yếu không kê đơn (OTC).", UsageInstructions = "1 viên mỗi 4-6 tiếng khi sốt trên 38.5 độ.", StockQuantity = 200 },
                new OtcMedicine { Name = "Berocca Viên sủi", Category = "Bổ sung vitamin", Price = 89000, Unit = "Tuýp 10 viên", Description = "Bổ sung vitamin nhóm B, C và khoáng chất tăng cường năng lượng.", UsageInstructions = "Hòa tan 1 viên trong 200ml nước, uống buổi sáng.", StockQuantity = 80 },
                new OtcMedicine { Name = "Men vi sinh Enterogermina", Category = "Tiêu hóa", Price = 160000, Unit = "Hộp 20 ống", Description = "Bổ sung lợi khuẩn đường ruột, hỗ trợ rối loạn tiêu hóa.", UsageInstructions = "1-2 ống/ngày, lắc đều trước khi uống.", StockQuantity = 60 },
                new OtcMedicine { Name = "Siro ho thảo dược Prospan", Category = "Tai mũi họng", Price = 85000, Unit = "Chai 100ml", Description = "Chiết xuất cao lá thường xuân trị ho khan, ho đờm an toàn.", UsageInstructions = "Uống 5ml/lần, 3 lần/ngày sau bữa ăn.", StockQuantity = 90 },
                new OtcMedicine { Name = "Nước muối sinh lý NaCl 0.9%", Category = "Tai mũi họng", Price = 8000, Unit = "Chai 500ml", Description = "Dung dịch đẳng trương vệ sinh mắt mũi súc họng.", UsageInstructions = "Sử dụng rửa mũi họng hằng ngày.", StockQuantity = 300 }
            );
            db.SaveChanges();
        }

        var existingOtcMedicineNames = db.OtcMedicines
            .Select(medicine => medicine.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var additionalOtcMedicines = new[]
        {
            new OtcMedicine { Name = "Oresol bù nước điện giải", Category = "Bù nước - điện giải", Price = 30000, Unit = "Hộp 10 gói", Description = "Gói bù nước và điện giải đường uống.", UsageInstructions = "Pha và sử dụng đúng hướng dẫn trên bao bì; hỏi dược sĩ nếu chưa rõ.", StockQuantity = 100 },
            new OtcMedicine { Name = "Vitamin C 500mg", Category = "Bổ sung vitamin", Price = 35000, Unit = "Hộp 10 vỉ", Description = "Sản phẩm bổ sung vitamin C.", UsageInstructions = "Sử dụng theo hướng dẫn trên bao bì hoặc tư vấn của dược sĩ.", StockQuantity = 100 },
            new OtcMedicine { Name = "Viên ngậm họng thảo dược", Category = "Tai mũi họng", Price = 28000, Unit = "Hộp 20 viên", Description = "Viên ngậm thảo dược dùng khi cần làm dịu họng.", UsageInstructions = "Sử dụng theo hướng dẫn trên bao bì.", StockQuantity = 80 },
            new OtcMedicine { Name = "Nước muối sinh lý dạng xịt", Category = "Tai mũi họng", Price = 45000, Unit = "Chai 70ml", Description = "Dung dịch nước muối sinh lý dạng xịt tiện dụng.", UsageInstructions = "Sử dụng theo hướng dẫn trên bao bì.", StockQuantity = 80 },
            new OtcMedicine { Name = "Nước mắt nhân tạo", Category = "Chăm sóc mắt", Price = 55000, Unit = "Lọ 10ml", Description = "Dung dịch nhỏ mắt bôi trơn.", UsageInstructions = "Đọc kỹ nhãn sản phẩm và hỏi dược sĩ nếu có bệnh lý về mắt.", StockQuantity = 60 },
            new OtcMedicine { Name = "Miếng dán cá nhân", Category = "Vật tư y tế", Price = 18000, Unit = "Hộp 20 miếng", Description = "Miếng dán bảo vệ vết xước nhỏ.", UsageInstructions = "Làm sạch vùng da trước khi sử dụng; thay miếng dán khi cần.", StockQuantity = 120 },
            new OtcMedicine { Name = "Gạc y tế tiệt trùng", Category = "Vật tư y tế", Price = 25000, Unit = "Gói 10 miếng", Description = "Gạc tiệt trùng dùng cho chăm sóc vết thương.", UsageInstructions = "Sử dụng theo hướng dẫn của nhân viên y tế.", StockQuantity = 100 },
            new OtcMedicine { Name = "Nhiệt kế điện tử", Category = "Thiết bị y tế", Price = 99000, Unit = "Cái", Description = "Thiết bị đo thân nhiệt điện tử.", UsageInstructions = "Đọc kỹ hướng dẫn sử dụng đi kèm sản phẩm.", StockQuantity = 30 }
        };
        var newOtcMedicines = additionalOtcMedicines
            .Where(medicine => existingOtcMedicineNames.Add(medicine.Name))
            .ToArray();
        if (newOtcMedicines.Length > 0)
        {
            db.OtcMedicines.AddRange(newOtcMedicines);
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Lưu ý: Khởi tạo database lúc startup gặp lỗi, website vẫn tiếp tục chạy...");
    }
}


    app.UseExceptionHandling();
    app.UseRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Medika Clinic API v1");
        options.RoutePrefix = "swagger";
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<PageAccessMiddleware>();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapControllers();
    app.MapHub<ClinicHub>("/hubs/clinic");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ứng dụng dừng bất thường khi khởi động.");
}
finally
{
    Log.CloseAndFlush();
}