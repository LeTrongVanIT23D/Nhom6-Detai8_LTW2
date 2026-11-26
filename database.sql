SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'StudentManagementDb')
BEGIN
    CREATE DATABASE [StudentManagementDb];
END;
GO
USE [StudentManagementDb];
GO
CREATE TABLE [AuditLogs] (
    [Id] int NOT NULL IDENTITY,
    [Action] nvarchar(max) NOT NULL,
    [PerformedBy] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [Details] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [DoctorLeaveRequests] (
    [Id] int NOT NULL IDENTITY,
    [DoctorId] int NOT NULL,
    [LeaveDate] datetime2 NOT NULL,
    [Shift] nvarchar(max) NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_DoctorLeaveRequests] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [DoctorSchedules] (
    [Id] int NOT NULL IDENTITY,
    [DoctorId] int NOT NULL,
    [DayOfWeek] int NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [IsDayOff] bit NOT NULL,
    CONSTRAINT [PK_DoctorSchedules] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MedicalRecords] (
    [Id] int NOT NULL IDENTITY,
    [PatientId] int NOT NULL,
    [DoctorId] int NOT NULL,
    [AppointmentId] int NULL,
    [ExaminedAt] datetime2 NOT NULL,
    [Symptoms] nvarchar(max) NOT NULL,
    [Diagnosis] nvarchar(max) NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    [BloodPressure] nvarchar(max) NOT NULL,
    [HeartRate] int NULL,
    [Temperature] float NULL,
    [Weight] float NULL,
    [TreatmentPlan] nvarchar(max) NOT NULL,
    [FollowUpDate] datetime2 NULL,
    [LabTests] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_MedicalRecords] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [OtcMedicines] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Category] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [Unit] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [UsageInstructions] nvarchar(max) NOT NULL,
    [ImageUrl] nvarchar(500) NOT NULL,
    [StockQuantity] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_OtcMedicines] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Patients] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NULL,
    [FullName] nvarchar(max) NOT NULL,
    [DateOfBirth] date NOT NULL,
    [Phone] nvarchar(max) NOT NULL,
    [Address] nvarchar(max) NOT NULL,
    [MedicalHistory] nvarchar(max) NOT NULL,
    [InsuranceNumber] nvarchar(max) NOT NULL,
    [RewardPoints] int NOT NULL,
    CONSTRAINT [PK_Patients] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Prescriptions] (
    [Id] int NOT NULL IDENTITY,
    [MedicalRecordId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Medicines] nvarchar(max) NOT NULL,
    [Instructions] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Prescriptions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [StoredFiles] (
    [Id] int NOT NULL IDENTITY,
    [FileName] nvarchar(max) NOT NULL,
    [ContentType] nvarchar(max) NOT NULL,
    [Content] varbinary(max) NOT NULL,
    [Length] bigint NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_StoredFiles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [SupportMessages] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [Subject] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [Reply] nvarchar(max) NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SupportMessages] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Appointments] (
    [Id] int NOT NULL IDENTITY,
    [PatientId] int NOT NULL,
    [DoctorId] int NOT NULL,
    [UserId] int NULL,
    [ReceptionistId] int NULL,
    [QueueNumber] int NULL,
    [AppointmentDate] datetime2 NOT NULL,
    [BookingSource] nvarchar(20) NOT NULL DEFAULT N'InPerson',
    [Reason] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [BookingFee] decimal(18,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [PointsUsed] int NOT NULL,
    [BookingFeeIsPaid] bit NOT NULL,
    [BookingFeePaymentMethod] nvarchar(30) NOT NULL,
    [BookingFeePaidAt] datetime2 NULL,
    [BookingFeePaymentReference] nvarchar(100) NOT NULL,
    [BookingFeeRefundDue] bit NOT NULL,
    [BookingFeeRefunded] bit NOT NULL,
    [BookingFeeForfeited] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Appointments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Appointments_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Invoices] (
    [Id] int NOT NULL IDENTITY,
    [AppointmentId] int NOT NULL,
    [PatientId] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PaymentMethod] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [PaidAt] datetime2 NULL,
    CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Invoices_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Invoices_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ChatMessages] (
    [Id] int NOT NULL IDENTITY,
    [RoomId] int NULL,
    [SenderId] int NOT NULL,
    [ReceiverId] int NOT NULL,
    [RoomPatientId] int NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [IsFromSupport] bit NOT NULL,
    [IsRead] bit NOT NULL,
    [SentAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ChatRooms] (
    [Id] int NOT NULL IDENTITY,
    [PatientUserId] int NOT NULL,
    [SupportUserId] int NULL,
    [RoomCode] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_ChatRooms] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Doctors] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NULL,
    [FullName] nvarchar(max) NOT NULL,
    [Specialty] nvarchar(max) NOT NULL,
    [Phone] nvarchar(max) NOT NULL,
    [Room] nvarchar(max) NOT NULL,
    [Degree] nvarchar(max) NOT NULL,
    [ExperienceYears] int NOT NULL,
    [ProfileImageUrl] nvarchar(500) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Doctors] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [FullName] nvarchar(max) NOT NULL,
    [Username] nvarchar(max) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Phone] nvarchar(30) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [DoctorId] int NULL,
    [RewardPoints] int NOT NULL,
    [IsLocked] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Users_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id])
);
GO


CREATE TABLE [Notifications] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [IsRead] bit NOT NULL,
    [LinkAction] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [OtcOrders] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ReceiverName] nvarchar(200) NOT NULL,
    [ReceiverPhone] nvarchar(30) NOT NULL,
    [DeliveryAddress] nvarchar(500) NOT NULL,
    [FulfillmentMethod] nvarchar(20) NOT NULL,
    [Status] nvarchar(30) NOT NULL,
    [PaymentMethod] nvarchar(30) NOT NULL,
    [IsPaid] bit NOT NULL,
    [PaymentConfirmedAt] datetime2 NULL,
    [PaymentReference] nvarchar(100) NOT NULL,
    [SubTotal] decimal(18,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [FinalAmount] decimal(18,2) NOT NULL,
    [PointsUsed] int NOT NULL,
    [PointsEarned] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CompletedAt] datetime2 NULL,
    CONSTRAINT [PK_OtcOrders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OtcOrders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Reviews] (
    [Id] int NOT NULL IDENTITY,
    [DoctorId] int NOT NULL,
    [PatientId] int NOT NULL,
    [UserId] int NULL,
    [AppointmentId] int NULL,
    [Rating] int NOT NULL,
    [Comment] nvarchar(max) NOT NULL,
    [IsHidden] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reviews_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]),
    CONSTRAINT [FK_Reviews_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reviews_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);
GO


CREATE TABLE [OtcOrderItems] (
    [Id] int NOT NULL IDENTITY,
    [OtcOrderId] int NOT NULL,
    [MedicineId] int NOT NULL,
    [MedicineName] nvarchar(max) NOT NULL,
    [MedicineUnit] nvarchar(max) NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Quantity] int NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OtcOrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OtcOrderItems_OtcMedicines_MedicineId] FOREIGN KEY ([MedicineId]) REFERENCES [OtcMedicines] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OtcOrderItems_OtcOrders_OtcOrderId] FOREIGN KEY ([OtcOrderId]) REFERENCES [OtcOrders] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_Appointments_DoctorId] ON [Appointments] ([DoctorId]);
GO


CREATE INDEX [IX_Appointments_PatientId] ON [Appointments] ([PatientId]);
GO


CREATE INDEX [IX_Appointments_ReceptionistId] ON [Appointments] ([ReceptionistId]);
GO


CREATE INDEX [IX_Appointments_UserId] ON [Appointments] ([UserId]);
GO


CREATE INDEX [IX_ChatMessages_RoomId] ON [ChatMessages] ([RoomId]);
GO


CREATE INDEX [IX_ChatRooms_PatientUserId] ON [ChatRooms] ([PatientUserId]);
GO


CREATE INDEX [IX_ChatRooms_SupportUserId] ON [ChatRooms] ([SupportUserId]);
GO


CREATE INDEX [IX_Doctors_UserId] ON [Doctors] ([UserId]);
GO


CREATE INDEX [IX_Invoices_AppointmentId] ON [Invoices] ([AppointmentId]);
GO


CREATE INDEX [IX_Invoices_PatientId] ON [Invoices] ([PatientId]);
GO


CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
GO


CREATE INDEX [IX_OtcOrderItems_MedicineId] ON [OtcOrderItems] ([MedicineId]);
GO


CREATE INDEX [IX_OtcOrderItems_OtcOrderId] ON [OtcOrderItems] ([OtcOrderId]);
GO


CREATE INDEX [IX_OtcOrders_UserId] ON [OtcOrders] ([UserId]);
GO


CREATE INDEX [IX_Reviews_AppointmentId] ON [Reviews] ([AppointmentId]);
GO


CREATE INDEX [IX_Reviews_DoctorId] ON [Reviews] ([DoctorId]);
GO


CREATE INDEX [IX_Reviews_PatientId] ON [Reviews] ([PatientId]);
GO


CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);
GO


SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_DoctorId' AND object_id = OBJECT_ID('Users'))
BEGIN
    CREATE UNIQUE INDEX [IX_Users_DoctorId] ON [Users] ([DoctorId]) WHERE [DoctorId] IS NOT NULL;
END;
GO


ALTER TABLE [Appointments] ADD CONSTRAINT [FK_Appointments_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION;
GO


ALTER TABLE [Appointments] ADD CONSTRAINT [FK_Appointments_Users_ReceptionistId] FOREIGN KEY ([ReceptionistId]) REFERENCES [Users] ([Id]);
GO


ALTER TABLE [Appointments] ADD CONSTRAINT [FK_Appointments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]);
GO


ALTER TABLE [ChatMessages] ADD CONSTRAINT [FK_ChatMessages_ChatRooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [ChatRooms] ([Id]) ON DELETE CASCADE;
GO


ALTER TABLE [ChatRooms] ADD CONSTRAINT [FK_ChatRooms_Users_PatientUserId] FOREIGN KEY ([PatientUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
GO


ALTER TABLE [ChatRooms] ADD CONSTRAINT [FK_ChatRooms_Users_SupportUserId] FOREIGN KEY ([SupportUserId]) REFERENCES [Users] ([Id]);
GO


ALTER TABLE [Doctors] ADD CONSTRAINT [FK_Doctors_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL;
GO



