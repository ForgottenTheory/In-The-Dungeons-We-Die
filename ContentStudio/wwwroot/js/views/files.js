// Files & Backups: per-file dirty/conflict state, diff viewing, revert, backup restore.

import { api } from '../api.js';
import { h, replaceChildren, toast, openModal, confirmModal, formatNumber } from '../ui.js';
import { applyValidation, state, loadRecords } from '../state.js';

export async function openFilesView(workspace) {
  replaceChildren(workspace, h('div', { class: 'spinner' }));
  const files = await api.files();

  const dirtyCount = files.filter((file) => file.dirty).length;
  const conflictCount = files.filter((file) => file.conflict).length;

  replaceChildren(workspace,
    h('div', { class: 'view-header' },
      h('span', { class: 'view-title' }, 'Files & Backups'),
      h('span', { class: 'badge accent' }, `${dirtyCount} modified`),
      conflictCount ? h('span', { class: 'badge err' }, `${conflictCount} conflict(s)`) : null,
      h('div', { class: 'toolbar-spacer', style: { flex: 1 } }),
      h('button', {
        class: 'button primary compact', disabled: dirtyCount === 0,
        onclick: async () => {
          const result = await api.save(null, false);
          applyValidation(await api.validation());
          const saved = result.results.filter((entry) => entry.saved).length;
          const failed = result.results.filter((entry) => entry.error);
          toast('Save All', `${saved} file(s) written${failed.length ? `, ${failed.length} blocked` : ''}`, failed.length ? 'warn' : 'ok');
          openFilesView(workspace);
        },
      }, 'Save All'),
      h('div', { class: 'view-subtitle' }, 'Every save is atomic and preceded by a timestamped backup under %LOCALAPPDATA%\\ContentStudio.')),
    h('div', { class: 'view-body' },
      h('table', { class: 'grid' },
        h('thead', {}, h('tr', {},
          h('th', {}, ''), h('th', {}, 'File'), h('th', {}, 'Type'), h('th', {}, 'Records'), h('th', {}, 'State'), h('th', {}, 'Actions'))),
        h('tbody', {}, files.map((file) => h('tr', {},
          h('td', { style: { width: '26px' } }, file.dirty ? h('span', { class: 'dirty-dot', title: 'Unsaved changes' }) : null),
          h('td', { class: 'id-cell' }, file.path),
          h('td', { class: 'low' }, file.typeId),
          h('td', { class: 'num' }, file.recordCount),
          h('td', {},
            file.parseError ? h('span', { class: 'badge err', title: file.parseError }, 'syntax error') : null,
            file.conflict ? h('span', { class: 'badge err' }, 'disk conflict') : null,
            file.dirty && !file.conflict ? h('span', { class: 'badge accent' }, 'modified') : null,
            !file.dirty && !file.conflict && !file.parseError ? h('span', { class: 'badge dim' }, 'clean') : null),
          h('td', {},
            h('div', { style: { display: 'flex', gap: '4px' } },
              file.conflict ? h('button', { class: 'button compact', onclick: () => resolveConflict(workspace, file) }, 'Resolve…') : null,
              file.dirty ? h('button', {
                class: 'button compact',
                onclick: async () => {
                  const result = await api.save([file.path], false);
                  const entry = result.results[0];
                  applyValidation(await api.validation());
                  toast(entry?.saved ? 'Saved' : 'Blocked', entry?.error ?? file.path, entry?.saved ? 'ok' : 'warn');
                  openFilesView(workspace);
                },
              }, 'Save') : null,
              file.dirty ? h('button', {
                class: 'button compact danger',
                onclick: async () => {
                  if (!await confirmModal(`Discard changes to ${file.path}?`, 'Unsaved edits in this file are lost.', 'Discard', 'danger')) return;
                  await api.revert(file.path);
                  applyValidation(await api.validation());
                  await loadRecords(file.typeId, true);
                  toast('Reverted', file.path, 'ok');
                  openFilesView(workspace);
                },
              }, 'Discard') : null,
              file.dirty ? h('button', { class: 'button compact', onclick: () => showDiff(file.path) }, 'Diff') : null,
              h('button', { class: 'button compact', onclick: () => showBackups(workspace, file) }, 'Backups'),
              h('button', { class: 'button ghost compact', onclick: () => api.open(file.path, false) }, 'Open')))))))));
}

async function showDiff(path) {
  const diff = await api.fileDiff(path);
  openModal({
    title: `Unsaved changes — ${path}`,
    wide: true,
    body: renderLineDiff(diff.disk ?? '', diff.memory ?? ''),
    actions: [{ label: 'Close' }],
  });
}

/** Minimal LCS-free line diff: common prefix/suffix, the middle shown as remove/add blocks. */
function renderLineDiff(before, after) {
  const beforeLines = before.split('\n');
  const afterLines = after.split('\n');
  let prefix = 0;
  while (prefix < beforeLines.length && prefix < afterLines.length && beforeLines[prefix] === afterLines[prefix]) prefix++;
  let suffix = 0;
  while (suffix < beforeLines.length - prefix && suffix < afterLines.length - prefix &&
         beforeLines[beforeLines.length - 1 - suffix] === afterLines[afterLines.length - 1 - suffix]) suffix++;

  const context = 3;
  const contextStart = Math.max(0, prefix - context);
  const renderColumn = (lines, changedClass) => {
    const column = h('div', { class: `diff-col ${changedClass === 'line-del' ? 'left' : ''}` });
    for (let index = contextStart; index < prefix; index++)
      column.append(h('span', { class: 'line-ctx' }, lines[index] || ' '));
    for (let index = prefix; index < lines.length - suffix; index++)
      column.append(h('span', { class: changedClass }, lines[index] || ' '));
    const suffixStart = lines.length - suffix;
    for (let index = suffixStart; index < Math.min(lines.length, suffixStart + context); index++)
      column.append(h('span', { class: 'line-ctx' }, lines[index] || ' '));
    return column;
  };

  return h('div', {},
    h('div', { style: { display: 'flex', gap: '20px', fontSize: '11px', color: 'var(--fg-faint)', marginBottom: '6px' } },
      h('span', {}, `on disk (${beforeLines.length} lines)`),
      h('span', { style: { marginLeft: 'auto' } }, `in memory (${afterLines.length} lines)`)),
    h('div', { class: 'diff-view' },
      renderColumn(beforeLines, 'line-del'),
      renderColumn(afterLines, 'line-add')));
}

async function resolveConflict(workspace, file) {
  const diff = await api.fileDiff(file.path);
  openModal({
    title: `⚠ ${file.path} changed on disk`,
    wide: true,
    body: h('div', {},
      h('p', { style: { color: 'var(--fg-dim)' } },
        'Someone (Claude, an editor, git) rewrote this file while you had unsaved edits. Left: the new disk version. Right: your in-memory version.'),
      renderLineDiff(diff.disk ?? '(file deleted)', diff.memory ?? '')),
    actions: [
      { label: 'Cancel' },
      {
        label: 'Reload Disk Version', kind: 'danger',
        onClick: async () => {
          await api.revert(file.path);
          applyValidation(await api.validation());
          await loadRecords(file.typeId, true);
          toast('Reloaded from disk', file.path, 'ok');
          openFilesView(workspace);
        },
      },
      {
        label: 'Keep My Version', kind: 'primary',
        onClick: async () => {
          await api.keepMine(file.path);
          toast('Keeping your version', 'The disk version will be backed up when you save.', 'ok');
          openFilesView(workspace);
        },
      },
    ],
  });
}

async function showBackups(workspace, file) {
  const versions = await api.backups(file.path);
  openModal({
    title: `Backups — ${file.path}`,
    body: versions.length === 0
      ? h('p', { style: { color: 'var(--fg-faint)' } }, 'No backups yet. One is taken automatically before every save.')
      : h('table', { class: 'grid' },
          h('thead', {}, h('tr', {}, h('th', {}, 'Taken (UTC)'), h('th', {}, 'Size'), h('th', {}, ''))),
          h('tbody', {}, versions.map((version) => h('tr', {},
            h('td', { class: 'id-cell' }, version.fileName.replace('.json', '').replace(/-/g, (m, i) => i === 8 ? ' ' : m)),
            h('td', { class: 'num' }, `${formatNumber(version.sizeBytes / 1024, 1)} KB`),
            h('td', {}, h('button', {
              class: 'button compact',
              onclick: async () => {
                if (!await confirmModal('Restore this backup?', 'It replaces the in-memory version; save afterwards to write it to disk.', 'Restore')) return;
                await api.restoreBackup(file.path, version.fileName);
                applyValidation(await api.validation());
                await loadRecords(file.typeId, true);
                toast('Backup restored to memory', 'Review, then Save.', 'ok');
                openFilesView(workspace);
              },
            }, 'Restore')))))),
    actions: [{ label: 'Close' }],
  });
}
