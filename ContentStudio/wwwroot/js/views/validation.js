// The Validation Center: every problem in the project, filterable, each row jumping
// straight to the offending record.

import { api } from '../api.js';
import { h, replaceChildren } from '../ui.js';
import { state, applyValidation, typeOf } from '../state.js';

let filters = { severity: '', source: '', search: '' };

export async function openValidationCenter(workspace) {
  const render = () => {
    const problems = (state.validation.problems ?? []).filter((problem) =>
      (!filters.severity || problem.severity === filters.severity) &&
      (!filters.source || problem.source === filters.source) &&
      (!filters.search ||
        problem.message.toLowerCase().includes(filters.search.toLowerCase()) ||
        (problem.recordId ?? '').toLowerCase().includes(filters.search.toLowerCase())));

    replaceChildren(workspace,
      h('div', { class: 'view-header' },
        h('span', { class: 'view-title' }, 'Validation'),
        h('span', { class: `badge ${state.validation.errors ? 'err' : 'ok'}` }, `${state.validation.errors} errors`),
        h('span', { class: `badge ${state.validation.warnings ? 'warn' : 'dim'}` }, `${state.validation.warnings} warnings`),
        h('div', { class: 'toolbar-spacer', style: { flex: 1 } }),
        h('button', {
          class: 'button compact', onclick: async (event) => {
            event.target.disabled = true;
            applyValidation(await api.revalidate());
            render();
          },
        }, '↻ Re-validate'),
        h('div', { class: 'view-subtitle' },
          'These are the same rules the game runs at startup (ContentValidator), plus per-record load checks and studio-only checks (unknown fields, dangling id-shaped strings).')),
      h('div', { class: 'toolbar' },
        h('input', { type: 'search', placeholder: 'Filter problems…', value: filters.search, oninput: (event) => { filters.search = event.target.value; render(); } }),
        pill(['', 'error', 'warning'], filters.severity, (value) => { filters.severity = value; render(); }, { '': 'All', error: 'Errors', warning: 'Warnings' }),
        pill(['', 'game-validator', 'load', 'studio'], filters.source, (value) => { filters.source = value; render(); },
          { '': 'All sources', 'game-validator': 'Game validator', load: 'Load', studio: 'Studio' }),
        h('span', { class: 'result-count' }, `${problems.length} shown`)),
      h('div', { class: 'view-body' },
        problems.length === 0
          ? h('div', { class: 'empty-state' }, h('div', { class: 'big' }, '✓'), h('div', {}, 'Everything the game would load, loads clean.'))
          : h('table', { class: 'grid' },
              h('thead', {}, h('tr', {}, h('th', {}, ''), h('th', {}, 'Record'), h('th', {}, 'Category'), h('th', {}, 'Message'), h('th', {}, 'File'))),
              h('tbody', {}, problems.slice(0, 800).map((problem) => h('tr', {
                onclick: () => { if (problem.recordId) location.hash = `#/record/${encodeURIComponent(problem.recordId)}`; },
              },
                h('td', { style: { width: '30px' } }, h('span', { class: `status-glyph ${problem.severity === 'error' ? 'err' : 'warn'}` }, problem.severity === 'error' ? '✕' : '⚠')),
                h('td', { class: 'id-cell' }, problem.recordId ?? h('span', { class: 'low' }, typeOf(problem.typeId ?? '')?.displayName ?? problem.typeId ?? '—')),
                h('td', {}, h('span', { class: 'badge dim' }, `${problem.source}·${problem.category}`)),
                h('td', { style: { whiteSpace: 'normal', maxWidth: '640px' } }, problem.message),
                h('td', { class: 'low', style: { fontSize: '11px' } }, problem.filePath ?? '')))))));
  };
  render();
}

function pill(values, current, onPick, labels) {
  return h('div', { class: 'pill-toggle' },
    values.map((value) => h('button', { class: value === current ? 'active' : '', onclick: () => onPick(value) }, labels[value] ?? value)));
}
