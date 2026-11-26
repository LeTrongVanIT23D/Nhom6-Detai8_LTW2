#!/bin/bash
echo "=========================================================="
echo ">>> DANG KIEM TRA HE THONG VA KHOI TAO LINK TRUY CAP..."
echo "=========================================================="

# 1. Kiem tra va khoi dong lai dich vu Medika neu can
sudo systemctl daemon-reload 2>/dev/null || true
sudo systemctl restart medika 2>/dev/null || true
sleep 2

# 2. Dam bao co file thuc thi cloudflared
if [ ! -f /tmp/cloudflared ] || [ $(wc -c < /tmp/cloudflared) -lt 1000000 ]; then
    echo "Dang tai Cloudflared..."
    curl -sL https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64 -o /tmp/cloudflared
    chmod +x /tmp/cloudflared
fi

# 3. Don dep tien trinh cu va khoi dong tunnel moi
sudo killall -9 cloudflared 2>/dev/null || true
rm -f /tmp/tunnel.log

nohup /tmp/cloudflared tunnel --url http://localhost:80 > /tmp/tunnel.log 2>&1 &

echo -n "Dang tao link truy cap truc tiep (cho khoang 5 - 10 giay)"
URL=""
for i in {1..20}; do
    echo -n "."
    sleep 1
    URL=$(grep -o 'https://[-a-z0-9]*\.trycloudflare\.com' /tmp/tunnel.log 2>/dev/null | head -n 1)
    if [ -n "$URL" ]; then
        break
    fi
done
echo ""

if [ -n "$URL" ]; then
    echo ""
    echo "=========================================================="
    echo "🎉 KHOI TAO THANH CONG! LINK MAY CHU CUA BAN:"
    echo "=========================================================="
    echo "👉 TRANG DANG NHAP: $URL/login.html"
    echo "👉 TRANG SWAGGER:   $URL/swagger/index.html"
    echo "👉 TRANG QUAN TRI:  $URL/admin.html"
    echo "=========================================================="
    echo "Tai khoan: admin  |  Mat khau: 123"
    echo "=========================================================="
else
    echo ""
    echo "⚠️ CHUA LAY DUOC LINK CLOUDFLARE, NOI DUNG LOG HIEN TAI:"
    echo "----------------------------------------------------------"
    cat /tmp/tunnel.log
    echo "----------------------------------------------------------"
fi
