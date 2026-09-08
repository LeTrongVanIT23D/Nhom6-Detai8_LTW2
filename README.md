# Hệ thống quản lý phòng khám đa khoa

Ứng dụng ASP.NET Core Web API để quản lý bệnh nhân với các chức năng:

- Xem danh sách bệnh nhân
- Xem thông tin bệnh nhân theo mã
- Thêm, sửa và xóa bệnh nhân
- Quản lý bác sĩ theo chuyên khoa
- Quản lý lịch hẹn, trạng thái lịch hẹn và kiểm tra trùng lịch
- Dashboard thống kê hoạt động phòng khám

## Chạy ứng dụng

```powershell
cd "d:\LTWEB2\ql phong da khoa"
dotnet run --project .\StudentManagement.csproj --launch-profile http
```

Sau khi thấy dòng `Now listening on: http://localhost:5078`, mở trình duyệt tại:

```text
http://localhost:5078/
```

Hoặc mở tự động bằng một lệnh PowerShell khác:

```powershell
Start-Process "http://localhost:5078/"
```

API bệnh nhân có địa chỉ `http://localhost:5078/api/patients`. Không gõ URL trực tiếp trong PowerShell vì nó không phải là một lệnh.

API người dùng:

```text
GET  /api/users
POST /api/users/login
```

## Đăng nhập, đăng ký và phân quyền

Trang đăng nhập:

```text
http://localhost:5078/login.html
```

Trang đăng ký dành cho người dùng:

```text
http://localhost:5078/register.html
```

Tài khoản đăng ký mới luôn có role `User` và tạo kèm hồ sơ bệnh nhân gồm họ tên, email, ngày sinh, số điện thoại và địa chỉ. User có thể chọn chuyên khoa, bác sĩ, ngày giờ, đặt lịch, hủy lịch và gửi yêu cầu hỗ trợ.

Tài khoản đặc biệt không được lưu trong source code. Khi chạy local, truyền mật khẩu qua biến môi trường:

```powershell
$env:MEDIKA_RECEPTIONIST_PASSWORD = 'mật-khẩu-lễ-tân'
$env:MEDIKA_ADMIN_PASSWORD = 'mật-khẩu-admin'
$env:MEDIKA_DOCTOR_PASSWORD = 'mật-khẩu-bác-sĩ'
$env:MEDIKA_SUPPORT_PASSWORD = 'mật-khẩu-hỗ-trợ'
dotnet run --project .\StudentManagement.csproj --launch-profile http
```

Mật khẩu chỉ được băm bằng `PasswordHasher` trong bộ nhớ; API không bao giờ trả về mật khẩu hoặc password hash.

Các khu vực riêng:

```text
http://localhost:5078/doctor.html
http://localhost:5078/admin.html
http://localhost:5078/user.html
```

Đường dẫn riêng không thay thế kiểm tra quyền: server vẫn yêu cầu cookie đăng nhập và role phù hợp. Bác sĩ có thể ghi bệnh án, kê toa và quản lý lịch làm việc. Admin quản lý tài khoản và role. Nhân viên `Support` tiếp nhận và trả lời tin nhắn khách hàng.

API xác thực:

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/auth/me
POST /api/auth/logout
POST /api/auth/change-password
POST /api/auth/forgot-password
```

## API bác sĩ và lịch hẹn

```text
GET    /api/doctors
GET    /api/doctors/{id}
POST   /api/doctors
PUT    /api/doctors/{id}
DELETE /api/doctors/{id}

GET    /api/appointments
GET    /api/appointments/{id}
POST   /api/appointments
PUT    /api/appointments/{id}
DELETE /api/appointments/{id}

GET    /api/dashboard
GET    /api/medical-records
POST   /api/medical-records
POST   /api/medical-records/{id}/prescriptions
GET    /api/doctors/{id}/schedule
POST   /api/doctors/{id}/schedule
GET    /api/support
POST   /api/support
PATCH  /api/support/{id}/reply
```

Có thể lọc lịch hẹn bằng query string:

```text
GET /api/appointments?date=2026-09-09
GET /api/appointments?status=Scheduled
```

Trạng thái lịch hẹn gồm `Scheduled`, `Completed` và `Cancelled`.

Ví dụ tạo lịch hẹn:

```json
{
	"patientId": 1,
	"doctorId": 1,
	"appointmentDate": "2026-09-09T09:00:00",
	"reason": "Tái khám",
	"status": "Scheduled"
}
```

Khi gửi JSON có tiếng Việt bằng PowerShell 5.1, hãy tạo body bằng UTF-8:

```powershell
$body = @{ fullName = 'Phạm Văn Dũng'; dateOfBirth = '1995-08-10'; phone = '0934567890'; address = 'Huế'; medicalHistory = 'Không' } | ConvertTo-Json
$bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
Invoke-RestMethod -Uri 'http://localhost:5078/api/patients' -Method Post -ContentType 'application/json; charset=utf-8' -Body $bytes
```

Các phương thức:

```text
GET    /api/patients
GET    /api/patients/{id}
POST   /api/patients
PUT    /api/patients/{id}
DELETE /api/patients/{id}
```
