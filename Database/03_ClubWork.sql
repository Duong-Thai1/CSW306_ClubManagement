SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

CREATE TABLE RecruitmentPost (
    Id varchar(500) PRIMARY KEY, ClubId varchar(500) NOT NULL REFERENCES Club(Id),
    Title nvarchar(256) NOT NULL, Content nvarchar(max) NOT NULL, Deadline datetime NOT NULL
);
ALTER TABLE ClubTask DROP CONSTRAINT FK_ClubTask_AssigneeMemberId;
ALTER TABLE ClubTask ALTER COLUMN AssigneeMemberId varchar(500) NULL;
ALTER TABLE ClubTask ADD CONSTRAINT FK_ClubTask_AssigneeMemberId FOREIGN KEY (AssigneeMemberId) REFERENCES ClubMember(Id);
ALTER TABLE ClubTask ADD
    Priority varchar(15) NOT NULL DEFAULT 'Normal',
    FrequencyDays int NULL CHECK (FrequencyDays > 0), RepeatUntil datetime NULL,
    TemplateId varchar(500) NULL REFERENCES ClubTask(Id), OccursAt datetime NULL;
CREATE UNIQUE INDEX UX_RecurringTask ON ClubTask(TemplateId, OccursAt) WHERE TemplateId IS NOT NULL;

CREATE TABLE TaskAssignment (
    Id varchar(500) PRIMARY KEY, ClubTaskId varchar(500) NOT NULL REFERENCES ClubTask(Id),
    MembershipId varchar(500) NOT NULL REFERENCES ClubMember(Id),
    Status varchar(20) NOT NULL CHECK (Status IN ('Todo', 'InProgress', 'Done')),
    CompletedAt datetime NULL
);
CREATE UNIQUE INDEX UX_TaskAssignment ON TaskAssignment(ClubTaskId, MembershipId);
INSERT INTO TaskAssignment(Id, ClubTaskId, MembershipId, Status, CompletedAt)
SELECT CONVERT(varchar(36), NEWID()), Id, AssigneeMemberId, Status, CompletedAt
FROM ClubTask WHERE AssigneeMemberId IS NOT NULL;

CREATE TABLE AbsenceRequest (
    Id varchar(500) PRIMARY KEY, MembershipId varchar(500) NOT NULL REFERENCES ClubMember(Id),
    EventId varchar(500) NULL REFERENCES Event(Id), TaskAssignmentId varchar(500) NULL REFERENCES TaskAssignment(Id),
    Reason nvarchar(2000) NOT NULL, EvidenceReference nvarchar(500) NULL,
    Status varchar(20) NOT NULL CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
    IsLate bit NOT NULL, CreatedAt datetime NOT NULL, ReviewedAt datetime NULL,
    ReviewedByUserId varchar(500) NULL REFERENCES ApplicationUser(Id),
    CHECK ((EventId IS NOT NULL AND TaskAssignmentId IS NULL) OR (EventId IS NULL AND TaskAssignmentId IS NOT NULL))
);
CREATE UNIQUE INDEX UX_AbsenceEvent ON AbsenceRequest(MembershipId, EventId) WHERE EventId IS NOT NULL;
CREATE UNIQUE INDEX UX_AbsenceTask ON AbsenceRequest(MembershipId, TaskAssignmentId) WHERE TaskAssignmentId IS NOT NULL;

ALTER TABLE Violation ADD
    AttendanceId varchar(500) NULL REFERENCES Attendance(Id),
    Status varchar(20) NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active', 'Waived', 'Appealed')),
    ResolutionReason nvarchar(2000) NULL, AppealReason nvarchar(2000) NULL, AppealedAt datetime NULL;
CREATE UNIQUE INDEX UX_EventViolation ON Violation(ClubMemberId, AttendanceId) WHERE AttendanceId IS NOT NULL;
CREATE UNIQUE INDEX UX_TaskViolation ON Violation(ClubMemberId, ClubTaskId) WHERE ClubTaskId IS NOT NULL;

CREATE TABLE ContributionPointEntry (
    Id varchar(500) PRIMARY KEY, MembershipId varchar(500) NOT NULL REFERENCES ClubMember(Id),
    SourceType varchar(30) NOT NULL, SourceId varchar(500) NOT NULL, Points int NOT NULL, CreatedAt datetime NOT NULL
);
CREATE UNIQUE INDEX UX_ContributionSource ON ContributionPointEntry(MembershipId, SourceId, SourceType);
COMMIT;
