(() => {
  const button = document.querySelector('#profile-button');
  if (!button) return;

  document.body.insertAdjacentHTML('beforeend', `
    <div id="staff-profile-modal" style="display:none;position:fixed;inset:0;z-index:10000;background:rgba(15,23,42,.55);align-items:center;justify-content:center;padding:16px;">
      <section role="dialog" aria-modal="true" aria-labelledby="staff-profile-title" style="width:min(100%,480px);background:#fff;border-radius:14px;padding:24px;box-shadow:0 20px 50px rgba(15,23,42,.25);">
        <div style="display:flex;justify-content:space-between;align-items:center;gap:12px;">
          <h2 id="staff-profile-title" style="margin:0;">Hồ sơ cá nhân</h2>
          <button id="staff-profile-close" type="button" aria-label="Đóng hồ sơ">✕</button>
        </div>
        <p style="color:#64748b;font-size:.9rem;">Hồ sơ giúp đồng nghiệp nhận biết người phụ trách và liên hệ công việc. Thông tin này không thay thế hồ sơ chuyên môn của bác sĩ.</p>
        <form id="staff-profile-form">
          <label style="display:block;margin:12px 0;">Tên đăng nhập
            <input id="staff-profile-username" readonly style="display:block;width:100%;padding:10px;margin-top:5px;">
          </label>
          <label style="display:block;margin:12px 0;">Vai trò
            <input id="staff-profile-role" readonly style="display:block;width:100%;padding:10px;margin-top:5px;">
          </label>
          <label style="display:block;margin:12px 0;">Họ và tên
            <input id="staff-profile-fullname" maxlength="120" required style="display:block;width:100%;padding:10px;margin-top:5px;">
          </label>
          <label style="display:block;margin:12px 0;">Email
            <input id="staff-profile-email" type="email" maxlength="256" style="display:block;width:100%;padding:10px;margin-top:5px;">
          </label>
          <label style="display:block;margin:12px 0;">Số điện thoại
            <input id="staff-profile-phone" type="tel" maxlength="30" style="display:block;width:100%;padding:10px;margin-top:5px;">
          </label>
          <section id="doctor-photo-section" style="display:none;margin:16px 0;padding:14px;border:1px solid #e2e8f0;border-radius:10px;">
            <h3 style="margin:0 0 10px;">Ảnh hồ sơ bác sĩ</h3>
            <img id="doctor-profile-photo-preview" alt="Ảnh hồ sơ bác sĩ" style="display:none;width:96px;height:96px;object-fit:cover;border-radius:50%;margin-bottom:10px;">
            <p style="margin:0 0 10px;color:#64748b;font-size:.85rem;">Bệnh nhân sẽ thấy ảnh này khi chọn bác sĩ. Chấp nhận JPEG, PNG hoặc WebP, tối đa 1 MB.</p>
            <input id="doctor-profile-photo-file" type="file" accept="image/jpeg,image/png,image/webp">
            <button id="doctor-profile-photo-upload" type="button" style="margin-top:10px;">Tải ảnh lên</button>
            <p id="doctor-profile-photo-message" role="status" style="min-height:1.4em;margin-bottom:0;"></p>
          </section>
          <p id="staff-profile-message" role="status" style="min-height:1.4em;"></p>
          <div style="display:flex;justify-content:flex-end;gap:8px;">
            <button id="staff-profile-cancel" type="button">Đóng</button>
            <button type="submit">Lưu hồ sơ</button>
          </div>
        </form>
      </section>
    </div>
  `);

  const modal = document.querySelector('#staff-profile-modal');
  const form = document.querySelector('#staff-profile-form');
  const message = document.querySelector('#staff-profile-message');
  const close = () => { modal.style.display = 'none'; };
  const request = async (method, body) => {
    const response = await fetch('/api/auth/profile', {
      method,
      headers: body ? { 'Content-Type': 'application/json' } : {},
      body: body ? JSON.stringify(body) : undefined
    });
    const data = await response.json().catch(() => null);
    if (!response.ok) throw new Error(data?.message || 'Không thể cập nhật hồ sơ.');
    return data;
  };

  button.addEventListener('click', async () => {
    modal.style.display = 'flex';
    message.textContent = 'Đang tải hồ sơ...';
    try {
      const profile = await request('GET');
      document.querySelector('#staff-profile-username').value = profile.username;
      document.querySelector('#staff-profile-role').value = profile.role;
      document.querySelector('#staff-profile-fullname').value = profile.fullName;
      document.querySelector('#staff-profile-email').value = profile.email;
      document.querySelector('#staff-profile-phone').value = profile.phone;
      const photoSection = document.querySelector('#doctor-photo-section');
      photoSection.style.display = profile.role === 'Doctor' ? 'block' : 'none';
      if (profile.role === 'Doctor') {
        const accountResponse = await fetch('/api/auth/me');
        const account = await accountResponse.json().catch(() => null);
        if (!accountResponse.ok) throw new Error(account?.message || 'Không thể tải thông tin bác sĩ.');
        if (!account?.doctorId) throw new Error('Tài khoản này chưa được Admin gắn với hồ sơ bác sĩ.');
        const doctorResponse = await fetch(`/api/doctors/${account.doctorId}`);
        const doctor = await doctorResponse.json().catch(() => null);
        if (!doctorResponse.ok) throw new Error(doctor?.message || 'Không thể tải hồ sơ bác sĩ.');
        const preview = document.querySelector('#doctor-profile-photo-preview');
        preview.src = doctor.profileImageUrl || '';
        preview.style.display = doctor.profileImageUrl ? 'block' : 'none';
        document.querySelector('#doctor-profile-photo-file').value = '';
        document.querySelector('#doctor-profile-photo-message').textContent = '';
      }
      message.textContent = '';
    } catch (error) {
      message.textContent = error.message;
    }
  });

  document.querySelector('#staff-profile-close').addEventListener('click', close);
  document.querySelector('#staff-profile-cancel').addEventListener('click', close);
  modal.addEventListener('click', event => { if (event.target === modal) close(); });

  document.querySelector('#doctor-profile-photo-upload').addEventListener('click', async () => {
    const input = document.querySelector('#doctor-profile-photo-file');
    const status = document.querySelector('#doctor-profile-photo-message');
    const file = input.files[0];
    if (!file) {
      status.textContent = 'Vui lòng chọn ảnh trước khi tải lên.';
      return;
    }

    status.textContent = 'Đang tải ảnh...';
    try {
      const formData = new FormData();
      formData.append('file', file);
      const response = await fetch('/api/doctors/my-profile/photo', { method: 'POST', body: formData });
      const data = await response.json().catch(() => null);
      if (!response.ok) throw new Error(data?.message || 'Không thể tải ảnh lên.');
      const preview = document.querySelector('#doctor-profile-photo-preview');
      preview.src = data.profileImageUrl;
      preview.style.display = 'block';
      const accountPhoto = document.querySelector('#doctor-account-photo');
      if (accountPhoto) {
        accountPhoto.src = data.profileImageUrl;
        accountPhoto.style.display = 'block';
      }
      input.value = '';
      status.textContent = 'Đã cập nhật ảnh. Bệnh nhân sẽ thấy ảnh này khi chọn bác sĩ.';
    } catch (error) {
      status.textContent = error.message;
    }
  });

  form.addEventListener('submit', async event => {
    event.preventDefault();
    message.textContent = 'Đang lưu...';
    try {
      const profile = await request('PUT', {
        fullName: document.querySelector('#staff-profile-fullname').value,
        email: document.querySelector('#staff-profile-email').value,
        phone: document.querySelector('#staff-profile-phone').value
      });
      message.textContent = 'Đã lưu hồ sơ.';
      const welcome = document.querySelector('#welcome');
      if (welcome) {
        if (profile.role === 'Receptionist')
          welcome.textContent = `Xin chào ${profile.fullName} (${profile.role}).`;
        else if (profile.role === 'Doctor')
          welcome.textContent = `Xin chào Bác sĩ ${profile.fullName}. Chúc bạn có một ngày làm việc hiệu quả.`;
        else if (profile.role === 'Support')
          welcome.textContent = `Xin chào ${profile.fullName}. Bộ phận Hỗ trợ & CSKH.`;
      }
      const profileName = document.querySelector('#profile-name');
      if (profileName) profileName.textContent = profile.fullName;
    } catch (error) {
      message.textContent = error.message;
    }
  });
})();
