-- Script SQL tạo 500 người dùng mẫu để kiểm tra tính năng Tìm kiếm, Lọc và Phân trang (Pagination)
DECLARE @i INT;
SELECT @i = ISNULL(MAX(Id), 0) + 1 FROM [Users];
DECLARE @Username NVARCHAR(50);
DECLARE @Email NVARCHAR(100);
DECLARE @FullName NVARCHAR(100);
DECLARE @Role NVARCHAR(20);
 
WHILE @i <= 500 -- (Nếu muốn nhập tới 500 user)
BEGIN
    SET @Username = CONCAT('user', @i);
    SET @Email = CONCAT('user', @i, '@example.com');
    SET @FullName = CONCAT(N'Người dùng ', @i);
    
    IF @i % 15 = 0
        SET @Role = 'Admin';
    ELSE IF @i % 5 = 0
        SET @Role = 'Manager';
    ELSE
        SET @Role = 'User';

    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE Username = @Username OR Email = @Email)
    BEGIN
        INSERT INTO [Users] ([Username], [Email], [PasswordHash], [Role], [FullName], [CreatedAt], [IsLocked], [RewardPoints])
        VALUES (
            @Username, 
            @Email, 
            '$2a$11$Eo6xzba8Xc9dswG5Rae8x.hof7US1ax3kyMLvC1eLbiNSximlGOMC', -- Mật khẩu mặc định
            @Role, 
            @FullName, 
            DATEADD(DAY, -(@i % 365), GETUTCDATE()),
            0,
            0
        );
    END

    SET @i = @i + 1;
END;

