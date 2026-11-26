#!/bin/bash
set -e

echo "=== 1. Tối ưu bộ nhớ Swap cho AWS EC2 Free Tier (4GB) ==="
if [ ! -f /swapfile ]; then
    sudo fallocate -l 4G /swapfile || sudo dd if=/dev/zero of=/swapfile bs=1M count=4096
    sudo chmod 600 /swapfile
    sudo mkswap /swapfile
    sudo swapon /swapfile
    echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
    echo "Swap 4GB đã được kích hoạt thành công."
else
    echo "Swap file đã tồn tại."
fi

echo "=== 2. Cài đặt Docker & Docker Compose ==="
sudo apt-get update -y
sudo apt-get install -y ca-certificates curl gnupg lsb-release

if ! command -v docker &> /dev/null; then
    sudo mkdir -p /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg --yes
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
    sudo apt-get update -y
    sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    sudo usermod -aG docker $USER
fi

echo "=== 3. Khởi chạy Medika Clinic App & Database ==="
cd ~/app
sudo docker compose down || true
sudo docker compose up -d --build

echo "=== 4. Kiểm tra trạng thái containers ==="
sudo docker compose ps

echo "=========================================================="
echo "DEPLOY THÀNH CÔNG! Web đang chạy tại: http://$(curl -s ifconfig.me)"
echo "=========================================================="
