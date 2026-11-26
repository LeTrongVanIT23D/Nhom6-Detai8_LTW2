(function () {
  // Tạo DOM cho Chatbot Widget
  const chatbotHtml = `
    <button id="chatbot-toggle-btn" class="chatbot-toggle" title="Mở Trợ lý AI Medika" aria-label="Mở Trợ lý AI Medika">
      <span class="bot-avatar" aria-hidden="true">
        <span class="doctor-penguin">
          <span class="penguin-wing penguin-wing-left"></span>
          <span class="penguin-wing penguin-wing-right"></span>
          <span class="penguin-body">
            <span class="penguin-face">
              <span class="penguin-eye penguin-eye-left"></span>
              <span class="penguin-eye penguin-eye-right"></span>
              <span class="penguin-blush penguin-blush-left"></span>
              <span class="penguin-blush penguin-blush-right"></span>
              <span class="penguin-beak"></span>
            </span>
            <span class="penguin-coat">
              <span class="penguin-coat-lapel penguin-coat-lapel-left"></span>
              <span class="penguin-coat-lapel penguin-coat-lapel-right"></span>
              <span class="penguin-cross">+</span>
            </span>
          </span>
          <span class="penguin-foot penguin-foot-left"></span>
          <span class="penguin-foot penguin-foot-right"></span>
        </span>
      </span>
    </button>
    <div id="chatbot-tip" class="chatbot-tip" role="status" aria-live="polite" hidden></div>

    <div id="chatbot-window" class="chatbot-window hidden">
      <div class="chatbot-header">
        <div class="chatbot-header-info">
          <span style="font-size:1.4rem;">🤖</span>
          <div>
            <h4>Medika AI Smart Assistant</h4>
            <small id="chatbot-status-subtitle"><span class="chatbot-status-dot"></span> Trực tuyến 24/7 · Gemini AI Enabled</small>
          </div>
        </div>
        <div style="display:flex;align-items:center;gap:6px;">
          <button id="chatbot-clear-btn" class="chatbot-close" title="Làm mới cuộc trò chuyện" style="font-size:0.9rem;">↺</button>
          <button id="chatbot-close-btn" class="chatbot-close" title="Đóng chat">✕</button>
        </div>
      </div>

      <div id="chatbot-messages" class="chatbot-messages">
        <div class="chat-msg bot">
          Xin chào! Tôi là <strong>Trợ lý Y tế Thông minh của Medika Clinic</strong> 🏥.<br><br>
          Tôi có thể hỗ trợ bạn tìm chuyên khoa, tra cứu bác sĩ, thủ tục BHYT và hướng dẫn đặt lịch khám nhanh chóng. Bạn cần hỗ trợ gì hôm nay?
        </div>
      </div>

      <div id="chatbot-prompts" class="chatbot-prompts"></div>

      <div class="chatbot-input-bar">
        <input type="text" id="chatbot-input" placeholder="Nhập câu hỏi, triệu chứng cần tư vấn..." autocomplete="off">
        <button id="chatbot-send-btn" class="chatbot-send-btn" title="Gửi">➤</button>
      </div>
    </div>
  `;

  // Nhúng vào body khi trang tải xong
  document.addEventListener('DOMContentLoaded', initChatbot);
  if (document.readyState === 'interactive' || document.readyState === 'complete') {
    initChatbot();
  }

  async function initChatbot() {
    if (document.querySelector('#chatbot-toggle-btn')) return;

    try {
      const response = await fetch('/api/auth/me', { cache: 'no-store' });
      if (!response.ok) return;
      const user = await response.json();
      if (user.role !== 'User') return;
    } catch (error) {
      console.error('Không thể xác minh tài khoản để mở Trợ lý Medika:', error);
      return;
    }

    const wrapper = document.createElement('div');
    wrapper.id = 'chatbot-container';
    wrapper.innerHTML = chatbotHtml;
    document.body.appendChild(wrapper);

    const toggleBtn = document.querySelector('#chatbot-toggle-btn');
    const windowEl = document.querySelector('#chatbot-window');
    const tipEl = document.querySelector('#chatbot-tip');
    const closeBtn = document.querySelector('#chatbot-close-btn');
    const clearBtn = document.querySelector('#chatbot-clear-btn');
    const inputEl = document.querySelector('#chatbot-input');
    const sendBtn = document.querySelector('#chatbot-send-btn');
    const messagesEl = document.querySelector('#chatbot-messages');
    const promptsEl = document.querySelector('#chatbot-prompts');

    const positionStorageKey = 'medika-chatbot-position';
    let dragState = null;
    let dragged = false;
    let movementFrame = 0;
    let movementTime = 0;
    let velocityX = 42;
    let velocityY = 26;
    let avatarLeft = window.innerWidth - 80;
    let avatarTop = window.innerHeight - 80;
    let tipTimeout;
    let nextTipTimeout;
    let tipIndex = new Date().getDate() % 7;
    const healthTips = [
      'Dành vài phút vận động nhẹ sau mỗi giờ ngồi lâu.',
      'Rửa tay bằng xà phòng trước khi ăn và sau khi đi vệ sinh.',
      'Nghỉ mắt định kỳ nếu bạn thường xuyên nhìn màn hình.',
      'Ưu tiên bữa ăn đa dạng rau củ và uống nước theo nhu cầu.',
      'Giữ giờ ngủ đều đặn giúp duy trì nhịp sinh hoạt ổn định.',
      'Che chắn phù hợp khi ở ngoài nắng lâu.',
      'Không tự ý dùng thuốc; hỏi dược sĩ hoặc bác sĩ nếu chưa rõ.'
    ];

    function clampPosition(left, top) {
      return {
        left: Math.max(8, Math.min(left, window.innerWidth - toggleBtn.offsetWidth - 8)),
        top: Math.max(8, Math.min(top, window.innerHeight - toggleBtn.offsetHeight - 8))
      };
    }

    function setTogglePosition(left, top) {
      const position = clampPosition(left, top);
      avatarLeft = position.left;
      avatarTop = position.top;
      toggleBtn.style.left = `${position.left}px`;
      toggleBtn.style.top = `${position.top}px`;
      toggleBtn.style.right = 'auto';
      toggleBtn.style.bottom = 'auto';
      return position;
    }

    function scheduleHealthTip(delay = 30000) {
      clearTimeout(nextTipTimeout);
      nextTipTimeout = setTimeout(showHealthTip, delay);
    }

    function positionHealthTip() {
      if (tipEl.hidden) return;
      const rect = toggleBtn.getBoundingClientRect();
      const tipWidth = Math.min(290, window.innerWidth - 24);
      const left = Math.max(12, Math.min(rect.left + rect.width / 2 - tipWidth / 2, window.innerWidth - tipWidth - 12));
      const top = rect.top >= tipEl.offsetHeight + 20
        ? rect.top - tipEl.offsetHeight - 10
        : Math.min(rect.bottom + 10, window.innerHeight - tipEl.offsetHeight - 12);
      tipEl.style.left = `${left}px`;
      tipEl.style.top = `${Math.max(12, top)}px`;
    }

    function showHealthTip() {
      if (document.visibilityState !== 'visible' ||
          !windowEl.classList.contains('hidden') ||
          dragState) {
        scheduleHealthTip(5000);
        return;
      }

      tipEl.textContent = `💡 Mẹo sức khỏe: ${healthTips[tipIndex]}`;
      tipIndex = (tipIndex + 1) % healthTips.length;
      tipEl.hidden = false;
      positionHealthTip();

      clearTimeout(tipTimeout);
      tipTimeout = setTimeout(() => {
        tipEl.hidden = true;
        scheduleHealthTip();
      }, 10000);
    }

    try {
      const savedPosition = JSON.parse(localStorage.getItem(positionStorageKey) || 'null');
      if (Number.isFinite(savedPosition?.left) && Number.isFinite(savedPosition?.top)) {
        setTogglePosition(savedPosition.left, savedPosition.top);
      } else {
        setTogglePosition(avatarLeft, avatarTop);
      }
    } catch (error) {
      console.warn('Không thể tải vị trí Trợ lý AI Medika:', error);
    }

    function animateAvatar(timestamp) {
      if (!movementTime) movementTime = timestamp;
      const elapsed = Math.min(timestamp - movementTime, 50);
      movementTime = timestamp;

      const shouldMove = document.visibilityState === 'visible' &&
        windowEl.classList.contains('hidden') &&
        !dragState;

      if (shouldMove && elapsed > 0) {
        const rect = toggleBtn.getBoundingClientRect();
        let left = avatarLeft + velocityX * elapsed / 1000;
        let top = avatarTop + velocityY * elapsed / 1000;
        const maxLeft = Math.max(8, window.innerWidth - toggleBtn.offsetWidth - 8);
        const maxTop = Math.max(8, window.innerHeight - toggleBtn.offsetHeight - 8);

        if (left <= 8 || left >= maxLeft) {
          velocityX *= -1;
          left = Math.max(8, Math.min(left, maxLeft));
        }
        if (top <= 8 || top >= maxTop) {
          velocityY *= -1;
          top = Math.max(8, Math.min(top, maxTop));
        }
        setTogglePosition(left, top);
      }

      positionHealthTip();
      movementFrame = window.requestAnimationFrame(animateAvatar);
    }

    movementFrame = window.requestAnimationFrame(animateAvatar);
    scheduleHealthTip();

    toggleBtn.addEventListener('pointerdown', (event) => {
      if (event.button !== 0) return;
      tipEl.hidden = true;
      dragState = { pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, left: avatarLeft, top: avatarTop };
      dragged = false;
      toggleBtn.classList.add('is-dragging');
      toggleBtn.setPointerCapture(event.pointerId);
    });

    toggleBtn.addEventListener('pointermove', (event) => {
      if (!dragState || dragState.pointerId !== event.pointerId) return;
      const deltaX = event.clientX - dragState.startX;
      const deltaY = event.clientY - dragState.startY;
      if (!dragged && Math.hypot(deltaX, deltaY) < 5) return;
      dragged = true;
      const position = clampPosition(dragState.left + deltaX, dragState.top + deltaY);
      avatarLeft = position.left;
      avatarTop = position.top;
      toggleBtn.style.left = `${position.left}px`;
      toggleBtn.style.top = `${position.top}px`;
      toggleBtn.style.right = 'auto';
      toggleBtn.style.bottom = 'auto';
    });

    toggleBtn.addEventListener('pointerup', (event) => {
      if (!dragState || dragState.pointerId !== event.pointerId) return;
      if (dragged) {
        try {
          localStorage.setItem(positionStorageKey, JSON.stringify({ left: avatarLeft, top: avatarTop }));
        } catch (error) {
          console.warn('Không thể lưu vị trí Trợ lý AI Medika:', error);
        }
      }
      dragState = null;
      toggleBtn.classList.remove('is-dragging');
    });

    toggleBtn.addEventListener('pointercancel', () => {
      dragState = null;
      dragged = false;
      toggleBtn.classList.remove('is-dragging');
    });

    window.addEventListener('resize', () => {
      setTogglePosition(avatarLeft, avatarTop);
      if (!windowEl.classList.contains('hidden')) positionChatWindow();
    });

    function positionChatWindow() {
      const buttonRect = toggleBtn.getBoundingClientRect();
      const width = windowEl.offsetWidth;
      const height = windowEl.offsetHeight;
      const left = Math.max(16, Math.min(buttonRect.right - width, window.innerWidth - width - 16));
      const above = buttonRect.top - height - 12;
      const top = above >= 16 ? above : Math.min(buttonRect.bottom + 12, window.innerHeight - height - 16);
      windowEl.style.left = `${left}px`;
      windowEl.style.top = `${top}px`;
      windowEl.style.right = 'auto';
      windowEl.style.bottom = 'auto';
    }

    const sessionStorageKey = 'medika-chatbot-session-id';
    let sessionId = getOrCreateSessionId();

    function generateUUID() {
      if (typeof crypto !== 'undefined' && crypto.randomUUID) {
        return crypto.randomUUID();
      }
      return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        const r = Math.random() * 16 | 0;
        const v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
      });
    }

    function getOrCreateSessionId() {
      try {
        const storedSessionId = localStorage.getItem(sessionStorageKey);
        if (storedSessionId) return storedSessionId;

        const newSessionId = generateUUID();
        localStorage.setItem(sessionStorageKey, newSessionId);
        return newSessionId;
      } catch (error) {
        console.warn('Không thể truy cập localStorage cho phiên chatbot:', error);
        return generateUUID();
      }
    }

    toggleBtn.addEventListener('click', () => {
      if (dragged) {
        dragged = false;
        return;
      }
      windowEl.classList.toggle('hidden');
      if (!windowEl.classList.contains('hidden')) {
        tipEl.hidden = true;
        clearTimeout(tipTimeout);
        scheduleHealthTip(30000);
      }
      if (!windowEl.classList.contains('hidden')) {
        positionChatWindow();
        inputEl.focus();
      }
    });

    closeBtn.addEventListener('click', () => {
      windowEl.classList.add('hidden');
    });

    clearBtn?.addEventListener('click', () => {
      sessionId = generateUUID();
      try {
        localStorage.setItem(sessionStorageKey, sessionId);
      } catch (error) {
        console.warn('Không thể lưu phiên chatbot mới vào localStorage:', error);
      }
      messagesEl.innerHTML = `
        <div class="chat-msg bot">
          Xin chào! Tôi là <strong>Trợ lý Y tế Thông minh của Medika Clinic</strong> 🏥.<br><br>
          Tôi có thể hỗ trợ bạn tư vấn chuyên khoa, tra cứu bác sĩ, chính sách BHYT và đặt lịch khám. Bạn cần giúp gì ạ?
        </div>
      `;
      loadPrompts();
    });

    sendBtn.addEventListener('click', () => sendMessage());
    inputEl.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') sendMessage();
    });

    loadPrompts();

    async function loadPrompts() {
      try {
        const res = await fetch('/api/chatbot/prompts');
        if (!res.ok) return;
        const data = await res.json();
        promptsEl.innerHTML = (data || []).map(p => `
          <button class="chatbot-prompt-chip" data-action="${escapeHtml(p.actionType)}" data-value="${escapeHtml(p.value || '')}">${escapeHtml(p.label)}</button>
        `).join('');

        promptsEl.querySelectorAll('.chatbot-prompt-chip').forEach(btn => {
          btn.addEventListener('click', () => {
            const action = btn.getAttribute('data-action');
            const val = btn.getAttribute('data-value');
            handleQuickAction(action, val);
          });
        });
      } catch (e) { /* ignore */ }
    }

    async function sendMessage(text) {
      const msg = (typeof text === 'string' ? text : inputEl.value).trim();
      if (!msg) return;

      appendMsg(msg, 'user');
      inputEl.value = '';

      // Hiệu ứng đang phân tích
      const typingEl = appendMsg('Đang phân tích dữ liệu y khoa...', 'bot');

      try {
        const res = await fetch('/api/chatbot/message', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ message: msg, sessionId: sessionId })
        });

        const data = await res.json();
        typingEl.remove();

        if (!res.ok) {
          appendMsg(data?.message || 'Xin lỗi, tôi gặp sự cố khi xử lý.', 'bot');
          return;
        }

        // Định dạng Markdown
        let formatted = formatMarkdown(data.reply || '');

        let botReplyHtml = `<div>${formatted}</div>`;

        // Nguồn AI
        if (data.source) {
          const isGemini = data.source.toLowerCase().includes('gemini');
          const badgeStyle = isGemini
            ? 'background:#e0e7ff;color:#3730a3;border:1px solid #c7d2fe;'
            : 'background:#f1f5f9;color:#475569;border:1px solid #e2e8f0;';
          botReplyHtml += `
            <div style="margin-top:6px;display:flex;align-items:center;gap:4px;">
              <span style="font-size:0.72rem;padding:2px 8px;border-radius:10px;font-weight:600;${badgeStyle}">
                ${isGemini ? '⚡ ' : '🏥 '}${escapeHtml(data.source)}
              </span>
            </div>
          `;
        }

        // Nếu có danh sách bác sĩ
        if (data.doctors && data.doctors.length > 0) {
          botReplyHtml += `<div class="chatbot-doctors-list">` +
            data.doctors.map(d => `
              <div class="chatbot-doc-card">
                <div class="chatbot-doc-info">
                  <strong>${escapeHtml(d.fullName)}</strong>
                  <small>${escapeHtml(d.specialty)} · ${escapeHtml(d.degree || '')} · ${Number(d.experienceYears || 0)} năm kinh nghiệm · ${escapeHtml(d.room)}</small>
                </div>
                <button class="chatbot-doc-btn" onclick="window.selectChatbotDoctor(${d.id}, '${escapeHtml(d.specialty)}')">Chọn bác sĩ</button>
              </div>
            `).join('') +
            `</div>`;
        }

        // Hiển thị quick actions nếu có
        if (data.quickActions && data.quickActions.length > 0) {
          botReplyHtml += `<div style="display:flex;gap:6px;flex-wrap:wrap;margin-top:10px;">` +
            data.quickActions.map(qa => `
              <button class="chatbot-prompt-chip" onclick="window.handleChatbotAction('${qa.actionType}', '${escapeHtml(qa.value || '')}')">${escapeHtml(qa.label)}</button>
            `).join('') +
            `</div>`;
        }

        // Nếu hệ thống vừa tạo lịch hẹn thành công qua AI Function Calling, kích hoạt tải lại UI
        if (data.shouldRefreshUi || data.intent === 'appointment_created') {
          try {
            window.dispatchEvent(new CustomEvent('medika:appointment-created', { detail: data }));
            if (typeof window.loadAppointments === 'function') window.loadAppointments();
            if (typeof loadAppointments === 'function') loadAppointments();
          } catch (e) {
            console.warn('Lỗi khi cập nhật UI sau khi AI đặt lịch:', e);
          }

          if (!location.pathname.includes('/user.html')) {
            botReplyHtml += `
              <div style="margin-top:10px;">
                <a href="/user.html" style="display:inline-block;padding:6px 12px;background:#0284c7;color:#fff;text-decoration:none;border-radius:6px;font-size:0.8rem;font-weight:600;">
                  👉 Xem danh sách Lịch khám của bạn
                </a>
              </div>
            `;
          }
        }

        const msgDiv = document.createElement('div');
        msgDiv.className = 'chat-msg bot';
        msgDiv.innerHTML = botReplyHtml;
        messagesEl.appendChild(msgDiv);
        messagesEl.scrollTop = messagesEl.scrollHeight;

      } catch (err) {
        typingEl.remove();
        appendMsg('Không thể kết nối đến máy chủ trợ lý AI.', 'bot');
      }
    }

    function appendMsg(text, sender) {
      const el = document.createElement('div');
      el.className = `chat-msg ${sender}`;
      el.textContent = text;
      messagesEl.appendChild(el);
      messagesEl.scrollTop = messagesEl.scrollHeight;
      return el;
    }

    function handleQuickAction(action, val) {
      if (action === 'info' && val) {
        sendMessage(val);
      } else if (action === 'book') {
        sendMessage(`Tôi muốn đặt lịch khám${val ? ` chuyên khoa ${val}` : ''}`);
      } else if (action === 'support') {
        location.href = '/support.html';
      } else if (action === 'specialty') {
        sendMessage(`Danh sách các bác sĩ chuyên khoa ${val || 'tổng quát'}`);
      }
    }
    window.handleChatbotAction = handleQuickAction;

    window.selectChatbotDoctor = (doctorId, specialty) => {
      sendMessage(`Tôi muốn đặt lịch khám chuyên khoa ${specialty}; tôi chọn bác sĩ mã ${doctorId}`);
    };

    window.bookDoctorDirectly = (doctorId, specialty) => {
      if (location.pathname.includes('/user.html')) {
        const specSelect = document.querySelector('#specialty');
        const docSelect = document.querySelector('#doctor-id');
        const formEl = document.querySelector('#booking-form');

        if (specSelect && specialty) {
          for (let opt of specSelect.options) {
            if (opt.value.toLowerCase().includes(specialty.toLowerCase())) {
              specSelect.value = opt.value;
              specSelect.dispatchEvent(new Event('change'));
              break;
            }
          }
        }
        if (docSelect && doctorId) {
          setTimeout(() => {
            docSelect.value = doctorId;
          }, 300);
        }
        if (formEl) {
          formEl.scrollIntoView({ behavior: 'smooth' });
          windowEl.classList.add('hidden');
        }
      } else {
        location.href = `/user.html?specialty=${encodeURIComponent(specialty || '')}&doctorId=${doctorId || ''}`;
      }
    };

    function formatMarkdown(text) {
      let escaped = escapeHtml(text);
      // In đậm
      escaped = escaped.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
      // In nghiêng
      escaped = escaped.replace(/\*(.*?)\*/g, '<em>$1</em>');
      // Xuống dòng
      escaped = escaped.replace(/\n/g, '<br>');
      return escaped;
    }

    function escapeHtml(str) {
      return String(str || '').replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[c]);
    }
  }
})();
