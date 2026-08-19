// Content Studio boot: load metadata, build the shell (sidebar, status bar, shortcuts),
// route hash URLs to views, and keep everything live over SSE.

import { api } from './api.js';
import { h, replaceChildren, toast, makeResizable } from './ui.js';
import { state, on, loadStatus, loadMeta, loadValidation, loadAllRecords, typeOf, findRecord, applyValidation, refreshTypeRecords } from './state.js';
import { connectEvents } from './sse.js';
import { togglePalette } from './palette.js';
import { undo, redo } from './undo.js';
import { openTypeList } from './listview.js';
import { openRecordEditor, editorFor } from './editor.js';
import { openDashboard } from './views/dashboard.js';
import { openValidationCenter } from './views/validation.js';
import { openDependencyExplorer } from './views/deps.js';
import { openCompare } from './views/compare.js';
import { openBalance } from './views/balance.js';
import { openFilesView } from './views/files.js';
import { showProjectPicker } from './views/projectpicker.js';
import { renderInspectorEmpty } from './inspector.js';

const workspace = () => document.getElementById('workspace');

// ── Routing ───────────────────────────────────────────────────────────────────────────────

async function route() {
  if (!state.status?.loaded) { renderNoProject(); return; }
  const hash = location.hash || '#/dashboard';
  const [, view, ...rest] = hash.split('/');
  const argument = rest.map(decodeURIComponent).join('/');

  updateSidebarActive(hash);
  renderInspectorEmpty();

  switch (view) {
    case 'dashboard': await openDashboard(workspace()); break;
    case 'type': await openTypeList(workspace(), argument); break;
    case 'record': await openRecordEditor(workspace(), argument); break;
    case 'validation': await openValidationCenter(workspace()); break;
    case 'deps': await openDependencyExplorer(workspace(), argument || null); break;
    case 'compare': openCompare(workspace(), argument.split(',').filter(Boolean)); break;
    case 'balance': await openBalance(workspace(), rest[0] || 'enemies'); break;
    case 'files': await openFilesView(workspace()); break;
    default: await openDashboard(workspace());
  }
}

// ── Sidebar ───────────────────────────────────────────────────────────────────────────────

const NAV_GROUP_ORDER = ['Combat', 'Crafting', 'Professions', 'World', 'Character', 'System'];

function buildSidebar() {
  const sidebar = document.getElementById('sidebar');
  const collapsedGroups = new Set(JSON.parse(localStorage.getItem('cs.collapsedGroups') ?? '[]'));

  const problemCountsByType = new Map();
  for (const problem of state.validation.problems ?? []) {
    if (!problem.typeId) continue;
    const counts = problemCountsByType.get(problem.typeId) ?? { errors: 0, warnings: 0 };
    if (problem.severity === 'error') counts.errors++; else counts.warnings++;
    problemCountsByType.set(problem.typeId, counts);
  }
  const dirtyTypes = new Set();
  for (const file of state.status?.dirtyFiles ?? []) {
    const folder = file.split('/')[0];
    dirtyTypes.add(folder);
  }

  const navItem = (label, hash, { count, badge, dirty } = {}) => h('div', {
    class: 'nav-item', dataset: { hash },
    onclick: () => { location.hash = hash; },
  },
    h('span', { class: 'nav-label' }, label),
    dirty ? h('span', { class: 'nav-dirty', title: 'Unsaved changes' }) : null,
    badge ? h('span', { class: `nav-badge ${badge.kind}` }, badge.text) : null,
    count !== undefined ? h('span', { class: 'nav-count' }, count) : null);

  const groups = new Map();
  for (const type of state.meta?.types ?? []) {
    if (!groups.has(type.group)) groups.set(type.group, []);
    groups.get(type.group).push(type);
  }

  const groupElements = [];
  for (const groupName of NAV_GROUP_ORDER) {
    const types = groups.get(groupName);
    if (!types) continue;
    const items = types.map((type) => {
      const problems = problemCountsByType.get(type.typeId);
      return navItem(type.displayName, `#/type/${type.typeId}`, {
        count: type.recordCount,
        dirty: dirtyTypes.has(type.typeId),
        badge: problems?.errors ? { kind: 'err', text: problems.errors } :
               problems?.warnings ? { kind: 'warn', text: problems.warnings } : null,
      });
    });
    const group = h('div', { class: `nav-group${collapsedGroups.has(groupName) ? ' collapsed' : ''}` },
      h('div', { class: 'nav-group-title', onclick: (event) => {
        const groupElement = event.currentTarget.parentElement;
        groupElement.classList.toggle('collapsed');
        const nowCollapsed = groupElement.classList.contains('collapsed');
        if (nowCollapsed) collapsedGroups.add(groupName); else collapsedGroups.delete(groupName);
        localStorage.setItem('cs.collapsedGroups', JSON.stringify([...collapsedGroups]));
      } },
        h('span', { class: 'twist' }, '▼'), groupName),
      h('div', { class: 'nav-group-items' }, items));
    groupElements.push(group);
  }

  replaceChildren(sidebar,
    navItem('Dashboard', '#/dashboard'),
    groupElements,
    h('div', { class: 'nav-group' },
      h('div', { class: 'nav-group-title' }, h('span', { class: 'twist' }, '▼'), 'Tools'),
      h('div', { class: 'nav-group-items' },
        navItem('Balance Studio', '#/balance/enemies'),
        navItem('Dependencies', '#/deps'),
        navItem('Validation', '#/validation', state.validation.errors
          ? { badge: { kind: 'err', text: state.validation.errors } }
          : state.validation.warnings ? { badge: { kind: 'warn', text: state.validation.warnings } } : {}),
        navItem('Files & Backups', '#/files', (state.status?.dirtyFiles?.length ?? 0) > 0
          ? { badge: { kind: 'warn', text: state.status.dirtyFiles.length } } : {}))));

  updateSidebarActive(location.hash || '#/dashboard');
}

function updateSidebarActive(hash) {
  for (const item of document.querySelectorAll('.nav-item')) {
    const itemHash = item.dataset.hash ?? '';
    const active = hash === itemHash ||
      (itemHash.startsWith('#/type/') && hash.startsWith(itemHash)) ||
      (itemHash.startsWith('#/balance') && hash.startsWith('#/balance')) ||
      (itemHash === '#/deps' && hash.startsWith('#/deps')) ||
      (itemHash === '#/validation' && hash.startsWith('#/validation')) ||
      (itemHash === '#/files' && hash.startsWith('#/files'));
    item.classList.toggle('active', active);
  }
}

// ── Status bar & top bar ─────────────────────────────────────────────────────────────────

function refreshChrome() {
  const status = state.status;
  const validation = state.validation;

  const projectChip = document.getElementById('project-chip');
  projectChip.textContent = status?.projectRoot ? status.projectRoot : 'no project';
  projectChip.title = 'Click to change the game project';

  const validationChip = document.getElementById('validation-chip');
  if (validation.errors > 0) {
    validationChip.textContent = `✕ ${validation.errors} error${validation.errors === 1 ? '' : 's'}`;
    validationChip.className = 'validation-chip err';
  } else if (validation.warnings > 0) {
    validationChip.textContent = `⚠ ${validation.warnings}`;
    validationChip.className = 'validation-chip warn';
  } else {
    validationChip.textContent = '✓ VALID';
    validationChip.className = 'validation-chip ok';
  }

  const dirtyCount = status?.dirtyFiles?.length ?? 0;
  const statusDirty = document.getElementById('status-dirty');
  statusDirty.textContent = `${dirtyCount} modified`;
  statusDirty.className = `status-item clickable${dirtyCount ? ' accent' : ''}`;

  const statusErrors = document.getElementById('status-errors');
  statusErrors.textContent = `${validation.errors} errors`;
  statusErrors.className = `status-item clickable${validation.errors ? ' err' : ''}`;

  const statusWarnings = document.getElementById('status-warnings');
  statusWarnings.textContent = `${validation.warnings} warnings`;
  statusWarnings.className = `status-item clickable${validation.warnings ? ' warn' : ''}`;

  document.getElementById('save-all').disabled = dirtyCount === 0;
}

async function saveAll() {
  const dirtyCount = state.status?.dirtyFiles?.length ?? 0;
  if (dirtyCount === 0) { toast('Nothing to save', '', 'ok', 1200); return; }
  try {
    const result = await api.save(null, false);
    const saved = result.results.filter((entry) => entry.saved);
    const blocked = result.results.filter((entry) => entry.error);
    applyValidation(await api.validation());
    await loadStatus();
    refreshChrome();
    buildSidebar();
    if (blocked.length) toast('Some files not saved', blocked.map((entry) => `${entry.relativePath}: ${entry.error}`).join('\n'), 'warn', 6000);
    else toast('Saved', `${saved.length} file(s) written (backups taken)`, 'ok');
  } catch (error) {
    toast('Save failed', error.message, 'err');
  }
}

function renderNoProject() {
  replaceChildren(workspace(),
    h('div', { class: 'empty-state', style: { height: '80%' } },
      h('div', { class: 'big' }, '⚒'),
      h('div', { style: { fontSize: '15px', color: 'var(--fg)' } }, 'No game project selected'),
      h('div', {}, 'Point Content Studio at the folder containing game/data.'),
      h('button', { class: 'button primary', onclick: () => showProjectPicker({ currentRoot: '', onOpened: bootAfterProjectOpen }) }, 'Select Project…')));
}

// ── Keyboard shortcuts ────────────────────────────────────────────────────────────────────

function bindShortcuts() {
  document.addEventListener('keydown', (event) => {
    const inEditableControl = ['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement?.tagName);
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      togglePalette();
    } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
      event.preventDefault();
      saveAll();
    } else if ((event.ctrlKey || event.metaKey) && !event.shiftKey && event.key.toLowerCase() === 'z' && !inEditableControl) {
      event.preventDefault();
      undo();
    } else if ((event.ctrlKey || event.metaKey) && (event.key.toLowerCase() === 'y' || (event.shiftKey && event.key.toLowerCase() === 'z')) && !inEditableControl) {
      event.preventDefault();
      redo();
    }
  });
}

// ── Boot ──────────────────────────────────────────────────────────────────────────────────

async function bootAfterProjectOpen() {
  await loadStatus();
  await loadMeta();
  await loadValidation();
  refreshChrome();
  buildSidebar();
  await route();
  loadAllRecords().then(() => {
    buildSidebar();
    refreshChrome();
  });
}

async function boot() {
  makeResizable('sidebar-resizer', '--sidebar-width', { min: 160, max: 380, storageKey: 'cs.sidebarWidth' });
  makeResizable('inspector-resizer', '--inspector-width', { min: 220, max: 520, storageKey: 'cs.inspectorWidth', invert: true });
  bindShortcuts();

  document.getElementById('global-search').addEventListener('click', togglePalette);
  document.getElementById('save-all').addEventListener('click', saveAll);
  document.getElementById('project-chip').addEventListener('click', () =>
    showProjectPicker({ currentRoot: state.status?.projectRoot ?? '', onOpened: () => location.reload() }));
  document.getElementById('validation-chip').addEventListener('click', () => { location.hash = '#/validation'; });
  document.getElementById('status-errors').addEventListener('click', () => { location.hash = '#/validation'; });
  document.getElementById('status-warnings').addEventListener('click', () => { location.hash = '#/validation'; });
  document.getElementById('status-dirty').addEventListener('click', () => { location.hash = '#/files'; });

  window.addEventListener('hashchange', route);

  on('validation', () => { refreshChrome(); buildSidebar(); });
  on('workspace-changed', async () => {
    await loadStatus();
    refreshChrome();
    buildSidebar();
  });
  on('file-event', async (payload) => {
    if (payload.reason === 'reloaded') {
      const typeId = payload.path.split('/')[0];
      await refreshTypeRecords(typeId).catch(() => {});
      // If the reloaded file backs the open editor, adopt the fresh data.
      const hash = location.hash;
      if (hash.startsWith('#/record/')) {
        const openId = decodeURIComponent(hash.slice('#/record/'.length));
        const editor = editorFor(openId);
        const fresh = findRecord(openId);
        if (editor && fresh && fresh.file === payload.path) editor.adoptExternal(fresh);
      }
    }
    await loadStatus();
    refreshChrome();
    buildSidebar();
  });

  connectEvents();

  try {
    await loadStatus();
  } catch (error) {
    replaceChildren(workspace(), h('div', { class: 'empty-state' }, h('div', {}, `Cannot reach the Content Studio server: ${error.message}`)));
    return;
  }

  if (!state.status.loaded) {
    renderNoProject();
    refreshChrome();
    return;
  }
  await bootAfterProjectOpen();
}

boot();
