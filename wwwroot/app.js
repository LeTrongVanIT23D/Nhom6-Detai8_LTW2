const apiUrl = '/api/patients';
const patientTable = document.querySelector('#patient-table');
const appointmentTable = document.querySelector('#appointment-table');
const searchInput = document.querySelector('#search-input');
const statusFilter = document.querySelector('#appointment-status-filter');
const modal = document.querySelector('#patient-modal');
const form = document.querySelector('#patient-form');
const toast = document.querySelector('#toast');

// Modals
const deskBookingModal = document.querySelector('#desk-booking-modal');
const deskBookingForm = document.querySelector('#desk-booking-form');
const rescheduleModal = document.querySelector('#reception-reschedule-modal');
const rescheduleForm = document.querySelector('#reception-reschedule-form');
const invoiceModal = document.querySelector('#invoice-modal');
const invoiceForm = document.querySelector('#invoice-form');

let patients = [];
let appointments = [];
let doctors = [];
let currentBookingBalance = 0;

async function init() {
  const dateStr = new Intl.DateTimeFormat('vi-VN', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }).format(new Date());
  document.querySelector('#current-date-label').textContent = dateStr;

  await Promise.all([loadDoctors(), loadPatients(), loadAppointments(), loadOtcOrders()]);
  initSignalRRealtime();
}

async function loadDoctors() {
  doctors = await fetch('/api/doctors').then(res => res.json()).catch(() => []);
}

async function loadPatients() {
  const response = await fetch(apiUrl);
  if (!response.ok) throw new Error('Không thể tải danh sách bệnh nhân.');
  patients = await response.json();
  renderPatients();
  renderAppointments();
  document.querySelector('#total-count').textContent = String(patients.length);
}

function renderPatients() {
  const keyword = searchInput.value.trim().toLowerCase();
  const visiblePatients = patients.filter(patient =>
    `${patient.fullName} ${patient.phone} ${patient.insuranceNumber || ''}`.toLowerCase().includes(keyword));
  patientTable.innerHTML = visiblePatients.length ? visiblePatients.map(patient => `
    <tr>
      <td>
        <div class="patient-name">
          <span class="patient-avatar">${initials(patient.fullName)}</span>
          <div>
            ${escapeHtml(patient.fullName)}
            ${patient.insuranceNumber ? `<br><small style="color:#0284c7;font-weight:600;">BHYT: ${escapeHtml(patient.insuranceNumber)}</small>` : ''}
          </div>
        </div>
      </td>
      <td>${formatDate(patient.dateOfBirth)}</td>
      <td>${escapeHtml(patient.phone)}</td>
      <td>${escapeHtml(patient.address)}</td>
      <td><span class="history" title="${escapeHtml(patient.medicalHistory)}">${escapeHtml(patient.medicalHistory || 'Chưa cập nhật')}</span></td>
      <td>
        <div class="row-actions">
          <button class="action-button" title="Đặt lịch tại quầy" onclick="bookForPatientDirectly(${patient.id})">📅</button>
          <button class="action-button" title="Chỉnh sửa" onclick="editPatient(${patient.id})">✎</button>
          <button class="action-button" title="Xóa" onclick="deletePatient(${patient.id})">⌫</button>
        </div>
      </td>
    </tr>`).join('') : '<tr><td colspan="6" class="empty">Không tìm thấy bệnh nhân.</td></tr>';
  document.querySelector('#result-count').textContent = `Hiển thị ${visiblePatients.length} / ${patients.length} hồ sơ`;
}

async function loadAppointments() {
  const response = await fetch('/api/appointments');
  if (!response.ok) return;
  appointments = await response.json();
  renderAppointments();

  // Update stats
  const pendingCount = appointments.filter(a => a.status === 'Pending').length;
  const checkedInCount = appointments.filter(a => a.status === 'CheckedIn').length;
  const completedCount = appointments.filter(a => a.status === 'Completed').length;

  document.querySelector('#pending-count').textContent = String(pendingCount);
  document.querySelector('#checkedin-count').textContent = String(checkedInCount);
  document.querySelector('#completed-count').textContent = String(completedCount);
}
window.loadAppointments = loadAppointments;

function getStatusBadge(status) {
  switch (status) {
    case 'Pending': return '<span class="badge-status badge-pending">⏳ Chờ duyệt</span>';
    case 'Confirmed': return '<span class="badge-status badge-confirmed">✓ Đã duyệt</span>';
    case 'CheckedIn': return '<span class="badge-status badge-checkedin">🏥 Đã Check-in</span>';
    case 'Completed': return '<span class="badge-status badge-completed">★ Hoàn thành</span>';
    case 'Cancelled': return '<span class="badge-status badge-cancelled">✕ Đã hủy</span>';
    case 'NoShow': return '<span class="badge-status badge-cancelled">Không đến khám</span>';
    case 'NeedReschedule': return '<span class="badge-status badge-pending" style="background:#fee2e2;color:#991b1b;">⚠ Cần đổi lịch</span>';
    default: return `<span class="badge-status">${escapeHtml(status)}</span>`;
  }
}

function isAppointmentToday(appointmentDate) {
  const date = new Date();
  const today = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  return String(appointmentDate).slice(0, 10) === today;
}

function renderAppointments() {
  const filterVal = statusFilter.value;
  const visible = filterVal ? appointments.filter(a => a.status === filterVal) : appointments;

  if (!visible.length) {
    appointmentTable.innerHTML = '<tr><td colspan="7" class="empty">Không có lịch hẹn nào.</td></tr>';
    return;
  }

  appointmentTable.innerHTML = visible.map(item => {
    const patient = patients.find(p => p.id === item.patientId);
    const pName = patient ? patient.fullName : (item.patientName || `Bệnh nhân #${item.patientId}`);
    const pPhone = patient ? patient.phone : '';

    const doctor = doctors.find(d => d.id === item.doctorId);
    const dName = doctor ? doctor.fullName : (item.doctorName || `Bác sĩ #${item.doctorId}`);

    const isPending = item.status === 'Pending';
    const isConfirmed = item.status === 'Confirmed';
    const isCheckedIn = item.status === 'CheckedIn';
    const isCompleted = item.status === 'Completed';
    const isActive = ['Pending', 'Confirmed', 'NeedReschedule'].includes(item.status);
    const hasUnpaidBookingFee = item.bookingSource === 'Online' &&
      item.bookingFee > item.discountAmount &&
      !item.bookingFeeIsPaid;
    const canCheckInToday = isAppointmentToday(item.appointmentDate);
    const checkInAction = hasUnpaidBookingFee
      ? '<button class="btn-action btn-checkin" disabled title="Cần đối soát phí giữ chỗ trước khi check-in">🏥 Chờ thanh toán phí</button>'
      : canCheckInToday
      ? `<button class="btn-action btn-checkin" onclick="checkInAppointment(${item.id}, this)">🏥 Check-in (Xếp STT)</button>`
      : '<button class="btn-action btn-checkin" disabled title="Chỉ check-in vào đúng ngày hẹn">🏥 Check-in vào ngày hẹn</button>';

    let actionsHtml = '';
    if (item.bookingSource === 'Online' && item.bookingFee > item.discountAmount) {
      if (!item.bookingFeeIsPaid && isPending) {
        actionsHtml += `<button class="btn-action btn-confirm" onclick="confirmBookingTransfer(${item.id})">Đối soát phí giữ chỗ</button>`;
      }
      if (item.bookingFeeRefundDue) {
        actionsHtml += `<button class="btn-action btn-confirm" onclick="confirmBookingRefund(${item.id})">Xác nhận đã hoàn phí</button>`;
      }
    }
    if (isPending) {
      if (!hasUnpaidBookingFee) {
        actionsHtml += `<button class="btn-action btn-confirm" onclick="confirmAppointment(${item.id})">✓ Duyệt</button>`;
      }
      actionsHtml += checkInAction;
    }
    if (isConfirmed) {
      actionsHtml += checkInAction;
    }
    if (isCheckedIn) {
      actionsHtml += `<button class="btn-action" style="background:#059669;color:#fff;" onclick="callPatientQueue(${item.id}, ${item.queueNumber || 0}, '${escapeJs(pName)}')">🔊 Gọi vào khám</button>`;
    }
    if (isCompleted) {
      actionsHtml += `<button class="btn-action" style="background:#059669;color:#fff;" onclick="openInvoiceModal(${item.id}, '${escapeJs(pName)}')">💳 Viện phí / Thu tiền</button>`;
    }
    if (isActive) {
      actionsHtml += `<button class="btn-action" style="background:#f59e0b;color:#fff;" onclick="openRescheduleModal(${item.id}, '${escapeJs(pName)}', '${item.appointmentDate}')">📅 Đổi lịch</button>`;
      actionsHtml += `<button class="btn-action" style="background:#dc2626;color:#fff;" onclick="cancelAppointmentByClinic(${item.id})">Hủy lịch</button>`;
    }
    if ((isPending || isConfirmed) && new Date(item.appointmentDate) <= new Date()) {
      actionsHtml += `<button class="btn-action" style="background:#7c2d12;color:#fff;" onclick="markAppointmentNoShow(${item.id})">Ghi nhận bỏ hẹn</button>`;
    }
    return `
      <tr>
        <td><strong style="font-size:1.05rem;color:#1e40af;">${item.queueNumber ? `#${item.queueNumber}` : '---'}</strong></td>
        <td><b>${escapeHtml(pName)}</b><br><small style="color:#64748b;">${escapeHtml(pPhone)}</small><br><span class="badge-status">${item.bookingSource === 'Online' ? 'Đặt trên website' : 'Đặt tại quầy'}</span></td>
        <td>${escapeHtml(dName)}</td>
        <td>${new Date(item.appointmentDate).toLocaleString('vi-VN')}</td>
        <td>${escapeHtml(item.reason)}${item.bookingSource === 'Online' && item.bookingFee > item.discountAmount ? `<br><small>${item.bookingFeeIsPaid ? 'Phí giữ chỗ đã thanh toán' : `Chờ chuyển khoản ${Math.max(0, item.bookingFee - item.discountAmount).toLocaleString('vi-VN')}đ`} · Nội dung: MEDIKA LICH ${item.id}</small>` : ''}${item.bookingFeeRefunded ? '<br><small>Đã hoàn phí giữ chỗ</small>' : ''}${item.bookingFeeForfeited ? '<br><small>Phí giữ chỗ bị mất theo chính sách</small>' : ''}</td>
        <td>${getStatusBadge(item.status)}</td>
        <td>${actionsHtml}</td>
      </tr>
    `;
  }).join('');
}

async function loadOtcOrders() {
  const table = document.querySelector('#otc-orders-table');
  if (!table) return;
  const response = await fetch('/api/otc-medicines/orders');
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new Error(error.message || 'Không thể tải đơn OTC.');
  }
  const orders = await response.json();
  if (!orders.length) {
    table.innerHTML = '<tr><td colspan="7" class="empty">Chưa có đơn đặt trước thuốc OTC.</td></tr>';
    return;
  }

  const statusLabels = {
    Pending: 'Chờ xác nhận',
    ReadyForPickup: 'Chờ nhận tại quầy',
    OutForDelivery: 'Đang giao',
    Completed: 'Hoàn tất · Đã thanh toán',
    Cancelled: 'Đã hủy'
  };
  table.innerHTML = orders.map(order => {
    const items = order.items.map(item => `${escapeHtml(item.medicineName)} × ${item.quantity}`).join('<br>');
    let action = '';
    if (order.status === 'Pending') {
      if (!order.isPaid) {
        action = `<button class="btn-action btn-confirm" onclick="confirmOtcPayment(${order.id})">Đối soát chuyển khoản</button>`;
      } else {
        const next = order.fulfillmentMethod === 'Pickup' ? 'ReadyForPickup' : 'OutForDelivery';
        action = `<button class="btn-action btn-confirm" onclick="updateOtcOrderStatus(${order.id}, '${next}')">Xác nhận đơn</button>`;
      }
    } else if (order.status === 'ReadyForPickup' || order.status === 'OutForDelivery') {
      action = `<button class="btn-action btn-checkin" onclick="updateOtcOrderStatus(${order.id}, 'Completed')">Hoàn tất giao/nhận thuốc</button>`;
    }

    return `<tr>
      <td>#${order.id}<br><small>${new Date(order.createdAt).toLocaleString('vi-VN')}</small></td>
      <td><b>${escapeHtml(order.receiverName)}</b><br><small>${escapeHtml(order.receiverPhone)}</small>${order.deliveryAddress ? `<br><small>${escapeHtml(order.deliveryAddress)}</small>` : ''}</td>
      <td>${items}</td>
      <td>${order.fulfillmentMethod === 'Pickup' ? 'Nhận tại quầy' : 'Giao hàng'}</td>
      <td>${order.finalAmount.toLocaleString('vi-VN')} đ<br><small>${order.isPaid ? 'Đã thanh toán' : 'Chưa thanh toán'}</small></td>
      <td>${escapeHtml(statusLabels[order.status] || order.status)}</td>
      <td>${action || '—'}</td>
    </tr>`;
  }).join('');
}

window.updateOtcOrderStatus = async (id, status) => {
  const prompt = status === 'Completed'
    ? 'Xác nhận đã giao/trao thuốc cho bệnh nhân?'
    : 'Xác nhận đã tiếp nhận đơn OTC?';
  if (!confirm(prompt)) return;
  try {
    const response = await fetch(`/api/otc-medicines/orders/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status })
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.message || 'Không thể cập nhật đơn OTC.');
    showToast('Đã cập nhật trạng thái đơn OTC.');
    await loadOtcOrders();
  } catch (error) {
    showToast(error.message);
  }
};

window.confirmOtcPayment = async id => {
  const transactionReference = prompt('Chỉ xác nhận sau khi đã thấy đúng số tiền vào BIDV. Nhập mã giao dịch nếu có (tối đa 100 ký tự):');
  if (transactionReference === null) return;
  if (transactionReference.trim().length > 100) {
    showToast('Mã giao dịch không được dài quá 100 ký tự.');
    return;
  }
  try {
    const response = await fetch(`/api/otc-medicines/orders/${id}/payment/confirm`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ transactionReference })
    });
    const data = await response.json().catch(() => null);
    if (!response.ok) {
      const validationErrors = data?.errors
        ? Object.values(data.errors).flat().join(' ')
        : '';
      throw new Error(data?.message || data?.title || validationErrors || `Máy chủ từ chối yêu cầu (HTTP ${response.status}).`);
    }
    showToast(data?.isPaid ? 'Đơn thuốc đã được xác nhận thanh toán.' : 'Đã đối soát thanh toán đơn thuốc.');
    try {
      await loadOtcOrders();
    } catch (error) {
      showToast(`Đã xác nhận thanh toán, nhưng không tải lại danh sách được: ${error.message}`);
    }
  } catch (error) {
    showToast(error.message);
  }
};

window.confirmAppointment = async id => {
  try {
    const res = await fetch(`/api/appointments/${id}/confirm`, { method: 'PATCH' });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.message || 'Duyệt lịch thất bại.');
    showToast('✓ Đã duyệt lịch hẹn thành công!');
    await loadAppointments();
  } catch (err) {
    showToast(err.message);
  }
};

window.confirmBookingTransfer = async id => {
  const transactionReference = prompt('Chỉ xác nhận sau khi đã thấy đúng số tiền vào BIDV. Nhập mã giao dịch nếu có:');
  if (transactionReference === null) return;
  try {
    const response = await fetch(`/api/appointments/${id}/payment/confirm`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ transactionReference })
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.message || 'Không thể xác nhận chuyển khoản.');
    showToast('Đã xác nhận thanh toán phí giữ chỗ.');
    await loadAppointments();
  } catch (error) {
    showToast(error.message);
  }
};

window.confirmBookingRefund = async id => {
  if (!confirm('Xác nhận phòng khám đã chuyển hoàn phí giữ chỗ cho bệnh nhân?')) return;
  try {
    const response = await fetch(`/api/appointments/${id}/refund/confirm`, { method: 'PATCH' });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.message || 'Không thể cập nhật hoàn phí.');
    showToast('Đã ghi nhận hoàn phí giữ chỗ.');
    await loadAppointments();
  } catch (error) {
    showToast(error.message);
  }
};

window.cancelAppointmentByClinic = async id => {
  if (!confirm('Hủy lịch này? Nếu bệnh nhân đã chuyển khoản, phí sẽ được đưa vào danh sách cần hoàn.')) return;
  try {
    const response = await fetch(`/api/appointments/${id}/cancel-by-clinic`, { method: 'PATCH' });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.message || 'Không thể hủy lịch.');
    showToast('Đã hủy lịch. Kiểm tra khoản phí đang chờ hoàn nếu có.');
    await loadAppointments();
  } catch (error) {
    showToast(error.message);
  }
};

window.markAppointmentNoShow = async id => {
  if (!confirm('Xác nhận bệnh nhân không đến khám? Phí giữ chỗ sẽ không được hoàn.')) return;
  try {
    const response = await fetch(`/api/appointments/${id}/no-show`, { method: 'PATCH' });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.message || 'Không thể ghi nhận bỏ hẹn.');
    showToast('Đã ghi nhận bệnh nhân bỏ hẹn.');
    await loadAppointments();
  } catch (error) {
    showToast(error.message);
  }
};

window.checkInAppointment = async (id, button) => {
  const originalText = button?.textContent;
  if (button) {
    button.disabled = true;
    button.textContent = 'Đang check-in...';
  }
  showToast('Đang check-in và xếp số thứ tự...');

  try {
    const res = await fetch(`/api/appointments/${id}/check-in`, { method: 'PATCH' });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.message || `Check-in thất bại (HTTP ${res.status}).`);
    showToast(`🏥 Check-in thành công! Bệnh nhân nhận STT: #${data.queueNumber ?? 'đã xếp'}`);
    await loadAppointments();
  } catch (err) {
    showToast(`Không thể check-in: ${err.message || 'Lỗi kết nối tới máy chủ.'}`);
  } finally {
    if (button?.isConnected) {
      button.disabled = false;
      button.textContent = originalText;
    }
  }
};

window.callPatientQueue = (id, queueNumber, pName) => {
  const speech = new SpeechSynthesisUtterance(`Mời bệnh nhân ${pName}, số thứ tự ${queueNumber}, vào phòng khám.`);
  speech.lang = 'vi-VN';
  window.speechSynthesis?.speak(speech);
  showToast(`🔊 Đang phát loa gọi bệnh nhân: ${pName} (STT #${queueNumber})`);
};

window.deleteAppointment = async id => {
  if (!confirm('Bạn có chắc muốn xóa lịch hẹn này?')) return;
  try {
    const res = await fetch(`/api/appointments/${id}`, { method: 'DELETE' });
    if (!res.ok) throw new Error('Xóa thất bại.');
    showToast('Đã xóa lịch hẹn.');
    await loadAppointments();
  } catch (err) {
    showToast(err.message);
  }
};

/* --- ĐẶT LỊCH TẠI QUẦY (DESK BOOKING) --- */
function openDeskBookingModal(patientId = null) {
  const patientSelect = document.querySelector('#desk-patient-id');
  const doctorSelect = document.querySelector('#desk-doctor-id');

  // Fill patients
  patientSelect.innerHTML = '<option value="">-- Chọn bệnh nhân --</option>' +
    patients.map(p => `<option value="${p.id}" ${patientId === p.id ? 'selected' : ''}>${escapeHtml(p.fullName)} - ${escapeHtml(p.phone)}</option>`).join('');

  // Fill doctors
  doctorSelect.innerHTML = '<option value="">-- Chọn bác sĩ --</option>' +
    doctors.map(d => `<option value="${d.id}">${escapeHtml(d.fullName)} (${escapeHtml(d.specialty)} - ${escapeHtml(d.room)})</option>`).join('');

  // Default datetime = now + 15 mins
  const now = new Date(Date.now() + 15 * 60000);
  now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
  document.querySelector('#desk-appt-date').value = now.toISOString().slice(0, 16);

  deskBookingModal.classList.remove('hidden');
}

window.bookForPatientDirectly = (patientId) => openDeskBookingModal(patientId);

document.querySelector('#open-book-desk')?.addEventListener('click', () => openDeskBookingModal());
document.querySelector('#close-desk-booking')?.addEventListener('click', () => deskBookingModal.classList.add('hidden'));
document.querySelector('#cancel-desk-booking')?.addEventListener('click', () => deskBookingModal.classList.add('hidden'));

deskBookingForm?.addEventListener('submit', async e => {
  e.preventDefault();
  const patientId = Number(document.querySelector('#desk-patient-id').value);
  const doctorId = Number(document.querySelector('#desk-doctor-id').value);
  const appointmentDate = document.querySelector('#desk-appt-date').value;
  const reason = document.querySelector('#desk-reason').value.trim();
  const autoCheckIn = document.querySelector('#desk-auto-checkin').checked;

  try {
    const res = await fetch('/api/appointments', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ patientId, doctorId, appointmentDate, reason })
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.message || 'Không thể tạo lịch hẹn tại quầy.');
    }
    const createdAppt = await res.json();

    if (autoCheckIn && createdAppt.id) {
      // Xác nhận và check-in ngay
      await fetch(`/api/appointments/${createdAppt.id}/confirm`, { method: 'PATCH' });
      const checkInRes = await fetch(`/api/appointments/${createdAppt.id}/check-in`, { method: 'PATCH' });
      const checkedData = await checkInRes.json();
      showToast(`✓ Đã tạo lịch và Check-in ngay! STT khám: #${checkedData.queueNumber}`);
    } else {
      showToast('✓ Đã tạo lịch hẹn tại quầy thành công!');
    }

    deskBookingModal.classList.add('hidden');
    deskBookingForm.reset();
    await loadAppointments();
  } catch (err) {
    showToast(err.message);
  }
});

/* --- ĐỔI LỊCH KHÁM (RESCHEDULE) --- */
window.openRescheduleModal = (id, pName, currentDateStr) => {
  document.querySelector('#reschedule-appt-id').value = id;
  document.querySelector('#reschedule-appt-info').value = `Mã ca #${id} - Bệnh nhân: ${pName} (Lịch cũ: ${new Date(currentDateStr).toLocaleString('vi-VN')})`;
  
  const nextDate = new Date(Date.now() + 24 * 3600000);
  nextDate.setMinutes(nextDate.getMinutes() - nextDate.getTimezoneOffset());
  document.querySelector('#reschedule-new-date').value = nextDate.toISOString().slice(0, 16);
  document.querySelector('#reschedule-reason').value = '';

  rescheduleModal.classList.remove('hidden');
};

document.querySelector('#close-reschedule-modal')?.addEventListener('click', () => rescheduleModal.classList.add('hidden'));
document.querySelector('#cancel-reschedule-modal')?.addEventListener('click', () => rescheduleModal.classList.add('hidden'));

rescheduleForm?.addEventListener('submit', async e => {
  e.preventDefault();
  const id = document.querySelector('#reschedule-appt-id').value;
  const newDate = document.querySelector('#reschedule-new-date').value;
  const reason = document.querySelector('#reschedule-reason').value.trim();

  try {
    const res = await fetch(`/api/appointments/${id}/reschedule`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ newAppointmentDate: newDate, reason })
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.message || 'Không thể đổi lịch khám.');
    }

    showToast('✓ Đã đổi lịch khám và gửi thông báo tới bệnh nhân thành công!');
    rescheduleModal.classList.add('hidden');
    await loadAppointments();
  } catch (err) {
    showToast(err.message);
  }
});

/* --- THANH TOÁN VIỆN PHÍ & TẠO HÓA ĐƠN --- */
window.calculateInvoiceTotal = () => {
  const examFee = parseFloat(document.querySelector('#invoice-exam-fee').value) || 0;
  const medFee = parseFloat(document.querySelector('#invoice-med-fee').value) || 0;
  const discountPercent = parseFloat(document.querySelector('#invoice-discount').value) || 0;

  const subtotal = examFee + medFee;
  const discount = (subtotal * discountPercent) / 100;
  const total = Math.max(0, subtotal - discount - currentBookingBalance);

  document.querySelector('#invoice-total-display').textContent = total.toLocaleString('vi-VN') + ' đ';
  document.querySelector('#invoice-booking-balance').textContent = currentBookingBalance.toLocaleString('vi-VN') + ' đ';
  return total;
};

window.openInvoiceModal = (appointmentId, patientName) => {
  const appointment = appointments.find(item => item.id === appointmentId);
  currentBookingBalance = appointment?.bookingSource === 'Online' && appointment.bookingFeeIsPaid
    ? Math.max(0, appointment.bookingFee || 0)
    : 0;
  document.querySelector('#invoice-appointment-id').value = appointmentId;
  document.querySelector('#invoice-patient-info').value = `Ca khám #${appointmentId} - Bệnh nhân: ${patientName}`;
  document.querySelector('#invoice-exam-fee').value = 150000;
  document.querySelector('#invoice-med-fee').value = 200000;
  document.querySelector('#invoice-discount').value = 0;
  calculateInvoiceTotal();

  invoiceModal.classList.remove('hidden');
};

document.querySelector('#close-invoice-modal')?.addEventListener('click', () => invoiceModal.classList.add('hidden'));
document.querySelector('#cancel-invoice-modal')?.addEventListener('click', () => invoiceModal.classList.add('hidden'));

invoiceForm?.addEventListener('submit', async (e) => {
  e.preventDefault();
  const appointmentId = parseInt(document.querySelector('#invoice-appointment-id').value, 10);
  const amount = calculateInvoiceTotal();
  const paymentMethod = document.querySelector('#invoice-method').value;

  try {
    const res = await fetch('/api/invoices', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        appointmentId,
        amount,
        paymentMethod,
        notes: `Thanh toán tại quầy (${paymentMethod})`
      })
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.message || 'Không thể tạo hóa đơn.');
    }

    const inv = await res.json();
    // Tự động xác nhận thanh toán
    if (inv && inv.id) {
      const paymentResponse = await fetch(`/api/invoices/${inv.id}/confirm`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ paymentMethod })
      });
      if (!paymentResponse.ok) {
        const error = await paymentResponse.json().catch(() => ({}));
        throw new Error(error.message || 'Không thể xác nhận thanh toán.');
      }
    }

    showToast(`✓ Đã thu viện phí thành công! Tổng số tiền: ${amount.toLocaleString('vi-VN')} đ`);
    invoiceModal.classList.add('hidden');
  } catch (err) {
    showToast(err.message);
  }
});

/* --- QUẢN LÝ BỆNH NHÂN MODAL --- */
function openModal(patient) {
  form.reset();
  document.querySelector('#patient-id').value = patient?.id || '';
  document.querySelector('#modal-title').textContent = patient ? 'Chỉnh sửa hồ sơ' : 'Thêm bệnh nhân';
  if (patient) {
    document.querySelector('#full-name').value = patient.fullName;
    document.querySelector('#date-of-birth').value = patient.dateOfBirth;
    document.querySelector('#phone').value = patient.phone;
    document.querySelector('#insurance-number').value = patient.insuranceNumber || '';
    document.querySelector('#address').value = patient.address;
    document.querySelector('#medical-history').value = patient.medicalHistory;
  }
  modal.classList.remove('hidden');
  document.querySelector('#full-name').focus();
}

window.editPatient = id => openModal(patients.find(patient => patient.id === id));
window.deletePatient = async id => {
  if (!confirm('Bạn có chắc muốn xóa hồ sơ bệnh nhân này?')) return;
  const response = await fetch(`${apiUrl}/${id}`, { method: 'DELETE' });
  if (!response.ok) return showToast('Xóa hồ sơ thất bại.');
  await loadPatients();
  showToast('Đã xóa hồ sơ bệnh nhân.');
};

form.addEventListener('submit', async event => {
  event.preventDefault();
  const id = document.querySelector('#patient-id').value;
  const patient = {
    id: id ? Number(id) : Math.max(0, ...patients.map(item => item.id)) + 1,
    fullName: document.querySelector('#full-name').value.trim(),
    dateOfBirth: document.querySelector('#date-of-birth').value,
    phone: document.querySelector('#phone').value.trim(),
    insuranceNumber: document.querySelector('#insurance-number').value.trim(),
    address: document.querySelector('#address').value.trim(),
    medicalHistory: document.querySelector('#medical-history').value.trim() || 'Chưa cập nhật'
  };
  const response = await fetch(id ? `${apiUrl}/${id}` : apiUrl, {
    method: id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(patient)
  });
  if (!response.ok) return showToast('Không thể lưu hồ sơ.');
  modal.classList.add('hidden');
  await loadPatients();
  showToast(id ? 'Đã cập nhật hồ sơ bệnh nhân thành công.' : 'Đã thêm bệnh nhân mới vào hệ thống.');
});

document.querySelector('#open-add').addEventListener('click', () => openModal());
document.querySelector('#close-modal').addEventListener('click', () => modal.classList.add('hidden'));
document.querySelector('#cancel-modal').addEventListener('click', () => modal.classList.add('hidden'));
searchInput.addEventListener('input', renderPatients);
statusFilter.addEventListener('change', renderAppointments);

/* --- SIGNALR REALTIME AUTO REFRESH --- */
function initSignalRRealtime() {
  if (typeof signalR === 'undefined') return;
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/clinic')
    .withAutomaticReconnect()
    .build();

  connection.on('NEW_BOOKING', () => {
    showToast('🔔 Có lịch khám mới từ bệnh nhân!');
    loadAppointments();
  });
  connection.on('APPOINTMENT_CANCELLED', () => {
    showToast('⚠️ Một lịch khám vừa bị hủy!');
    loadAppointments();
  });
  connection.on('EXAM_COMPLETED', () => {
    showToast('🩺 Bác sĩ vừa hoàn tất ca khám! Sẵn sàng thu viện phí & xuất hóa đơn.');
    loadAppointments();
  });

  connection.start().catch(err => console.log('SignalR reconnecting...', err));
}

function formatDate(date) { return new Intl.DateTimeFormat('vi-VN').format(new Date(`${date}T00:00:00`)); }
function initials(name) { return name.split(' ').slice(-2).map(word => word[0]).join('').toUpperCase(); }
function escapeHtml(value) { return String(value || '').replace(/[&<>'"]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' })[c]); }
function escapeJs(value) { return String(value || '').replace(/'/g, "\\'"); }
function showToast(message) { toast.textContent = message; toast.classList.add('show'); setTimeout(() => toast.classList.remove('show'), 2800); }

init().catch(() => { patientTable.innerHTML = '<tr><td colspan="6" class="empty">Không kết nối được API. Hãy kiểm tra server.</td></tr>'; });
