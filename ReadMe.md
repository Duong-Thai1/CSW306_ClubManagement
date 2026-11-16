# Club Management API

Backend quản lý CLB và sự kiện cho CSW309. Project dùng ASP.NET Core 10, SQL Server, EF Core, BCrypt, JWT Bearer và Swagger UI. Mở `ClubManagement.csproj` trong Visual Studio có hỗ trợ .NET 10 hoặc dùng CLI bên dưới.

## 1. Tạo database từ SQL nháp

Trong SQL Server Management Studio, tạo database mới `ClubManagement`. Mở từng script, chọn database này trong danh sách kết nối rồi chạy theo thứ tự:

| Script | Nội dung |
|---|---|
| `Database/01_Base.sql` | Giữ cấu trúc các bảng chính trong query gốc. Sửa dấu phẩy cuối, cột bị kiểm tra nhầm trong CHECK của ClubMember, FK EventClub và tham chiếu actor. |
| `Database/02_Extensions.sql` | Thêm thông tin đăng nhập, phiên JWT, đề xuất, đăng ký, điểm danh, điểm hoạt động, thông báo và audit. |
| `Database/03_ClubWork.sql` | Thêm bài tuyển thành viên, phân công, xin vắng, xử lý vi phạm và sổ điểm đóng góp. |

`Database/OriginalDraft.sql` là bản gốc để đối chiếu. Ba script trên áp dụng một lần theo thứ tự vào database mới. Nếu đang có dữ liệu trong database tạo theo bản nháp, cần đối chiếu các thay đổi ở bước 01 và xử lý các bản ghi xung đột trước khi áp dụng phần mở rộng. Project không tự gọi `EnsureCreated` hoặc tự cập nhật schema lúc chạy.

Trên Windows, có thể dùng `sqlcmd` với SQL Server mặc định (`.`) và Windows Authentication:

```powershell
sqlcmd -S . -E -C -b -Q "CREATE DATABASE ClubManagement"
sqlcmd -S . -E -C -b -d ClubManagement -i Database/01_Base.sql
sqlcmd -S . -E -C -b -d ClubManagement -i Database/02_Extensions.sql
sqlcmd -S . -E -C -b -d ClubManagement -i Database/03_ClubWork.sql
```

## 2. Khởi động API

Từ thư mục chứa `ClubManagement.csproj`:

```powershell
dotnet restore
dotnet build
dotnet dev-certs https --trust
dotnet user-secrets set "ConnectionStrings:ClubManagement" "Server=.;Database=ClubManagement;Trusted_Connection=True;TrustServerCertificate=True"
$signingKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "JwtSettings:SigningKey" $signingKey
dotnet user-secrets set "Bootstrap:Username" "ctsv"
dotnet user-secrets set "Bootstrap:Password" "<nhập mật khẩu riêng dài ít nhất 12 ký tự>"
dotnet run -- --bootstrap-staff
dotnet user-secrets remove "Bootstrap:Password"
dotnet run
```

Lệnh bootstrap chạy một lần để tạo tài khoản CTSV đầu tiên rồi thoát. Đổi mật khẩu mẫu trước khi chạy. Với SQL Server Express, dùng đúng tên instance, ví dụ `Server=.\SQLEXPRESS`. Khi chạy ngoài môi trường Development, cấp cấu hình qua biến môi trường `ConnectionStrings__ClubManagement` và `JwtSettings__SigningKey`. Dùng chứng chỉ tin cậy khi triển khai và giới hạn Swagger theo môi trường.

Mở [Swagger UI](https://localhost:7151/swagger/index.html). Gọi `POST /api/auth/login`, sao chép `accessToken`, chọn **Authorize** và dán token. UI tự thêm tiền tố `Bearer`. Tài liệu Swashbuckle ở `/swagger/v1/swagger.json`. Tài liệu OpenAPI tích hợp của ASP.NET Core ở `/openapi/v1.json` khi bật tài liệu.

## 3. JWT và thời gian refresh

| Cấu hình | Hành vi |
|---|---|
| `JwtSettings:AccessTokenDays = 3` | Access JWT dùng trong 3 ngày từ lần cấp. Hết hạn thì endpoint cần xác thực trả 401. |
| `JwtSettings:RefreshGraceHours = 5` | Cửa sổ gọi `POST /api/auth/refresh` bắt đầu đúng khi access JWT hết hạn và kết thúc sau 5 giờ. |
| Refresh trước cửa sổ | Trả 409. |
| Refresh hợp lệ trong cửa sổ | Gửi JSON `{"refreshToken":"..."}`. Server cấp JWT và refresh token mới, đồng thời vô hiệu hóa refresh token cũ. |
| Hết cửa sổ hoặc dùng lại refresh token đã xoay | Trả 401. Đăng nhập lại để tạo phiên mới. |
| Logout | Hủy phiên của access JWT hiện tại. CTSV khóa tài khoản sẽ thu hồi mọi phiên của tài khoản đó. |

Cách hiểu đã dùng cho “5 giờ hồi” là **5 giờ được phép refresh sau khi JWT hết hạn**. Client cần giữ refresh token và chủ động gửi yêu cầu làm mới. Quy tắc 3 ngày + 5 giờ thay cho quy tắc hết phiên sau 30 phút không hoạt động trong bản phân tích. Server lưu SHA-256 của refresh token ngẫu nhiên, còn mật khẩu được băm bằng BCrypt.

## 4. Luồng chạy thử trên Swagger

1. Đăng nhập CTSV, dùng `POST /api/users` tạo hai tài khoản sinh viên. Tạo lĩnh vực qua `POST /api/clubs/fields`. Tạo CLB qua `POST /api/clubs`, chuyển sang `Active` và gán một sinh viên làm chủ CLB qua `PUT /api/clubs/{clubId}/owner`.
2. Đăng nhập chủ CLB và thay token trong **Authorize**. Tạo đề xuất qua `POST /api/clubs/{clubId}/event-proposals`. Gửi qua `POST /api/event-proposals/{id}/submit`. Trong lần chạy thử có thể dùng sức chứa 1 và thời điểm bắt đầu sắp tới. Gửi thời gian ISO 8601 kèm múi giờ, ví dụ `2026-12-01T09:00:00+07:00`.
3. Đăng nhập CTSV, chọn `Approved` và đặt `activityPoints` qua `POST /api/event-proposals/{id}/decisions`. Lấy `eventId` từ kết quả và thử danh mục sự kiện.
4. Đăng nhập từng sinh viên rồi thử `POST /api/events/{eventId}/registrations`. Khi còn một chỗ, gửi hai request đồng thời bằng token của hai sinh viên để kiểm tra chỉ một người được đăng ký. Chủ CLB có thể xem roster ở `GET /api/events/{eventId}/registrations`.
5. Sau giờ bắt đầu trong ngày diễn ra, chủ CLB ghi điểm danh qua `PUT /api/events/{eventId}/attendance`. Sau giờ kết thúc, CTSV gọi `POST /api/events/{eventId}/confirm-results` hai lần. Số điểm được cấp phải giữ nguyên khi gọi lại. Sinh viên xem điểm tại `GET /api/me/activity-points`.
6. Kiểm tra chủ CLB A bị từ chối khi dùng token đó sửa tài nguyên CLB B. Tạo một sự kiện CTSV, thử gợi ý CLB, gửi lời mời, phản hồi với quyền chủ CLB và lọc báo cáo bằng `GET /api/reports/event-participation`.
7. Để thử biên refresh nhanh, có thể tạm mô phỏng phiên hết hạn bằng cách đổi `AccessExpiresAt` của một phiên thử trong SQL. Việc đó kiểm thử cổng refresh và xoay phiên. Để đo thời hạn chữ ký JWT thực tế, cần JWT đã hết hạn hoặc cấu hình thời gian kiểm thử riêng. Khôi phục database thử sau khi hoàn thành.

## 5. Tổ chức code và những điểm cần chốt

- `Controllers/` chứa endpoint và chính sách quyền. `Dtos/` chứa hợp đồng HTTP có validation. `Services/` chứa các luồng nghiệp vụ tách theo tài khoản, CLB, đăng ký, đề xuất, kết quả, lời mời, nhiệm vụ và kỷ luật.
- `Models/` bám schema SQL. `Data/` ánh xạ EF Core và cung cấp khóa giao dịch SQL Server. Unique index chặn trùng đăng ký, phiếu điểm và membership. Khóa giao dịch theo sự kiện tuần tự hóa các request tranh sức chứa.
- Có ba luồng sự kiện: đề xuất công khai được CTSV duyệt, sự kiện CTSV mời CLB, và sự kiện nội bộ CLB. Điểm hoạt động cấp trường và điểm đóng góp CLB dùng hai sổ điểm riêng. Lời mời CLB và đăng ký của sinh viên cũng dùng hai bảng khác nhau.
- Các quyết định mặc định gồm học kỳ theo nửa năm, một chủ mỗi CLB, giới hạn 5 CLB, 100 điểm đóng góp ban đầu từ SQL nháp, điểm danh theo giờ Việt Nam, và minh chứng xin vắng lưu dưới dạng tham chiếu do client cung cấp. Mức phạt và ngưỡng xem xét khai trừ nằm dưới `ClubRules`.
- Các endpoint thông báo dùng hộp thư trong ứng dụng. Mức phạt được tính khi chủ CLB thực hiện thao tác phạt, hỗ trợ miễn/hoàn điểm và khiếu nại. Hỗ trợ từ chối phạt bằng cách không ghi khoản trừ. Với sự kiện bắt buộc, cấu hình mức phạt vắng và cho phép xin vắng muộn theo nghiệp vụ.
- Các phần cần triển khai hoặc nghiệm thu thêm theo quy chế thật gồm upload minh chứng, danh sách người được giao sự kiện ngoài người đã đăng ký, chính sách cộng điểm đóng góp khi dự sự kiện, chế độ lưu lịch sử kỳ thành viên và đo tải/uptime. Báo cáo sự kiện CTSV theo CLB dựa trên liên kết CLB và lịch sử thành viên hiện có, chưa chụp snapshot đội hình mỗi sự kiện.
- `Design.md` lưu kế hoạch ban đầu và mapping FR/NFR/UC để đối chiếu. Các bước 01–03 của schema và endpoint có thể khác tên đề xuất nếu giữ được cấu trúc SQL gốc.

## 6. Phạm vi kiểm tra

Đã đối chiếu cấu hình JSON, package references, routes, DTO, entity mapping và script SQL ở mức tĩnh. Môi trường tạo project thiếu .NET 10 SDK và SQL Server, nên cần chạy `dotnet restore`, `dotnet build` và quy trình Swagger ở mục 4 trên máy phát triển để xác minh tích hợp.

## 7. Prompt tóm tắt để tái tạo project

> Từ `FR_NFR.docx`, tài liệu use case CSW309, `KeHoachBackendCLB.md` và `ClubManagementQueryDraft.sql`, tạo project ASP.NET Core 10 Web API dạng modular monolith theo SOLID. Giữ query gốc để đối chiếu. Xuất script SQL Server đã sửa và script mở rộng cho các bảng còn thiếu. Dùng EF Core ánh xạ schema SQL, chia Models, DTOs, Controllers, Services và Data. Tạo xác thực username/password BCrypt, JWT Bearer, vai trò CTSV/Sinh viên và quyền chủ/thành viên theo từng CLB. Trong `appsettings.json`, đặt access JWT 3 ngày, cho phép xoay refresh token trong 5 giờ kể từ khi JWT hết hạn, và yêu cầu đăng nhập lại khi hết cửa sổ. Swagger UI phải có nút Authorize kiểu HTTP Bearer.
>
> Thêm các NuGet package `BCrypt.Net-Next`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.VisualStudio.Azure.Containers.Tools.Targets`, `Swashbuckle.AspNetCore`, `Swashbuckle.AspNetCore.Swagger`, `Swashbuckle.AspNetCore.SwaggerGen` và `Swashbuckle.AspNetCore.SwaggerUI`.
>
> Triển khai tài khoản, CLB, đề xuất/duyệt/công bố, danh mục, đăng ký/hủy, điểm danh/xác nhận điểm hoạt động, CTSV giao và gợi ý CLB, tuyển thành viên, nhiệm vụ định kỳ, thông báo, xin vắng, phạt/khiếu nại và báo cáo. Bảo đảm đăng ký đồng thời không vượt sức chứa và ghi điểm không trùng. Tách điểm hoạt động khỏi điểm đóng góp. Dùng DTO có validation và policy kiểm tra quyền theo tài nguyên. Viết `ReadMe.md` gồm cách chạy SQL, cấu hình, bootstrap CTSV, quy trình thử Swagger, phạm vi đã xác minh và prompt tái tạo này.
