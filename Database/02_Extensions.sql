SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

ALTER TABLE ApplicationUser ADD
    Username varchar(100) NULL, PasswordHash varchar(100) NULL,
    SystemRole varchar(20) NOT NULL CONSTRAINT DF_User_Role DEFAULT 'Student',
    CONSTRAINT CK_User_Role CHECK (SystemRole IN ('Student', 'Staff'));
CREATE UNIQUE INDEX UX_User_Username ON ApplicationUser(Username) WHERE Username IS NOT NULL;
CREATE UNIQUE INDEX UX_User_Email ON ApplicationUser(Email) WHERE Email IS NOT NULL;
CREATE UNIQUE INDEX UX_User_StudentCode ON ApplicationUser(StudentCode) WHERE StudentCode IS NOT NULL;

ALTER TABLE Club ADD IsRecruiting bit NOT NULL DEFAULT 0, MaxMembers int NOT NULL DEFAULT 100;
CREATE UNIQUE INDEX UX_Membership ON ClubMember(ClubId, UserId);
CREATE UNIQUE INDEX UX_ActiveOwner ON ClubMember(ClubId) WHERE Role = 'Owner' AND Status = 'Active';
ALTER TABLE JoinRequest ADD ReviewReason nvarchar(1000) NULL;
CREATE UNIQUE INDEX UX_PendingJoin ON JoinRequest(ClubId, UserId) WHERE Status = 'Pending';

CREATE TABLE EventProposal (
    Id varchar(500) PRIMARY KEY,
    ClubId varchar(500) NOT NULL REFERENCES Club(Id),
    CreatedByUserId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    Title nvarchar(256) NOT NULL, Objective nvarchar(1000) NOT NULL,
    Description nvarchar(max) NOT NULL, Venue nvarchar(256) NOT NULL,
    StartTime datetime NOT NULL, EndTime datetime NOT NULL, Capacity int NOT NULL CHECK (Capacity > 0),
    Status varchar(30) NOT NULL CHECK (Status IN ('Draft', 'Pending', 'ChangesRequested', 'Approved', 'Rejected'))
);
CREATE TABLE ProposalReview (
    Id varchar(500) PRIMARY KEY, ProposalId varchar(500) NOT NULL REFERENCES EventProposal(Id),
    ActorId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    Decision varchar(30) NOT NULL, Feedback nvarchar(2000) NULL, CreatedAt datetime NOT NULL
);
ALTER TABLE Event ADD
    ClubId varchar(500) NULL REFERENCES Club(Id),
    ProposalId varchar(500) NULL REFERENCES EventProposal(Id),
    Kind varchar(30) NOT NULL DEFAULT 'Staff',
    Venue nvarchar(256) NOT NULL DEFAULT '',
    Capacity int NOT NULL DEFAULT 1 CHECK (Capacity > 0),
    ActivityPoints int NOT NULL DEFAULT 0 CHECK (ActivityPoints >= 0),
    IsPublished bit NOT NULL DEFAULT 0, IsMandatory bit NOT NULL DEFAULT 0,
    ResponseDeadline datetime NULL, MinimumMembersPerClub int NOT NULL DEFAULT 0;
CREATE UNIQUE INDEX UX_Event_Proposal ON Event(ProposalId) WHERE ProposalId IS NOT NULL;
CREATE INDEX IX_Event_Catalog ON Event(IsPublished, StartTime);
CREATE INDEX IX_Event_Club ON Event(ClubId, StartTime);
ALTER TABLE EventClub ADD ExpectedMembers int NULL, Progress varchar(30) NULL;
CREATE UNIQUE INDEX UX_EventClub ON EventClub(EventId, ClubId);

CREATE TABLE EventRegistration (
    Id varchar(500) PRIMARY KEY, EventId varchar(500) NOT NULL REFERENCES Event(Id),
    UserId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    RegisteredAt datetime NOT NULL, CancelledAt datetime NULL
);
CREATE UNIQUE INDEX UX_EventRegistration ON EventRegistration(EventId, UserId);
CREATE TABLE Attendance (
    Id varchar(500) PRIMARY KEY, EventId varchar(500) NOT NULL REFERENCES Event(Id),
    UserId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    Present bit NOT NULL, RecordedByUserId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    RecordedAt datetime NOT NULL
);
CREATE UNIQUE INDEX UX_Attendance ON Attendance(EventId, UserId);
CREATE TABLE EventResult (
    EventId varchar(500) PRIMARY KEY REFERENCES Event(Id),
    ConfirmedByUserId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    ConfirmedAt datetime NOT NULL
);
CREATE TABLE ActivityPointEntry (
    Id varchar(500) PRIMARY KEY, EventId varchar(500) NOT NULL REFERENCES Event(Id),
    UserId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    Points int NOT NULL, CreatedAt datetime NOT NULL
);
CREATE UNIQUE INDEX UX_ActivityPoint ON ActivityPointEntry(EventId, UserId);
CREATE INDEX IX_ActivityPoint_User ON ActivityPointEntry(UserId, CreatedAt);

CREATE TABLE UserSession (
    Id varchar(500) PRIMARY KEY, UserId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    RefreshHash varchar(64) NOT NULL, AccessExpiresAt datetime2 NOT NULL,
    RefreshExpiresAt datetime2 NOT NULL, RevokedAt datetime2 NULL
);
CREATE UNIQUE INDEX UX_Session_Refresh ON UserSession(RefreshHash);
CREATE TABLE Notification (
    Id varchar(500) PRIMARY KEY, UserId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    ClubId varchar(500) NULL REFERENCES Club(Id), Message nvarchar(1000) NOT NULL,
    CreatedAt datetime NOT NULL, ReadAt datetime NULL
);
CREATE INDEX IX_Notification_User ON Notification(UserId, CreatedAt);
CREATE TABLE AuditEntry (
    Id varchar(500) PRIMARY KEY, ActorId varchar(500) NOT NULL REFERENCES ApplicationUser(Id),
    Action varchar(100) NOT NULL, TargetId varchar(500) NOT NULL, CreatedAt datetime NOT NULL
);
COMMIT;
