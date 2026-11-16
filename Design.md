# Kế hoạch backend hệ thống quản lý CLB và sự kiện

Nguồn: `FR_NFR.docx` (FR-01–18, NFR-01–08), `Phân tích & thiết kế hệ thống quản lý CLB và sự kiện (CSW309 ISAD).docx` (UC01–22, mục 7–8), `ClubManagementQueryDraft.sql` (schema nháp).

**Kiến trúc đề xuất:** modular monolith ASP.NET Core Web API, EF Core và SQL Server. Giả định stack này dựa trên cú pháp SQL nháp và ngữ cảnh phát triển. Phạm vi dưới đây bao phủ cả hai tài liệu. Các ID trong bảng giữ nguyên theo nguồn. Endpoint, trạng thái và thứ tự triển khai là đề xuất.

## 1. Yêu cầu chức năng (FR)

| ID | Ưu tiên | Tóm tắt |
|---|---|---|
| FR-01 | Must | Đăng nhập bằng username/mật khẩu và truy cập theo quyền. |
| FR-02 | Must | CTSV tạo, sửa, vô hiệu hóa CLB và tài khoản, chỉ định sinh viên làm cán bộ CLB. |
| FR-03–04 | Must | Sinh viên lọc sự kiện đã công bố theo từ khóa, ngày, CLB. Chi tiết gồm tiêu đề, CLB, mô tả, địa điểm, thời gian, sức chứa, chỗ còn lại và điểm hoạt động. |
| FR-05 | Must | Sinh viên đăng ký sự kiện đã công bố khi còn chỗ và chưa đăng ký. |
| FR-06 | Should | Sinh viên hủy đăng ký trước giờ bắt đầu. |
| FR-07–09 | Must | Chủ CLB tạo đề xuất gồm tiêu đề, mục tiêu, mô tả, địa điểm, thời gian, sức chứa dự kiến. Gửi, xem trạng thái và phản hồi, sửa rồi gửi lại khi được yêu cầu. |
| FR-10–12 | Must | CTSV duyệt, yêu cầu sửa hoặc từ chối đề xuất đang chờ. Yêu cầu sửa và từ chối cần phản hồi. Khi duyệt phải đặt điểm hoạt động, hệ thống công bố sự kiện. |
| FR-13 | Should | Chủ CLB xem danh sách sinh viên đăng ký sự kiện của CLB mình. |
| FR-14–16 | Must | Chủ CLB ghi nhận điểm danh vào ngày diễn ra. CTSV xác nhận kết quả rồi cấp điểm cho người có mặt. Sinh viên xem lịch sử và tổng điểm hoạt động. |
| FR-17 | Should | Thông báo kết quả duyệt, sự kiện mới công bố, đăng ký thành công và điểm được cấp. |
| FR-18 | Should | CTSV báo cáo sự kiện và mức tham gia theo CLB và khoảng thời gian. |

## 2. Yêu cầu phi chức năng (NFR)

| ID | Yêu cầu trong nguồn | Cách thiết kế và xác minh |
|---|---|---|
| NFR-01 | Danh sách sự kiện tải trong 3 giây với ít nhất 500 người dùng đồng thời. Tải mục tiêu cần xác nhận. Bản use case thêm trang thường dưới 2 giây với dữ liệu 2.000 sinh viên, 50 CLB và bảng tiến độ CTSV dưới 3 giây. | Phân trang, chỉ mục theo bộ lọc, projection DTO. Đo riêng hành trình tải trang và độ trễ API, thống nhất percentile, hồ sơ tải, kích thước dữ liệu trước khi nghiệm thu. |
| NFR-02 | Phân quyền theo vai trò, băm mật khẩu, chỉ CTSV được xem toàn bộ báo cáo. Bản use case thêm HTTPS, hết phiên sau 30 phút không hoạt động, chống SQL injection và XSS. | Policy phía server theo role và tài nguyên, password hasher có sẵn, truy vấn tham số hóa, encode dữ liệu khi giao diện render. Nếu chọn JWT, kiểm tra thời gian không hoạt động ở phiên phía server và kiểm thử token cũ sau khi khóa tài khoản. |
| NFR-03 | Đăng ký tối đa 3 bước từ danh sách sự kiện, chạy trên desktop và điện thoại. Bản use case yêu cầu tiếng Việt, lỗi rõ ràng, thao tác chính tối đa 3 lần bấm, tương thích Chrome, Edge, Firefox và Safari bản mới. | Trả trạng thái và chỗ còn lại cùng danh sách, lỗi nghiệp vụ ổn định để giao diện hiển thị bằng tiếng Việt. Nghiệm thu bằng kiểm thử luồng và trình duyệt. |
| NFR-04 | Không trùng lượt đăng ký hoặc vượt sức chứa. Bản use case thêm duy nhất một Membership mỗi cặp sinh viên–CLB, lưu lịch sử phạt và dùng transaction khi duyệt thành viên hoặc ghi phạt. | Unique index, khóa hoặc cập nhật có điều kiện trong transaction, lưu thay đổi phạt theo trạng thái và nhật ký. Kiểm thử nhiều request tranh chỗ cuối. |
| NFR-05 | Uptime ít nhất 99% trong học kỳ, trừ bảo trì có kế hoạch. Bản use case thêm sao lưu hằng ngày. | Đo uptime bằng giám sát đầu cuối, ghi cửa sổ bảo trì, thử khôi phục bản sao lưu. |
| NFR-06 | Lưu người và thời điểm duyệt đề xuất, ghi điểm danh và xác nhận điểm. Bản use case thêm nhật ký duyệt, phạt và miễn phạt. | Ghi actor, thời điểm, hành động và đối tượng trong cùng transaction với thay đổi liên quan. |
| NFR-07 | Dữ liệu cá nhân chỉ cấp theo quyền và mục đích quản lý sự kiện. Bản use case giới hạn MSSV, họ tên, email và quyền xem vi phạm. | DTO riêng theo người xem, kiểm tra quyền trên từng CLB, loại dữ liệu cá nhân không cần thiết khỏi báo cáo và log. Chốt quyền tự xem vi phạm theo UC21. |
| NFR-08 | Hỗ trợ ít nhất 50 CLB và 5.000 sinh viên không đổi cấu trúc. Mốc sinh viên cần xác nhận. Bản use case yêu cầu tách lớp và cấu hình được quy tắc. | Seed bộ dữ liệu mục tiêu, đo truy vấn danh sách và báo cáo. Quản lý trọng số gợi ý, giới hạn CLB và mức phạt bằng cấu hình. |

## 3. Use case và thiết kế backend

CTSV là vai trò cấp hệ thống. Sinh viên có thể là chủ CLB A và thành viên CLB B. Quyền thao tác theo CLB lấy từ `Membership` đang hoạt động. Danh mục công khai giới hạn theo trạng thái công bố và đối tượng được xem.

### 3.1 Use case UC01–UC22

| UC | Actor và hành động | Controller / Service | DTO chính |
|---|---|---|---|
| UC01 / FR-01 | Mọi người đăng nhập, đăng xuất | AuthController / AuthService | LoginRequest, SessionResponse |
| UC02 / FR-02 | CTSV tạo, duyệt, khóa CLB. FR-02 thêm sửa, vô hiệu hóa, quản lý tài khoản và chỉ định chủ CLB. | ClubsController, UsersController, ClubOfficersController / ClubAdminService, UserAdminService, MembershipService | CreateClubRequest, UpdateClubRequest, ChangeClubStatusRequest, CreateUserRequest, UpdateUserRequest, AssignClubOfficerRequest |
| UC03 | CTSV tạo sự kiện cấp trường và mời CLB đang hoạt động. Hạn phản hồi trước giờ bắt đầu. | StaffEventsController / StaffEventService | CreateStaffEventRequest, InviteClubsRequest |
| UC04 | CTSV xem CLB xếp hạng theo tag, nhân sự và lịch sử hoàn thành. Có thể chọn thủ công. | ClubRecommendationsController / ClubRecommendationService | RecommendClubsRequest, RankedClubDto |
| UC05 | CTSV theo dõi số CLB nhận lời và tiến độ thực hiện | StaffEventsController / EventProgressQueryService | EventProgressDto |
| UC06 | Chủ CLB nhận hoặc từ chối lời mời. Khi nhận nhập số thành viên dự kiến, khi từ chối nhập lý do. Có thể đổi trước hạn. | ClubInvitationsController / ClubInvitationService | RespondInvitationRequest, ClubInvitationDto |
| UC07 | Chủ CLB tạo sự kiện nội bộ | ClubEventsController / ClubEventService | CreateInternalEventRequest, ClubEventDto |
| UC08–09 | Chủ CLB đăng tin tuyển, duyệt hoặc từ chối đơn kèm lý do | RecruitmentPostsController, JoinRequestsController / RecruitmentService, JoinRequestService | CreateRecruitmentPostRequest, ReviewJoinRequestRequest |
| UC10 | Chủ CLB giao việc cho một, nhiều hoặc toàn bộ thành viên đang hoạt động. Có thể lặp hằng ngày. | ClubTasksController / ClubTaskService | CreateClubTaskRequest, TaskRecurrenceDto, TaskAssignmentDto |
| UC11 | Chủ CLB gửi thông báo nội bộ | ClubAnnouncementsController / ClubAnnouncementService | CreateAnnouncementRequest, AnnouncementDto |
| UC12 | Chủ CLB theo dõi nhiệm vụ và đóng góp của thành viên trong CLB | ClubMembersController / ClubMemberProgressQueryService | MemberProgressDto |
| UC13–14 | Chủ CLB điểm danh, đối chiếu đơn xin vắng, xác nhận hoặc miễn phạt. Thành viên có thể khiếu nại trong 3 ngày. | AttendanceController, PenaltiesController / AttendanceService, PenaltyService | RecordAttendanceRequest, ResolvePenaltyRequest, AppealPenaltyRequest |
| UC15 / FR-03–04 | Sinh viên tìm CLB và sự kiện công khai | ClubsController, EventsController / ClubDirectoryQueryService, EventCatalogQueryService | ClubFilter, EventFilter, EventDetailDto |
| UC16 | Sinh viên nộp đơn vào CLB đang tuyển, còn chỗ, chưa là thành viên và chưa có đơn chờ | JoinRequestsController / JoinRequestService | SubmitJoinRequestRequest, JoinRequestDto |
| UC17 | Thành viên xem và cập nhật nhiệm vụ được giao | MyTasksController / TaskAssignmentService | UpdateAssignmentStatusRequest, MyTaskDto |
| UC18 / FR-05–06, 13 | Thành viên đăng ký sự kiện trong UC. FR thêm đăng ký của sinh viên, hủy trước giờ bắt đầu và danh sách cho chủ CLB. | EventRegistrationsController / EventRegistrationService | RegistrationDto, EventRosterItemDto |
| UC19 | Thành viên gửi đơn xin vắng cho sự kiện hoặc nhiệm vụ, có thể đính kèm minh chứng. Chủ CLB duyệt. Tài liệu cho phép bổ sung trong 48 giờ sau sự kiện. | AbsenceRequestsController / AbsenceRequestService | SubmitAbsenceRequest, ReviewAbsenceRequest, AbsenceRequestDto |
| UC20 | Thành viên xem thông báo trong phạm vi của mình | NotificationsController / NotificationService | NotificationDto |
| UC21 | Thành viên xem điểm đóng góp và vi phạm của mình | MyMembershipsController / MemberContributionQueryService | MyContributionDto, MyViolationDto |
| UC22 | Thành viên rời CLB | MyMembershipsController / MembershipService | MembershipDto |

### 3.2 Luồng FR cần endpoint riêng

| FR | Endpoint minh họa | Controller / Service | DTO |
|---|---|---|---|
| FR-07–09 | `POST /clubs/{clubId}/event-proposals`, `PUT /event-proposals/{id}`, `POST /event-proposals/{id}/submit`, `GET /clubs/{clubId}/event-proposals` | EventProposalsController / EventProposalService | SaveEventProposalRequest, EventProposalDetailDto |
| FR-10–12 | `POST /event-proposals/{id}/decisions` | EventProposalDecisionsController / EventProposalService | ReviewEventProposalRequest gồm decision, feedback và activityPoints khi duyệt |
| FR-05–06,13 | `POST /events/{id}/registrations`, `DELETE /events/{id}/registrations/me`, `GET /events/{id}/registrations` | EventRegistrationsController / EventRegistrationService | RegistrationDto, EventRosterItemDto |
| FR-14–16 | `PUT /events/{id}/attendance`, `POST /events/{id}/confirm-results`, `GET /me/activity-points` | AttendanceController, EventResultsController, MyActivityPointsController / AttendanceService, EventResultService, ActivityPointQueryService | RecordAttendanceRequest, EventResultDto, ActivityPointHistoryDto |
| FR-17 | `GET /me/notifications` và xử lý phía server khi có sự kiện nghiệp vụ | NotificationsController / NotificationService và worker | NotificationDto |
| FR-18 | `GET /reports/event-participation?clubId=…&from=…&to=…` | ReportsController / EventReportQueryService | EventParticipationReportFilter, EventParticipationReportDto |

Các endpoint lấy danh tính tác nhân từ phiên đã xác thực. Request DTO chứa dữ liệu đầu vào người dùng được phép sửa. Response DTO chỉ trả trường phù hợp với vai trò. Dùng `400` cho dữ liệu sai, `401` khi chưa xác thực, `403` khi thiếu quyền và `409` cho xung đột nghiệp vụ như hết chỗ, đã có đơn chờ hoặc đề xuất đã được xử lý.

## 4. Models và kế hoạch chỉnh schema

| Module | Models đề xuất | Công việc đối chiếu SQL |
|---|---|---|
| Accounts | ApplicationUser, UserSession (nếu cần), vai trò Student và Staff | SQL thiếu username, password hash và vai trò. Nếu dùng ASP.NET Core Identity, chốt mapping bảng và kiểu khóa trước migration. Chốt email/MSSV bắt buộc theo loại tài khoản và chính sách unique với giá trị NULL. |
| Clubs | Club, Membership, ClubField, ClubFieldLink, RecruitmentPost, JoinRequest | Chuẩn hóa `ClubMember` thành Membership ở domain, có thể giữ tên bảng bằng mapping. Bổ sung trạng thái chờ duyệt của CLB, cờ tuyển và giới hạn thành viên. Sửa CHECK status đang kiểm tra nhầm `Role`. Thêm unique `(ClubId, UserId)` và tối đa một owner đang hoạt động mỗi CLB nếu giả định một chủ được chốt. |
| Events | Event, EventProposal, ProposalReview, ClubInvitation, EventFieldLink, EventRegistration | Event cần nguồn tạo, CLB chủ trì nếu có, địa điểm, visibility, trạng thái công bố, sức chứa, hạn phản hồi, chỉ tiêu người và cờ bắt buộc tùy loại. `EventClub` thiếu FK ClubId. Thêm unique `(EventId, ClubId)`. Tách trạng thái lời mời khỏi tiến độ thực hiện. Bổ sung bảng đăng ký cá nhân theo UserId. |
| Tasks | ClubTask, TaskAssignment, TaskRecurrence | Chuyển assignee, trạng thái và giờ hoàn thành từ task sang assignment. Thêm mức ưu tiên, loại việc và lịch lặp. Mỗi occurrence có khóa duy nhất theo quy tắc và thời điểm dự kiến. |
| Attendance và điểm | Attendance, EventResult, ActivityPointEntry, ContributionPointEntry | SQL chưa có các bảng này. Điểm danh cấp sự kiện gắn theo người đủ điều kiện tham gia, với MembershipId khi phục vụ nội bộ. Duy nhất một bản điểm danh cho mỗi người mỗi sự kiện. Điểm hoạt động và điểm đóng góp có sổ riêng. |
| Kỷ luật | AbsenceRequest, Penalty, PenaltyAppeal | Mở rộng từ `Violation` để có nguồn phát sinh theo Event hoặc TaskAssignment, trạng thái, người xử lý và lý do. Đơn xin vắng có đúng một loại đối tượng. Chốt cách tính học kỳ và thời điểm tính cửa sổ 48 giờ. |
| Thông báo và audit | Announcement, Notification, AuditEntry, OutboxMessage (khi cần worker) | Announcement là bài đăng CLB, Notification là thông báo theo người nhận. `JoinRequest.ReviewedByUserId` và `Announcement.CreatedByUserId` hiện tham chiếu ClubMember.Id. Chọn UserId tham chiếu ApplicationUser hoặc đổi tên cột thành MembershipId, đồng thời kiểm tra membership cùng CLB khi dùng quan hệ này. |

Trước khi chạy migration, bỏ dấu phẩy cuối các danh sách cột/ràng buộc. Kiểm tra dữ liệu hiện có trước khi thêm FK, CHECK, unique index. Nếu cần giữ lịch sử tái gia nhập, kích hoạt lại Membership duy nhất và lưu các lần tham gia trong bảng lịch sử. Chỉ mục ưu tiên danh sách sự kiện đã công bố theo ngày và CLB, lượt đăng ký theo sự kiện/trạng thái và đơn theo CLB/trạng thái.

### Các giao dịch nghiệp vụ

1. **Duyệt đề xuất.** Từ Draft sang Pending. CTSV chuyển Pending sang ChangesRequested, Rejected hoặc Approved. ChangesRequested có thể sửa và gửi lại. Khi Approved, transaction lưu quyết định, điểm hoạt động, sự kiện đã công bố và thông báo cần gửi. Dùng ràng buộc duy nhất theo ProposalId để mỗi đề xuất sinh tối đa một sự kiện. Khóa cạnh tranh bằng version hoặc cập nhật có điều kiện theo trạng thái.
2. **Đăng ký và hủy.** Unique `(EventId, UserId)`. Trong transaction, tuần tự hóa các thay đổi sức chứa trên cùng sự kiện hoặc cập nhật bộ đếm có điều kiện rồi ghi đăng ký. Nếu có hủy và đăng ký lại, tái kích hoạt bản ghi trong cùng cơ chế. Truy vấn chỗ còn lại chỉ tính đăng ký đang hiệu lực. Chốt cutoff đăng ký trước khi triển khai.
3. **Duyệt gia nhập.** Kiểm tra lại sức chứa CLB và số CLB đang tham gia trong transaction. Tuần tự hóa quyết định có cùng CLB hoặc cùng sinh viên để hai yêu cầu đồng thời không vượt giới hạn. Lưu Membership, kết quả duyệt và audit cùng nhau.
4. **Điểm danh và cấp điểm.** Xác thực danh sách được đăng ký hoặc được giao hợp lệ. Chốt kết quả và ghi điểm hoạt động theo khóa duy nhất `(EventId, UserId)` khi CTSV xác nhận. Khóa việc điểm danh sau xác nhận theo quy tắc đề xuất, điều chỉnh nếu sai qua nghiệp vụ có audit. Điểm đóng góp và phạt gắn với Membership, có khóa theo nguồn nghiệp vụ để tránh cộng/trừ lặp. Hoàn hoặc điều chỉnh điểm bằng bút toán có lịch sử nếu được miễn phạt sau khi ghi.
5. **Lời mời và tác vụ nền.** Lời mời có trạng thái Pending, Accepted, Declined, NoResponse. Tiến độ như Preparing và Completed lưu riêng. Khi phản hồi, server kiểm tra hạn và quyền chủ CLB. Worker có thể chốt các lời mời Pending đã hết hạn và sinh việc định kỳ với khóa chống trùng. Công thức gợi ý mặc định 0,4 × khớp tag + 0,3 × đủ nhân sự + 0,3 × tỷ lệ hoàn thành, với các thành phần chuẩn hóa cùng thang điểm. Loại CLB khóa hoặc đang bị cảnh cáo khỏi danh sách gợi ý. Chốt cách tính cho CLB mới chưa có lịch sử, ngưỡng lọc và top N.

## 5. Cấu trúc và trách nhiệm

| Project | Trách nhiệm |
|---|---|
| ClubManagement.Api | ControllerBase, request/response DTO, validation hình dạng request, xác thực, policy và ánh xạ lỗi HTTP. Composition root đăng ký DI. |
| ClubManagement.Application | Service theo use case, command/query nghiệp vụ, kiểm tra phạm vi truy cập, điều phối giao dịch và các interface cho hạ tầng như IClock, INotificationPublisher, IEventRegistrationStore khi cần. |
| ClubManagement.Domain | Entity, enum, value object và quy tắc trạng thái, ví dụ Proposal.Submit và Proposal.RequestChanges, cùng quy tắc phạt/điểm thuần nghiệp vụ. |
| ClubManagement.Infrastructure | EF Core DbContext, mappings, migrations, truy vấn SQL Server, adapter gửi thông báo và worker. |
| ClubManagement.Tests | Unit test chuyển trạng thái và quy tắc, integration test API, SQL Server và giao dịch đồng thời. |

Domain độc lập. Application dùng Domain và định nghĩa hợp đồng hạ tầng cần dùng. Infrastructure hiện thực các hợp đồng. Api gọi Application và nối các implementation khi khởi động. Tổ chức thư mục bên trong theo Accounts, Clubs, Events, Tasks, Discipline, Notifications và Reporting. Đầu vào của Application là các kiểu nghiệp vụ độc lập với HTTP.

## 6. Design Pattern và SOLID

| Pattern | Áp dụng |
|---|---|
| Application Service | Điều phối luồng theo use case. EventRegistrationService quản lý đăng ký và hủy, EventProposalService xử lý đề xuất và quyết định, EventResultService xác nhận kết quả. |
| State machine | Biểu diễn bằng enum và phương thức chuyển trạng thái có kiểm tra. Dùng cho proposal, invitation và join request. Kiểm thử từng chuyển hợp lệ và chuyển bị từ chối. |
| Strategy | Cấu hình trọng số gợi ý và phạt bằng một implementation mặc định. Trích `IClubRecommendationScorer` hoặc `IPenaltyPolicy` khi có nhiều cách tính cần hoán đổi. |
| Repository và Unit of Work | Dùng DbContext làm đơn vị ghi trong hạ tầng. Tạo abstraction theo nhu cầu, ví dụ `IEventRegistrationStore` chịu trách nhiệm cho thao tác giữ chỗ nguyên tử. Dùng projection riêng cho báo cáo và danh mục. |
| Observer qua domain events và Transactional Outbox | Dùng cho ProposalDecided, EventPublished, StudentRegistered và PointsAwarded khi cần gửi bất đồng bộ hoặc retry. Lưu outbox cùng transaction và chống trùng theo event/recipient ở consumer. Với thông báo trong ứng dụng, có thể ghi Notification trực tiếp trong transaction. |

| Nguyên tắc | Cách kiểm tra khi review code |
|---|---|
| **S — Single Responsibility** | Đổi cách cấp điểm không cần sửa controller hoặc luồng duyệt đề xuất. |
| **O — Open/Closed** | Đổi trọng số qua cấu hình. Thêm cách xếp hạng hoặc kênh gửi mới qua implementation của hợp đồng tương ứng khi cần. |
| **L — Liskov Substitution** | Các implementation giữ cùng hợp đồng. Bộ lưu đăng ký vẫn bảo đảm tính nguyên tử với cơ sở dữ liệu thật. Dùng test theo hợp đồng và test concurrency trên SQL Server. |
| **I — Interface Segregation** | Dịch vụ báo cáo phụ thuộc vào read interface, dịch vụ đăng ký phụ thuộc vào thao tác đăng ký/hủy. |
| **D — Dependency Inversion** | Application phụ thuộc vào các hợp đồng do nó định nghĩa. EF Core, đồng hồ hệ thống và nhà cung cấp thông báo được gắn từ Infrastructure qua DI. |

## 7. Thứ tự triển khai và nghiệm thu

| Giai đoạn | Phạm vi | Điều kiện hoàn thành |
|---|---|---|
| 1. Nền tảng | Migration, account, quyền theo membership, CRUD CLB, phân công chủ CLB | Tài khoản bị vô hiệu hóa bị chặn theo chính sách phiên. Chủ CLB A bị từ chối khi sửa tài nguyên CLB B. |
| 2. Luồng công bố | Đề xuất, duyệt/yêu cầu sửa/từ chối, danh mục và chi tiết, đăng ký/hủy, danh sách đăng ký | Duyệt hai lần chỉ tạo một sự kiện. Hai request tranh chỗ cuối chỉ một request thành công. Sinh viên chỉ thấy sự kiện phù hợp. |
| 3. Kết quả và điểm | Điểm danh, CTSV xác nhận, lịch sử và tổng điểm, audit | Chỉ người có mặt được cấp điểm. Gửi lại yêu cầu xác nhận giữ nguyên tổng điểm. Truy được ai thực hiện và khi nào. |
| 4. CTSV điều phối | Tạo sự kiện cấp trường, gợi ý, mời, phản hồi và bảng tiến độ | Đổi phản hồi trước hạn thành công. Phản hồi sau hạn bị từ chối. Thống kê phản ánh lời mời đã nhận. |
| 5. Quản trị nội bộ | Tuyển, gia nhập/rời CLB, việc định kỳ, thông báo, xin vắng, phạt/khiếu nại, điểm đóng góp | Duyệt đồng thời không vượt giới hạn CLB. Worker chạy lại không sinh nhiệm vụ trùng. Vắng có phép và miễn phạt cập nhật đúng lịch sử. |
| 6. Nghiệm thu | Hoàn thiện FR-17 và FR-18, kiểm thử tải, quyền riêng tư, thiết bị, audit và khôi phục | Báo cáo lọc đúng theo CLB và kỳ. Phân quyền trường dữ liệu đúng. Đo NFR theo hồ sơ tải đã thống nhất. |

### Quyết định nghiệp vụ cần chốt

- **Ba luồng sự kiện.** Giữ đề xuất công khai do CLB gửi CTSV duyệt, sự kiện cấp trường CTSV mời CLB, và sự kiện nội bộ do CLB tạo. Đề xuất yêu cầu duyệt trước khi một sự kiện nội bộ muốn xuất hiện ở danh mục công khai.
- **Quyền đăng ký và xem dữ liệu.** Theo FR, mọi sinh viên đủ điều kiện có thể đăng ký sự kiện công khai. Giới hạn sự kiện nội bộ theo Membership. Theo UC21, cho thành viên xem vi phạm của chính mình. Chủ CLB có quyền trong CLB mình và CTSV có quyền theo nhiệm vụ quản lý. Chốt với nhóm vì mục quyền riêng tư của bản use case ghi chỉ Chủ CLB và CTSV xem điểm vi phạm.
- **Điểm và thời điểm.** Điểm hoạt động cấp trường chỉ phát sinh sau xác nhận CTSV theo FR-15. Điểm đóng góp nội bộ phát sinh theo quy tắc CLB, phù hợp với luồng cộng điểm sau điểm danh trong tài liệu UC. Chốt điểm ban đầu SQL đang để 100, việc cộng điểm đóng góp khi dự sự kiện, cách điều chỉnh điểm và cách xử lý đơn xin vắng duyệt muộn.
- **Vai trò và quy chế.** Xác nhận một hay nhiều chủ CLB, xử lý UC22 khi người rời là chủ, mặc định tối đa 5 CLB, mức phạt −5/−2, ngưỡng cảnh cáo 3 lần và đề xuất xem xét khai trừ 5 lần trong học kỳ. Khai trừ cần quyết định của chủ CLB. Chốt sử dụng tài khoản riêng hay SSO.
- **Chuẩn nghiệm thu.** Chốt 500 người đồng thời và mốc 5.000 sinh viên. Thống nhất mục tiêu hiệu năng trang thường, danh sách sự kiện và bảng tiến độ theo từng màn hình. Thống nhất các chức năng Should cần có trong bản nộp.
