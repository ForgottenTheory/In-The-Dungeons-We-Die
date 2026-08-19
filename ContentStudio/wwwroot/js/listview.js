// The content browser for one type: compact searchable/sortable table with validation
// status, multi-select, bulk actions, context menus and record creation.

import { api } from './api.js';
import { h, replaceChildren, contextMenu, toast, openModal, promptModal, formatNumber } from './ui.js';
import { state, typeOf, recordsOf, loadRecords, problemsOf, applyValidation, refreshTypeRecords, vocabList } from './state.js';
import { duplicateRecordInteractive, deleteRecordInteractive } from './editor.js';
import { renderInspectorForRecord, renderInspectorForSelection, renderInspectorEmpty } from './inspector.js';

const viewState = new Map(); // typeId → { search, sortKey, sortDir, fileFilter, tagFilter, selected:Set }

export async function openTypeList(workspace, typeId) {
  const type = typeOf(typeId);
  if (!type) {
    replaceChildren(workspace, h('div', { class: 'empty-state' }, h('div', {}, `Unknown type ${typeId}`)));
    return;
  }
  if (!viewState.has(typeId))
    viewState.set(typeId, { search: '', sortKey: 'id', sortDir: 1, fileFilter: '', tagFilter: '', selected: new Set() });
  const vs = viewState.get(typeId);

  replaceChildren(workspace, h('div', { class: 'spinner' }));
  await loadRecords(typeId);

  const container = h('div', { style: { display: 'flex', flexDirection: 'column', height: '100%' } });
  replaceChildren(workspace, container);
  renderList(container, type, vs);
}

function columnValue(record, column) {
  const value = record.data?.[column];
  if (value === null || value === undefined) return '';
  if (Array.isArray(value)) return value.length;
  if (typeof value === 'object') return '{…}';
  return value;
}

function visibleRecords(type, vs) {
  let records = [...recordsOf(type.typeId)];
  if (vs.fileFilter) records = records.filter((record) => record.file === vs.fileFilter);
  if (vs.tagFilter) records = records.filter((record) => (record.data?.tags ?? []).includes(vs.tagFilter));
  if (vs.search) {
    const query = vs.search.toLowerCase();
    records = records.filter((record) =>
      record.id.toLowerCase().includes(query) ||
      (record.name ?? '').toLowerCase().includes(query) ||
      (record.data?.tags ?? []).some((tag) => String(tag).toLowerCase().includes(query)));
  }
  const key = vs.sortKey;
  records.sort((left, right) => {
    let a; let b;
    if (key === 'id') { a = left.id; b = right.id; }
    else if (key === 'name') { a = left.name ?? ''; b = right.name ?? ''; }
    else if (key === 'status') { a = left.errors * 100 + left.warnings; b = right.errors * 100 + right.warnings; }
    else if (key === 'file') { a = left.file; b = right.file; }
    else { a = columnValue(left, key); b = columnValue(right, key); }
    if (typeof a === 'number' && typeof b === 'number') return (a - b) * vs.sortDir;
    return String(a).localeCompare(String(b)) * vs.sortDir;
  });
  return records;
}

function renderList(container, type, vs) {
  const records = visibleRecords(type, vs);
  const allRecords = recordsOf(type.typeId);

  const searchInput = h('input', {
    type: 'search', placeholder: `Search ${type.displayName.toLowerCase()}…`, value: vs.search,
    oninput: (event) => { vs.search = event.target.value; rerender(); },
  });

  const fileOptions = [...new Set(allRecords.map((record) => record.file))].sort();
  const fileSelect = fileOptions.length > 1
    ? h('select', { onchange: (event) => { vs.fileFilter = event.target.value; rerender(); } },
        h('option', { value: '' }, 'All files'),
        fileOptions.map((file) => h('option', { value: file, selected: file === vs.fileFilter }, file.split('/').pop())))
    : null;

  const allTags = [...new Set(allRecords.flatMap((record) => record.data?.tags ?? []))].sort();
  const tagSelect = allTags.length > 0
    ? h('select', { onchange: (event) => { vs.tagFilter = event.target.value; rerender(); } },
        h('option', { value: '' }, 'All tags'),
        allTags.map((tag) => h('option', { value: tag, selected: tag === vs.tagFilter }, tag)))
    : null;

  const header = h('div', { class: 'view-header' },
    h('span', { class: 'view-title' }, type.displayName),
    h('span', { class: 'result-count' }, `${records.length} / ${allRecords.length}`),
    h('div', { class: 'toolbar-spacer', style: { flex: 1 } }),
    h('button', { class: 'button compact', onclick: () => { location.hash = `#/balance/${balanceSectionFor(type.typeId)}`; }, style: { display: hasBalanceView(type.typeId) ? '' : 'none' } }, '📊 Balance'),
    h('button', { class: 'button primary compact', onclick: () => createRecordInteractive(type) }, `+ New ${type.singularName}`),
    h('div', { class: 'view-subtitle' }, type.description));

  const toolbar = h('div', { class: 'toolbar' }, searchInput, fileSelect, tagSelect);

  const bulkBar = vs.selected.size > 0 ? h('div', { class: 'bulk-bar' },
    `${vs.selected.size} selected`,
    h('button', { class: 'button compact', onclick: () => { location.hash = `#/compare/${[...vs.selected].map(encodeURIComponent).join(',')}`; } }, 'Compare'),
    h('button', { class: 'button compact', onclick: () => bulkEditInteractive(type, vs, rerender) }, 'Bulk Edit'),
    h('button', { class: 'button compact danger', onclick: () => bulkDeleteInteractive(type, vs, rerender) }, 'Delete'),
    h('button', { class: 'button ghost compact', onclick: () => { vs.selected.clear(); rerender(); } }, 'Clear')) : null;

  const extraColumns = type.listColumns ?? [];
  const sortHeader = (label, key) => h('th', { onclick: () => {
    if (vs.sortKey === key) vs.sortDir *= -1; else { vs.sortKey = key; vs.sortDir = 1; }
    rerender();
  } }, label, vs.sortKey === key ? h('span', { class: 'sort-arrow' }, vs.sortDir > 0 ? '▲' : '▼') : null);

  const selectAll = h('input', {
    type: 'checkbox',
    checked: records.length > 0 && records.every((record) => vs.selected.has(record.id)),
    onchange: (event) => {
      if (event.target.checked) records.forEach((record) => vs.selected.add(record.id));
      else records.forEach((record) => vs.selected.delete(record.id));
      rerender();
    },
  });

  let lastClickedIndex = -1;
  const rows = records.map((record, index) => {
    const status = record.errors > 0
      ? h('span', { class: 'status-glyph err', title: `${record.errors} error(s)` }, '✕')
      : record.warnings > 0
        ? h('span', { class: 'status-glyph warn', title: `${record.warnings} warning(s)` }, '⚠')
        : h('span', { class: 'status-glyph ok' }, '✓');
    return h('tr', {
      class: vs.selected.has(record.id) ? 'selected' : '',
      onclick: (event) => {
        if (event.target.type === 'checkbox') return;
        if (event.shiftKey && lastClickedIndex >= 0) {
          const [from, to] = [Math.min(lastClickedIndex, index), Math.max(lastClickedIndex, index)];
          for (let i = from; i <= to; i++) vs.selected.add(records[i].id);
          rerender();
          return;
        }
        if (event.ctrlKey || event.metaKey) {
          if (vs.selected.has(record.id)) vs.selected.delete(record.id); else vs.selected.add(record.id);
          lastClickedIndex = index;
          rerender();
          return;
        }
        lastClickedIndex = index;
        location.hash = `#/record/${encodeURIComponent(record.id)}`;
      },
      onmouseenter: () => { if (vs.selected.size === 0) renderInspectorForRecord(record.id); },
      oncontextmenu: (event) => {
        event.preventDefault();
        rowContextMenu(event, type, record, vs, rerender);
      },
    },
      h('td', { style: { width: '26px' } }, h('input', {
        type: 'checkbox', checked: vs.selected.has(record.id),
        onchange: (event) => {
          if (event.target.checked) vs.selected.add(record.id); else vs.selected.delete(record.id);
          lastClickedIndex = index;
          rerender();
        },
      })),
      h('td', { style: { width: '30px' } }, status, record.dirty || record.fileDirty ? h('span', { class: 'dirty-dot', title: 'Unsaved changes', style: { marginLeft: '4px' } }) : null),
      h('td', {}, record.name ?? h('span', { class: 'low' }, '—')),
      h('td', { class: 'id-cell' }, record.id),
      extraColumns.map((column) => {
        const value = columnValue(record, column);
        return h('td', { class: typeof value === 'number' ? 'num' : '' }, typeof value === 'number' ? formatNumber(value) : String(value));
      }),
      h('td', { class: 'low', style: { fontSize: '11px' } }, record.file.split('/').pop()));
  });

  const table = h('div', { class: 'view-body' },
    records.length === 0
      ? h('div', { class: 'empty-state' }, h('div', { class: 'big' }, '⌀'), h('div', {}, 'Nothing matches.'))
      : h('table', { class: 'grid' },
          h('thead', {}, h('tr', {},
            h('th', { style: { width: '26px' } }, selectAll),
            sortHeader('', 'status'),
            sortHeader('Name', 'name'),
            sortHeader('Id', 'id'),
            extraColumns.map((column) => sortHeader(prettifyColumn(column), column)),
            sortHeader('File', 'file'))),
          h('tbody', {}, rows)));

  const rerender = () => {
    renderList(container, type, vs);
    if (vs.selected.size > 0) renderInspectorForSelection(type.typeId, [...vs.selected]);
  };

  replaceChildren(container, header, toolbar, bulkBar, table);
  if (vs.selected.size > 0) renderInspectorForSelection(type.typeId, [...vs.selected]);
  else renderInspectorEmpty('Hover a row to preview; click to edit.');
}

function prettifyColumn(column) {
  return column.replace(/_/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').replace(/\b\w/g, (c) => c.toUpperCase());
}

function hasBalanceView(typeId) {
  return ['actors', 'moves', 'materials', 'profession_actions', 'professions', 'loot_tables'].includes(typeId);
}

function balanceSectionFor(typeId) {
  return { actors: 'enemies', moves: 'moves', materials: 'materials', profession_actions: 'professions', professions: 'professions', loot_tables: 'loot' }[typeId] ?? 'enemies';
}

function rowContextMenu(event, type, record, vs, rerender) {
  contextMenu(event.clientX, event.clientY, [
    { label: 'Open', icon: '↗', onClick: () => { location.hash = `#/record/${encodeURIComponent(record.id)}`; } },
    { label: 'View Dependencies', icon: '🕸', onClick: () => { location.hash = `#/deps/${encodeURIComponent(record.id)}`; } },
    '-',
    { label: 'Copy ID', icon: '⧉', onClick: () => navigator.clipboard.writeText(record.id).then(() => toast('Copied', record.id, 'ok', 1100)) },
    { label: 'Copy JSON', icon: '{}', onClick: () => navigator.clipboard.writeText(JSON.stringify(record.data, null, 2)).then(() => toast('Copied JSON', '', 'ok', 1100)) },
    { label: 'Open Source File', icon: '📄', onClick: () => api.open(record.file, false).catch((e) => toast('Failed', e.message, 'err')) },
    { label: 'Reveal in Folder', icon: '📁', onClick: () => api.open(record.file, true).catch((e) => toast('Failed', e.message, 'err')) },
    '-',
    { label: 'Duplicate', icon: '⧉', onClick: () => duplicateRecordInteractive(record.id) },
    { label: 'Delete', icon: '🗑', danger: true, onClick: () => deleteRecordInteractive(record.id) },
  ]);
}

// ── Create ────────────────────────────────────────────────────────────────────────────────

export async function createRecordInteractive(type, template = null) {
  const idInput = h('input', { type: 'text', value: template?.id ? `${template.id}_new` : type.idPrefix, style: { width: '100%', fontFamily: 'var(--mono)' } });
  const nameInput = h('input', { type: 'text', value: '', placeholder: 'Display name', style: { width: '100%' } });
  const files = type.files ?? [];
  const arrayFiles = files.filter((file) => file.isArrayFile);
  const fileSelect = h('select', { style: { width: '100%' } },
    arrayFiles.map((file) => h('option', { value: file.path }, `${file.path} (${file.recordCount})`)),
    h('option', { value: '' }, '→ new file named after the id'));
  if (arrayFiles.length === 0) fileSelect.value = '';
  const errorLine = h('div', { style: { color: 'var(--err)', fontSize: '11.5px', minHeight: '15px', marginTop: '4px' } });

  openModal({
    title: `New ${type.singularName}`,
    body: h('div', {},
      h('div', { class: 'field-row' }, h('label', {}, 'Id'), h('div', { class: 'field-control' }, idInput)),
      h('div', { class: 'field-row' }, h('label', {}, 'Name'), h('div', { class: 'field-control' }, nameInput)),
      h('div', { class: 'field-row' }, h('label', {}, 'File'), h('div', { class: 'field-control' }, fileSelect)),
      errorLine),
    actions: [
      { label: 'Cancel' },
      {
        label: 'Create', kind: 'primary', closes: false,
        onClick: async (close) => {
          const id = idInput.value.trim();
          if (!id || id === type.idPrefix) { errorLine.textContent = 'A full id is required.'; return false; }
          const data = template ? structuredClone(template) : {};
          data.id = id;
          if (nameInput.value.trim()) data.name = nameInput.value.trim();
          else if (!data.name) data.name = defaultNameFromId(id, type.idPrefix);
          try {
            const payload = await api.createRecord(type.typeId, data, fileSelect.value || null);
            await refreshTypeRecords(type.typeId);
            const validation = await api.validation();
            applyValidation(validation);
            toast('Created', payload.record.id, 'ok');
            close();
            location.hash = `#/record/${encodeURIComponent(payload.record.id)}`;
          } catch (error) {
            errorLine.textContent = error.message;
            return false;
          }
        },
      },
    ],
  });
  setTimeout(() => { idInput.focus(); idInput.setSelectionRange(idInput.value.length, idInput.value.length); }, 40);
}

function defaultNameFromId(id, prefix) {
  const slug = prefix && id.startsWith(prefix) ? id.slice(prefix.length) : id.split('.').pop();
  return slug.split('_').map((word) => word.charAt(0).toUpperCase() + word.slice(1)).join(' ');
}

// ── Bulk operations ───────────────────────────────────────────────────────────────────────

async function bulkEditInteractive(type, vs, rerender) {
  const ids = [...vs.selected];
  const addTagInput = h('input', { type: 'text', placeholder: 'e.g. origin:mineral', style: { width: '100%' } });
  const removeTagInput = h('input', { type: 'text', placeholder: 'tag to remove', style: { width: '100%' } });
  const fieldPathInput = h('input', { type: 'text', placeholder: 'e.g. requiredLevel or armor', style: { width: '100%', fontFamily: 'var(--mono)' } });
  const fieldValueInput = h('input', { type: 'text', placeholder: 'JSON value: 12, "text", true, null clears', style: { width: '100%', fontFamily: 'var(--mono)' } });
  const errorLine = h('div', { style: { color: 'var(--err)', fontSize: '11.5px', minHeight: '15px' } });

  openModal({
    title: `Bulk edit ${ids.length} ${type.displayName.toLowerCase()}`,
    body: h('div', {},
      h('div', { class: 'section-title', style: { marginTop: 0 } }, 'Tags'),
      h('div', { class: 'field-row' }, h('label', {}, 'Add tag'), h('div', { class: 'field-control' }, addTagInput)),
      h('div', { class: 'field-row' }, h('label', {}, 'Remove tag'), h('div', { class: 'field-control' }, removeTagInput)),
      h('div', { class: 'section-title' }, 'Set field (dot path)'),
      h('div', { class: 'field-row' }, h('label', {}, 'Field'), h('div', { class: 'field-control' }, fieldPathInput)),
      h('div', { class: 'field-row' }, h('label', {}, 'Value'), h('div', { class: 'field-control' }, fieldValueInput)),
      errorLine),
    actions: [
      { label: 'Cancel' },
      {
        label: `Apply to ${ids.length}`, kind: 'primary', closes: false,
        onClick: async (close) => {
          const payload = { ids };
          if (addTagInput.value.trim()) payload.addTag = addTagInput.value.trim();
          if (removeTagInput.value.trim()) payload.removeTag = removeTagInput.value.trim();
          if (fieldPathInput.value.trim()) {
            let value = null;
            const rawValue = fieldValueInput.value.trim();
            if (rawValue !== '' && rawValue !== 'null') {
              try { value = JSON.parse(rawValue); }
              catch { errorLine.textContent = 'Value must be valid JSON (quote strings).'; return false; }
            }
            payload.set = { path: fieldPathInput.value.trim(), value };
          }
          if (!payload.addTag && !payload.removeTag && !payload.set) {
            errorLine.textContent = 'Nothing to apply.';
            return false;
          }
          try {
            const result = await api.bulkEdit(payload);
            await refreshTypeRecords(type.typeId);
            applyValidation(await api.validation());
            toast('Bulk edit applied', `${result.applied.length} changed${result.failed.length ? `, ${result.failed.length} failed` : ''}`,
              result.failed.length ? 'warn' : 'ok');
            close();
            rerender();
          } catch (error) {
            errorLine.textContent = error.message;
            return false;
          }
        },
      },
    ],
  });
}

async function bulkDeleteInteractive(type, vs, rerender) {
  const ids = [...vs.selected];
  const { confirmModal } = await import('./ui.js');
  const proceed = await confirmModal(`Delete ${ids.length} records?`,
    h('div', {}, h('p', {}, 'References are checked one by one; referenced records are force-deleted.'),
      h('p', { style: { color: 'var(--warn)' } }, 'Changes stay unsaved until you Save.')),
    'Delete All', 'danger');
  if (!proceed) return;
  let deleted = 0;
  for (const id of ids) {
    try { await api.deleteRecord(id, true); deleted++; } catch { /* keep going */ }
  }
  vs.selected.clear();
  await refreshTypeRecords(type.typeId);
  applyValidation(await api.validation());
  toast('Bulk delete', `${deleted}/${ids.length} deleted`, deleted === ids.length ? 'ok' : 'warn');
  rerender();
}
