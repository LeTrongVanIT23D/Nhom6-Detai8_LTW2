
Set-Location $PSScriptRoot

$adminPassword = Read-Host "Nhap mat khau cho tai khoan admin" -AsSecureString
$doctorPassword = Read-Host "Nhap mat khau cho tai khoan doctor" -AsSecureString
$receptionistPassword = Read-Host "Nhap mat khau cho tai khoan letu" -AsSecureString
$supportPassword = Read-Host "Nhap mat khau cho tai khoan support" -AsSecureString

function Set-LocalSecret($key, $securePassword) {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    try {
        $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        dotnet user-secrets set $key $plainPassword
        if ($LASTEXITCODE -ne 0) {
            throw "Khong the luu secret: $key"
        }
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

Set-LocalSecret "AdminPassword" $adminPassword
Set-LocalSecret "DoctorPassword" $doctorPassword
Set-LocalSecret "ReceptionistPassword" $receptionistPassword
Set-LocalSecret "SupportPassword" $supportPassword

Write-Host "Da cau hinh tai khoan local trong .NET User Secrets."
Write-Host "Username admin: admin"
Write-Host "Username doctor: doctor"
