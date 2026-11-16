
/*
DROP TABLE IF EXIStS JoinRequest;
DROP TABLE IF EXIStS Announcement;
DROP TABLE IF EXIStS Event;
DROP TABLE IF EXIStS EventClub;
DROP TABLE IF EXIStS ClubTask;
DROP TABLE IF EXIStS Violation;
DROP TABLE IF EXIStS ApplicationUser;
DROP TABLE IF EXIStS Club;
DROP TABLE IF EXIStS ClubMember;
DROP TABLE IF EXIStS ClubField;
*/

CREATE TABLE ApplicationUser (
	Id VARCHAR(500) PRIMARY KEY,
	Email VARCHAR(256) UNIQUE,
	FullName NVARCHAR(256) NOT NULL,
	StudentCode VARCHAR(100) UNIQUE,
	IsActive BIT NOT NULL,
	CreatedAt DATETIME NOT NULL,
);

CREATE TABLE ClubField(
	Id VARCHAR(500) PRIMARY KEY,
	Name VARCHAR(256) UNIQUE,
);

CREATE TABLE Club(
	Id VARCHAR(500) PRIMARY KEY,
	Name NVARCHAR(256) UNIQUE,
	Description NVARCHAR(MAX) NOT NULL,
	Field VARCHAR(500) NOT NULL,
	Status VARCHAR(50) NOT NULL,
	CreatedAt DATETIME NOT NULL,

	CONSTRAINT FK_Club_Field FOREIGN KEY (Field) REFERENCES ClubField(Id),
	CONSTRAINT CK_Club_Status CHECK (Status IN ('Active', 'Suspended')),
);

CREATE TABLE ClubMember(
	Id VARCHAR(500) PRIMARY KEY,
	ClubId VARCHAR(500) NOT NULL,
	UserId VARCHAR(500) NOT NULL,
	Role VARCHAR(50) NOT NULL,
	Status VARCHAR(50) NOT NULL,
	Points INT DEFAULT(100) NOT NULL,
	JoinedAt DATETIME NOT NULL,

	CONSTRAINT FK_ClubMember_ClubId FOREIGN KEY(ClubId) REFERENCES Club(Id),
	CONSTRAINT FK_ClubMember_UserId FOREIGN KEY(UserId) REFERENCES ApplicationUser(Id),
	CONSTRAINT CK_ClubMember_Role CHECK (Role IN ('Owner', 'Member')),
	CONSTRAINT CK_ClubMember_Status CHECK (Role IN ('Active', 'Removed')),
);

CREATE TABLE JoinRequest(
	Id VARCHAR(500) PRIMARY KEY,
	ClubId VARCHAR(500) NOT NULL,
	UserId VARCHAR(500) NOT NULL,
	Message NVARCHAR(MAX) NOT NULL,
	Status VARCHAR(50) DEFAULT('Pending') NOT NULL,
	ReviewedByUserId VARCHAR(500),
	ReviewedAt DATETIME,
	CreatedAt DATETIME NOT NULL,

	CONSTRAINT FK_JoinRequest_ClubId FOREIGN KEY(ClubId) REFERENCES Club(Id),
	CONSTRAINT FK_JoinRequest_UserId FOREIGN KEY(UserId) REFERENCES ApplicationUser(Id),
	CONSTRAINT FK_JoinRequest_ReviewedByUserId FOREIGN KEY(ReviewedByUserId) REFERENCES ClubMember(Id),
	CONSTRAINT CK_JoinRequest_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected')),

);

CREATE TABLE Announcement(
	Id VARCHAR(500) PRIMARY KEY,
	ClubId VARCHAR(500) NOT NULL,
	CreatedByUserId VARCHAR(500) NOT NULL,
	Title NVARCHAR(256) NOT NULL,
	Content NVARCHAR(MAX) NOT NULL,
	IsImportant BIT NOT NULL,
	CreatedAt DATETIME NOT NULL,

	CONSTRAINT FK_Announcement_ClubId FOREIGN KEY(ClubId) REFERENCES Club(Id),
	CONSTRAINT FK_Announcement_CreatedByUserId FOREIGN KEY(CreatedByUserId) REFERENCES ClubMember(Id),
);

CREATE TABLE Event(
	Id VARCHAR(500) PRIMARY KEY,
	CreatedByUserId VARCHAR(500),
	Title NVARCHAR(256) NOT NULL,
	Description NVARCHAR(MAX) NOT NULL,
	Requirements NVARCHAR(MAX) NOT NULL,
	RequiredClubCount INT NOT NULL,
	StartTime DATETIME NOT NULL,
	EndTime DATETIME,
	CreatedAt DATETIME,

	CONSTRAINT FK_Event_CreatedByUserId FOREIGN KEY(CreatedByUserId) REFERENCES ApplicationUser(Id),
);

CREATE TABLE EventClubFieldRelation (
	EventId VARCHAR(500) NOT NULL,
	Field VARCHAR(500) NOT NULL,
	CONSTRAINT PK_EventClubFieldRelation PRIMARY KEY (EventId, Field),
	CONSTRAINT FK_EventClubFieldRelation_EventId FOREIGN KEY (EventId) REFERENCES Event(Id),
	CONSTRAINT FK_EventClubFieldRelation_Field FOREIGN KEY (Field) REFERENCES ClubField(Id),
);

CREATE TABLE EventClub(
	Id VARCHAR(500) PRIMARY KEY,
	EventId VARCHAR(500) NOT NULL,
	ClubId VARCHAR(500) NOT NULL,
	Status VARCHAR(50) NOT NULL,
	Note NVARCHAR(MAX) NOT NULL,
	UpdatedAt DATETIME NOT NULL,
	CreatedAt DATETIME NOT NULL,

	CONSTRAINT FK_EventClub_EventId FOREIGN KEY(EventId) REFERENCES Event(Id),
	CONSTRAINT CK_EventClub_Status CHECK (Status IN ('Registered', 'Preparing', 'Completed', 'Declined')),
);
CREATE TABLE ClubTask(
	Id VARCHAR(500) PRIMARY KEY,
	ClubId VARCHAR(500) NOT NULL,
	AssigneeMemberId VARCHAR(500) NOT NULL,
	CreatedByUserId VARCHAR(500) NOT NULL,
	EventId VARCHAR(500),
	Title NVARCHAR(256) NOT NULL,
	Description NVARCHAR(MAX) NOT NULL,
	Deadline DATETIME NOT NULL,
	Status VARCHAR(50) NOT NULL,
	Points INT NOT NULL,
	CompletedAt DATETIME,
	CreatedAt DATETIME,

	CONSTRAINT FK_ClubTask_ClubId FOREIGN KEY (ClubId) REFERENCES Club(Id),
	CONSTRAINT FK_ClubTask_AssigneeMemberId FOREIGN KEY (AssigneeMemberId) REFERENCES ClubMember(Id),
	CONSTRAINT FK_ClubTask_CreatedByUserId FOREIGN KEY (CreatedByUserId) REFERENCES ApplicationUser(Id),
	CONSTRAINT FK_ClubTask_EventId FOREIGN KEY (EventId) REFERENCES Event(Id),
	CONSTRAINT CK_ClubTask_Status CHECK (Status IN ('Todo', 'InProgress', 'Done')),
);
CREATE TABLE Violation(
	Id VARCHAR(500) PRIMARY KEY,
	ClubMemberId VARCHAR(500) NOT NULL,
	RecordedByUserId VARCHAR(500) NOT NULL,
	ClubTaskId VARCHAR(500),
	Reason NVARCHAR(MAX) NOT NULL,
	PointsDeducted INT NOT NULL,
	CreatedAt DATETIME NOT NULL,

	CONSTRAINT FK_Violation_ClubMemberId FOREIGN KEY (ClubMemberId) REFERENCES ClubMember(Id),
	CONSTRAINT FK_Violation_RecordedByUserId FOREIGN KEY (RecordedByUserId) REFERENCES ApplicationUser(Id),
	CONSTRAINT FK_Violation_ClubTaskId FOREIGN KEY (ClubTaskId) REFERENCES ClubTask(Id),
);
