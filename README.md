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
dotnet run --project .\StudentManagement.csproj -- --urls http://localhost:5078
```

Sau khi thấy dòng `Now listening on: http://localhost:5078`, mở trình duyệt tại:

```text
http://localhost:5078/
```

Trang Swagger xem và thử API:

```text
http://localhost:5078/swagger
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

Tài khoản đặc biệt không được lưu trong source code. Chạy một lần script sau để lưu mật khẩu trong .NET User Secrets ở máy local:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\setup-local-accounts.ps1
dotnet run --project .\StudentManagement.csproj -- --urls http://localhost:5078
```

Script sẽ hỏi mật khẩu riêng cho các username `admin`, `doctor`, `letu` và `support`. Các tài khoản này chỉ được tạo/cập nhật khi mật khẩu đã được cấu hình; ứng dụng không đặt mật khẩu mặc định và trang đăng nhập không hiển thị tài khoản mẫu.

Mật khẩu đặc biệt được lưu ngoài repository bằng .NET User Secrets. Tài khoản đăng ký và hồ sơ bệnh nhân được lưu trong SQL Server; cơ sở dữ liệu chỉ lưu password hash, API không trả về mật khẩu hoặc password hash.

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

API đăng nhập trả về `accessToken` cùng thông tin người dùng và vẫn tạo cookie để tương thích với website. Client gọi API có thể gửi token trong header `Authorization: Bearer <accessToken>`; server chấp nhận cả cookie lẫn bearer token. JWT signing key không được lưu trong `appsettings.json`; cấu hình local bằng .NET User Secrets:

```powershell
dotnet user-secrets set "JwtSettings:SecretKey" "Scretkey123456789123456789123456789123456789" --project .\StudentManagement.csproj
```

Khi triển khai, đặt `JwtSettings__SecretKey` trong secret store của môi trường triển khai bằng một khóa ngẫu nhiên riêng dài ít nhất 32 byte; không sử dụng khóa ví dụ ở trên.

### Lưu cấu hình API key chatbot

API key và cấu hình chatbot được lưu ngoài repository trong hồ sơ người dùng tại `%LOCALAPPDATA%\MedikaClinic\chatbot-settings.json`; API key được mã hóa bằng ASP.NET Core Data Protection. Khóa bảo vệ được lưu trong `%LOCALAPPDATA%\MedikaClinic\DataProtectionKeys` và trên Windows được bảo vệ bằng DPAPI. Vì vậy, cấu hình vẫn còn sau khi tắt/mở máy hoặc chạy lại `dotnet run`; nhập API key mới rồi bấm **Lưu cấu hình** để thay key đang dùng. Không xóa hai thư mục Medika này nếu muốn giữ cấu hình đã lưu. Khi chuyển ứng dụng/database sang máy hoặc tài khoản Windows khác, hãy cấu hình lại API key trên máy đó.

## Phân biệt chức năng trực tuyến và tại phòng khám

- **Trực tuyến (User):** gửi yêu cầu đặt lịch, theo dõi/đổi/hủy lịch, xem hồ sơ sau khám, gửi ticket hoặc chat hỗ trợ, đặt trước thuốc OTC. Lịch được đánh dấu nguồn `Online` và chờ lễ tân xác nhận.
- **Tại phòng khám (Receptionist/Doctor):** lễ tân tiếp nhận và check-in đúng ngày hẹn để cấp số theo bác sĩ; bác sĩ khám trực tiếp, nhập bệnh án/đơn thuốc; lễ tân lập hóa đơn và xác nhận đã thu tiền. Chỉ ca `CheckedIn` mới được hoàn tất khám.
- Lịch đặt online yêu cầu chuyển khoản phí giữ chỗ BIDV 100.000đ (trừ điểm dùng); QR VietQR được tạo cục bộ theo từng lịch, điền sẵn người nhận, số tiền và nội dung chuyển khoản. Phí này được trừ vào hóa đơn sau khi khám. Lễ tân đối soát giao dịch trước khi xác nhận lịch. Bệnh nhân hủy trước ít nhất 24 giờ sẽ được đưa vào danh sách chờ hoàn khoản chuyển khoản và được hoàn điểm đã dùng; hủy muộn hoặc không đến khám thì không được hoàn phí. Nếu phòng khám hủy, khoản chuyển khoản được hoàn thủ công. Lịch đặt tại quầy không bị tính phí giữ chỗ online.
- Đơn OTC yêu cầu thanh toán ngay bằng chuyển khoản BIDV qua ảnh QR tĩnh của phòng khám; người mua cần nhập đúng số tiền và mã đơn hiển thị. Điểm thưởng được trừ trực tiếp khi đặt. Lễ tân đối soát giao dịch trước khi xác nhận giao/nhận thuốc; điểm thưởng mới được cộng sau khi hoàn tất. Đơn chưa thanh toán có thể tự hủy và được hoàn điểm/tồn kho; đơn đã thanh toán cần liên hệ lễ tân để xử lý. Hệ thống chưa tích hợp cổng thanh toán tự động hay đơn vị vận chuyển, vì vậy nhân viên phải đối chiếu giao dịch trong tài khoản ngân hàng.
- Admin có thể thêm/cập nhật ảnh sản phẩm bằng cách dán URL HTTPS hoặc tải file tại mục **Ảnh thuốc OTC** trong trung tâm quản trị. Bệnh nhân xem ảnh ở danh mục và cửa sổ đặt thuốc; ảnh tải lên chấp nhận JPEG/PNG/WebP, tối đa 1 MB.

### Cấu hình nhận chuyển khoản BIDV

Không lưu số tài khoản hoặc tên chủ tài khoản trong mã nguồn. Khi chạy local, cấu hình riêng bằng User Secrets:

```powershell
dotnet user-secrets set "BankTransfer:AccountNumber" "<ACCOUNT_NUMBER>"
dotnet user-secrets set "BankTransfer:AccountName" "<ACCOUNT_NAME>"
```

Khi triển khai, dùng secret store của môi trường hoặc các biến môi trường `BankTransfer__AccountNumber` và `BankTransfer__AccountName`. Nếu thiếu cấu hình, API sẽ từ chối tạo lịch/đơn cần chuyển khoản thay vì tạo giao dịch thiếu thông tin. Việc xác nhận thanh toán và chuyển hoàn hiện do nhân viên thực hiện thủ công; không có kết nối trực tiếp với BIDV.

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

Trạng thái lịch hẹn gồm `Pending`, `Confirmed`, `CheckedIn`, `Completed`, `Cancelled` và `NeedReschedule` (`Scheduled` chỉ còn để đọc dữ liệu cũ).

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

## API lưu trữ và tải file (Database StoredFiles)

Hệ thống hỗ trợ upload và lưu trữ file nhị phân (ảnh, pdf, tài liệu) trực tiếp vào SQL Server:

```text
POST /api/files/upload   - Upload file (multipart/form-data với field 'File')
GET  /api/files/{id}     - Tải file theo mã ID
```
