// Small DOM toolkit: element builder, toasts, modals, context menus, tooltips.
// No framework — everything is plain DOM built through h().

export function h(tag, attributes = {}, ...children) {
  const element = document.createElement(tag);
  for (const [key, value] of Object.entries(attributes || {})) {
    if (value === null || value === undefined || value === false) continue;
    if (key === 'class') element.className = value;
    else if (key === 'dataset') Object.assign(element.dataset, value);
    else if (key === 'style' && typeof value === 'object') Object.assign(element.style, value);
    else if (key.startsWith('on') && typeof value === 'function') element.addEventListener(key.slice(2), value);
    else if (key === 'value' && 'value' in element) element.value = value;
    else if (key === 'checked') element.checked = !!value;
    else if (value === true) element.setAttribute(key, '');
    else element.setAttribute(key, value);
  }
  append(element, children);
  return element;
}

function append(element, children) {
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    element.append(child.nodeType ? child : document.createTextNode(String(child)));
  }
}

export function clear(element) {
  while (element.firstChild) element.removeChild(element.firstChild);
  return element;
}

export function replaceChildren(element, ...children) {
  clear(element);
  append(element, children);
  return element;
}

// ── Toasts ────────────────────────────────────────────────────────────────────────────────

let toastRoot = null;

export function toast(title, detail = '', kind = 'ok', durationMs = 3200) {
  if (!toastRoot) {
    toastRoot = h('div', { id: 'toast-root' });
    document.body.append(toastRoot);
  }
  const node = h('div', { class: `toast ${kind}` },
    h('div', { class: 'toast-title' }, title),
    detail ? h('div', { class: 'toast-detail' }, detail) : null);
  toastRoot.append(node);
  setTimeout(() => node.remove(), durationMs);
  return node;
}

// ── Modals ────────────────────────────────────────────────────────────────────────────────

export function openModal({ title, body, actions, wide = false, onClose }) {
  const backdrop = h('div', { class: 'modal-backdrop' });
  const modal = h('div', { class: `modal${wide ? ' wide' : ''}` });
  if (title) modal.append(h('div', { class: 'modal-title' }, title));
  const bodyElement = h('div', { class: 'modal-body' });
  append(bodyElement, [body]);
  modal.append(bodyElement);

  const close = () => { backdrop.remove(); document.removeEventListener('keydown', onKey); onClose?.(); };
  if (actions) {
    const actionsRow = h('div', { class: 'modal-actions' });
    for (const action of actions) {
      actionsRow.append(h('button', {
        class: `button ${action.kind || ''} ${action.left ? 'left' : ''}`,
        onclick: async () => { if (await action.onClick?.(close) !== false && action.closes !== false) close(); },
      }, action.label));
    }
    modal.append(actionsRow);
  }

  const onKey = (event) => { if (event.key === 'Escape') close(); };
  document.addEventListener('keydown', onKey);
  backdrop.addEventListener('mousedown', (event) => { if (event.target === backdrop) close(); });
  backdrop.append(modal);
  document.getElementById('overlay-root').append(backdrop);
  return { close, modal };
}

export function confirmModal(title, message, confirmLabel = 'Confirm', kind = 'primary') {
  return new Promise((resolve) => {
    openModal({
      title,
      body: typeof message === 'string' ? h('div', {}, message) : message,
      actions: [
        { label: 'Cancel', onClick: () => resolve(false) },
        { label: confirmLabel, kind, onClick: () => resolve(true) },
      ],
      onClose: () => resolve(false),
    });
  });
}

export function promptModal(title, { label = '', initial = '', placeholder = '', validate } = {}) {
  return new Promise((resolve) => {
    const input = h('input', { type: 'text', value: initial, placeholder, style: { width: '100%' } });
    const errorLine = h('div', { style: { color: 'var(--err)', fontSize: '11.5px', marginTop: '6px', minHeight: '15px' } });
    const { close } = openModal({
      title,
      body: h('div', {}, label ? h('div', { style: { marginBottom: '6px', color: 'var(--fg-dim)' } }, label) : null, input, errorLine),
      actions: [
        { label: 'Cancel', onClick: () => resolve(null) },
        {
          label: 'OK', kind: 'primary', closes: false,
          onClick: () => {
            const problem = validate?.(input.value);
            if (problem) { errorLine.textContent = problem; return false; }
            resolve(input.value);
            close();
          },
        },
      ],
      onClose: () => resolve(null),
    });
    input.addEventListener('keydown', (event) => {
      if (event.key === 'Enter') {
        const problem = validate?.(input.value);
        if (problem) { errorLine.textContent = problem; return; }
        resolve(input.value);
        close();
      }
    });
    setTimeout(() => { input.focus(); input.select(); }, 30);
  });
}

// ── Context menu ──────────────────────────────────────────────────────────────────────────

let openMenu = null;

export function contextMenu(x, y, items) {
  closeContextMenu();
  const menu = h('div', { class: 'context-menu' });
  for (const item of items) {
    if (item === '-') { menu.append(h('div', { class: 'menu-sep' })); continue; }
    if (!item) continue;
    menu.append(h('div', {
      class: `menu-item ${item.danger ? 'danger' : ''}`,
      onclick: () => { closeContextMenu(); item.onClick?.(); },
    }, item.icon ? h('span', {}, item.icon) : null, item.label, item.shortcut ? h('kbd', {}, item.shortcut) : null));
  }
  document.body.append(menu);
  const rect = menu.getBoundingClientRect();
  menu.style.left = `${Math.min(x, window.innerWidth - rect.width - 8)}px`;
  menu.style.top = `${Math.min(y, window.innerHeight - rect.height - 8)}px`;
  openMenu = menu;
  setTimeout(() => document.addEventListener('mousedown', onGlobalDown, { once: true }), 0);
}

function onGlobalDown(event) {
  if (openMenu && !openMenu.contains(event.target)) closeContextMenu();
  else if (openMenu) setTimeout(() => document.addEventListener('mousedown', onGlobalDown, { once: true }), 0);
}

export function closeContextMenu() {
  openMenu?.remove();
  openMenu = null;
}

// ── Pane resizing with persistence ───────────────────────────────────────────────────────

export function makeResizable(resizerId, cssVariable, { min, max, storageKey, invert = false }) {
  const resizer = document.getElementById(resizerId);
  const stored = localStorage.getItem(storageKey);
  if (stored) document.documentElement.style.setProperty(cssVariable, `${stored}px`);

  resizer.addEventListener('mousedown', (event) => {
    event.preventDefault();
    resizer.classList.add('dragging');
    const startX = event.clientX;
    const startWidth = parseInt(getComputedStyle(document.documentElement).getPropertyValue(cssVariable)) || 250;
    const onMove = (moveEvent) => {
      const delta = (moveEvent.clientX - startX) * (invert ? -1 : 1);
      const width = Math.max(min, Math.min(max, startWidth + delta));
      document.documentElement.style.setProperty(cssVariable, `${width}px`);
    };
    const onUp = () => {
      resizer.classList.remove('dragging');
      const width = parseInt(getComputedStyle(document.documentElement).getPropertyValue(cssVariable));
      localStorage.setItem(storageKey, String(width));
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onUp);
    };
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
  });
}

// ── Formatting helpers ────────────────────────────────────────────────────────────────────

export function formatNumber(value, digits = 2) {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  if (!Number.isFinite(value)) return '∞';
  if (Number.isInteger(value)) return String(value);
  return value.toFixed(digits).replace(/\.?0+$/, '');
}

export function escapeHtml(text) {
  return String(text).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

export function debounce(fn, delayMs) {
  let timer = null;
  const wrapped = (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delayMs);
  };
  wrapped.flush = (...args) => { clearTimeout(timer); fn(...args); };
  wrapped.cancel = () => clearTimeout(timer);
  return wrapped;
}
