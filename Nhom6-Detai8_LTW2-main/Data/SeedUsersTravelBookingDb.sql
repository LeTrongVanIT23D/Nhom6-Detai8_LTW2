-- Script SQL tự động tạo 500 User mẫu cho Database TravelBookingDb
USE TravelBookingDb;
GO

-- 1. Tạo tài khoản Admin mặc định (mật khẩu 123)
IF NOT EXISTS (SELECT 1 FROM [Users] WHERE Username = 'admin')
BEGIN
    INSERT INTO [Users] ([Username], [Email], [PasswordHash], [Phone], [Role], [IsActive], [CreatedAt], [UpdatedAt])
    VALUES (
        'admin',
        'admin@travel.local',
        '$2a$11$Eo6xzba8Xc9dswG5Rae8x.hof7US1ax3kyMLvC1eLbiNSximlGOMC', -- Password: 123
        '0901234567',
        'Admin',
        1,
        GETUTCDATE(),
        GETUTCDATE()
    );
END
GO

-- 2. Vòng lặp tạo 500 User từ user1 đến user500
DECLARE @i INT = 1;
DECLARE @Username NVARCHAR(50);
DECLARE @Email NVARCHAR(100);
DECLARE @Role NVARCHAR(20);

WHILE @i <= 500
BEGIN
    SET @Username = CONCAT('user', @i);
    SET @Email = CONCAT('user', @i, '@example.com');
    
    IF @i % 15 = 0
        SET @Role = 'Admin';
    ELSE IF @i % 5 = 0
        SET @Role = 'Manager';
    ELSE
        SET @Role = 'User';

    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE Username = @Username OR Email = @Email)
    BEGIN
        INSERT INTO [Users] ([Username], [Email], [PasswordHash], [Phone], [Role], [IsActive], [CreatedAt], [UpdatedAt])
        VALUES (
            @Username, 
            @Email, 
            '$2a$11$Eo6xzba8Xc9dswG5Rae8x.hof7US1ax3kyMLvC1eLbiNSximlGOMC', -- Password: 123
            CONCAT('09', RIGHT(CONCAT('00000000', @i), 8)),
            @Role, 
            1,
            DATEADD(DAY, -(@i % 365), GETUTCDATE()),
            GETUTCDATE()
        );
    END

    SET @i = @i + 1;
END
GO

SELECT COUNT(*) AS [TotalUsersInTravelBookingDb] FROM [Users];
GO

