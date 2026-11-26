param (
    [Parameter(Mandatory=$true)]
    [string]$Ec2PublicIp,

    [Parameter(Mandatory=$true)]
    [string]$PemFilePath
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $PemFilePath)) {
    Write-Error "Không tìm thấy file PEM tại: $PemFilePath"
    exit 1
}

# Đảm bảo phân quyền file PEM an toàn trên Windows
try {
    icacls.exe $PemFilePath /inheritance:r | Out-Null
    icacls.exe $PemFilePath /grant:r "$($env:USERNAME):(R)" | Out-Null
} catch {}

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host ">>> Đang chuẩn bị deploy dự án Medika Clinic lên EC2: $Ec2PublicIp" -ForegroundColor Cyan

# 1. Nén source code cần thiết (bỏ qua bin, obj, publish)
$ZipPath = Join-Path $env:TEMP "medika-clinic-deploy.zip"
if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }

Write-Host ">>> Đang nén mã nguồn dự án..." -ForegroundColor Yellow
$itemsToZip = Get-ChildItem -Path $ProjectDir -Exclude "bin", "obj", "publish*", ".vs", ".git"
Compress-Archive -Path $itemsToZip.FullName -DestinationPath $ZipPath -Force

# 2. Upload mã nguồn lên EC2 qua SCP
Write-Host ">>> Đang tải file lên EC2 qua SCP..." -ForegroundColor Yellow
$sshTarget = "ubuntu@$Ec2PublicIp"
scp -o StrictHostKeyChecking=no -i $PemFilePath $ZipPath "${sshTarget}:~/medika-clinic-deploy.zip"

# 3. Giải nén và chạy script setup trên EC2
Write-Host ">>> Đang khởi chạy và build container trên EC2..." -ForegroundColor Yellow
$remoteCommands = "sudo apt-get update -y && sudo apt-get install -y unzip && rm -rf ~/app && mkdir -p ~/app && unzip -q -o ~/medika-clinic-deploy.zip -d ~/app && rm -f ~/medika-clinic-deploy.zip && chmod +x ~/app/setup-ec2.sh && cd ~/app && ./setup-ec2.sh"

ssh -o StrictHostKeyChecking=no -i $PemFilePath $sshTarget $remoteCommands

Write-Host ">>> HOÀN TẤT! Hãy mở trình duyệt và truy cập: http://$Ec2PublicIp" -ForegroundColor Green
