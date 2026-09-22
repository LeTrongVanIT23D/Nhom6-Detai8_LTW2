const apiUrl = '/api/patients';
const table = document.querySelector('#patient-table');
const searchInput = document.querySelector('#search-input');
const modal = document.querySelector('#patient-modal');
const form = document.querySelector('#patient-form');
const toast = document.querySelector('#toast');
let patients = [];

async function loadPatients() {
  const response = await fetch(apiUrl);
  if (!response.ok) throw new Error('Không thể tải danh sách bệnh nhân.');
  patients = await response.json();
  renderPatients();
  document.querySelector('#total-count').textContent = String(patients.length).padStart(2, '0');
  document.querySelector('#today-count').textContent = String(Math.min(patients.length, 3)).padStart(2, '0');
}

function renderPatients() {
  const keyword = searchInput.value.trim().toLowerCase();
  const visiblePatients = patients.filter(patient =>
    `${patient.fullName} ${patient.phone}`.toLowerCase().includes(keyword));
  table.innerHTML = visiblePatients.length ? visiblePatients.map(patient => `
    <tr>
      <td><div class="patient-name"><span class="patient-avatar">${initials(patient.fullName)}</span>${escapeHtml(patient.fullName)}</div></td>
      <td>${formatDate(patient.dateOfBirth)}</td>
      <td>${escapeHtml(patient.phone)}</td>
      <td>${escapeHtml(patient.address)}</td>
      <td><span class="history" title="${escapeHtml(patient.medicalHistory)}">${escapeHtml(patient.medicalHistory || 'Chưa cập nhật')}</span></td>
      <td><div class="row-actions"><button class="action-button" title="Chỉnh sửa" onclick="editPatient(${patient.id})">✎</button><button class="action-button" title="Xóa" onclick="deletePatient(${patient.id})">⌫</button></div></td>
    </tr>`).join('') : '<tr><td colspan="6" class="empty">Không tìm thấy bệnh nhân.</td></tr>';
  document.querySelector('#result-count').textContent = `Hiển thị ${visiblePatients.length} / ${patients.length} hồ sơ`;
}

function openModal(patient) {
  form.reset();
  document.querySelector('#patient-id').value = patient?.id || '';
  document.querySelector('#modal-title').textContent = patient ? 'Chỉnh sửa hồ sơ' : 'Thêm bệnh nhân';
  if (patient) {
    document.querySelector('#full-name').value = patient.fullName;
    document.querySelector('#date-of-birth').value = patient.dateOfBirth;
    document.querySelector('#phone').value = patient.phone;
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
    fullName: document.querySelector('#full-name').value,
    dateOfBirth: document.querySelector('#date-of-birth').value,
    phone: document.querySelector('#phone').value,
    address: document.querySelector('#address').value,
    medicalHistory: document.querySelector('#medical-history').value || 'Không'
  };
  const response = await fetch(id ? `${apiUrl}/${id}` : apiUrl, {
    method: id ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(patient)
  });
  if (!response.ok) return showToast('Không thể lưu hồ sơ.');
  modal.classList.add('hidden');
  await loadPatients();
  showToast(id ? 'Đã cập nhật hồ sơ.' : 'Đã thêm bệnh nhân mới.');
});

document.querySelector('#open-add').addEventListener('click', () => openModal());
document.querySelector('#close-modal').addEventListener('click', () => modal.classList.add('hidden'));
document.querySelector('#cancel-modal').addEventListener('click', () => modal.classList.add('hidden'));
searchInput.addEventListener('input', renderPatients);

function formatDate(date) { return new Intl.DateTimeFormat('vi-VN').format(new Date(`${date}T00:00:00`)); }
function initials(name) { return name.split(' ').slice(-2).map(word => word[0]).join('').toUpperCase(); }
function escapeHtml(value) { return String(value).replace(/[&<>'"]/g, character => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' })[character]); }
function showToast(message) { toast.textContent = message; toast.classList.add('show'); setTimeout(() => toast.classList.remove('show'), 2600); }

loadPatients().catch(() => { table.innerHTML = '<tr><td colspan="6" class="empty">Không kết nối được API. Hãy kiểm tra server.</td></tr>'; });
