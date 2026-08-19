// The landing page: content counts, validation health, unsaved work, top balance warnings.

import { api } from '../api.js';
import { h, replaceChildren, formatNumber } from '../ui.js';
import { state } from '../state.js';

export async function openDashboard(workspace) {
  replaceChildren(workspace,
    h('div', { class: 'view-header' },
      h('span', { class: 'view-title' }, 'Dashboard'),
      h('div', { class: 'view-subtitle' }, state.status?.projectRoot ?? '')),
    h('div', { class: 'view-body padded' }, h('div', { class: 'spinner' })));

  const body = workspace.querySelector('.view-body');
  const [status, warningsPayload] = await Promise.all([
    api.status(),
    api.analysisWarnings().catch(() => []),
  ]);
  const validation = state.validation;
  const groups = new Map();
  for (const type of state.meta?.types ?? []) {
    if (!groups.has(type.group)) groups.set(type.group, []);
    groups.get(type.group).push(type);
  }

  const validationCard = h('div', { class: 'card' },
    h('div', { class: 'card-title' }, 'Validation'),
    h('div', { style: { display: 'flex', gap: '18px' } },
      h('div', { class: 'big-stat' }, h('span', { class: 'n', style: { color: validation.errors ? 'var(--err)' : 'var(--ok)' } }, validation.errors), h('span', { class: 'lbl' }, 'errors')),
      h('div', { class: 'big-stat' }, h('span', { class: 'n', style: { color: validation.warnings ? 'var(--warn)' : 'var(--fg-dim)' } }, validation.warnings), h('span', { class: 'lbl' }, 'warnings'))),
    h('div', { style: { marginTop: '10px' } },
      h('a', { class: 'plain-link', onclick: () => { location.hash = '#/validation'; } }, 'Open Validation Center →')));

  const workCard = h('div', { class: 'card' },
    h('div', { class: 'card-title' }, 'Working State'),
    h('div', { class: 'big-stat' }, h('span', { class: 'n' }, status.recordCount), h('span', { class: 'lbl' }, 'records loaded')),
    h('div', { style: { marginTop: '8px', fontSize: '12px', color: 'var(--fg-dim)' } },
      status.dirtyFiles.length === 0 ? 'No unsaved changes.' : `${status.dirtyFiles.length} file(s) with unsaved changes:`),
    status.dirtyFiles.slice(0, 6).map((file) => h('div', { class: 'inspector-row' }, h('span', { class: 'v mono' }, file))),
    status.conflictFiles.length ? h('div', { style: { color: 'var(--err)', marginTop: '6px', fontSize: '12px' } }, `${status.conflictFiles.length} disk conflict(s) — resolve in Files.`) : null,
    h('div', { style: { marginTop: '10px' } },
      h('a', { class: 'plain-link', onclick: () => { location.hash = '#/files'; } }, 'Files & Backups →')));

  const contentCards = [...groups.entries()].map(([group, types]) => h('div', { class: 'card' },
    h('div', { class: 'card-title' }, group),
    types.map((type) => h('div', { class: 'count-row', onclick: () => { location.hash = `#/type/${type.typeId}`; } },
      h('span', {}, type.displayName),
      h('span', { class: 'count-value' }, type.recordCount)))));

  const warnings = Array.isArray(warningsPayload) ? warningsPayload : [];
  const warningsCard = h('div', { class: 'card', style: { gridColumn: 'span 2' } },
    h('div', { class: 'card-title' }, `Balance Warnings (${warnings.length})`),
    warnings.length === 0
      ? h('div', { style: { color: 'var(--fg-faint)', fontSize: '12px' } }, 'Nothing suspicious detected.')
      : warnings.slice(0, 10).map((warning) => h('div', {
          class: 'warning-row',
          onclick: () => {
            if (warning.recordId) location.hash = `#/record/${encodeURIComponent(warning.recordId)}`;
            else location.hash = '#/balance/warnings';
          },
        },
          h('div', { class: 'w-area' }, warning.area),
          warning.message)),
    warnings.length > 10 ? h('a', { class: 'plain-link', onclick: () => { location.hash = '#/balance/warnings'; } }, `All ${warnings.length} warnings →`) : null);

  replaceChildren(body, h('div', { class: 'dash-grid' },
    validationCard, workCard, warningsCard, contentCards));
}
