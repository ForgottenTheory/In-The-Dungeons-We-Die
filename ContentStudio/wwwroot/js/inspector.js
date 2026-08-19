// The right-hand inspector panel: identity, validation problems and the live reference
// graph for whatever is focused.

import { api } from './api.js';
import { h, replaceChildren, toast } from './ui.js';
import { findRecord, typeOf, problemsOf } from './state.js';

const inspector = () => document.getElementById('inspector');

export function renderInspectorEmpty(message = 'Select something to inspect.') {
  replaceChildren(inspector(), h('div', { class: 'inspector-section', style: { color: 'var(--fg-faint)', fontSize: '12px' } }, message));
}

export async function renderInspectorForRecord(id) {
  const record = findRecord(id);
  if (!record) { renderInspectorEmpty(); return; }
  const type = typeOf(record.typeId);
  const problems = problemsOf(id);
  const root = inspector();

  const referencesSection = h('div', { class: 'inspector-section' },
    h('div', { class: 'inspector-title' }, 'References'),
    h('div', { class: 'spinner' }));

  replaceChildren(root,
    h('div', { class: 'inspector-section' },
      h('div', { class: 'inspector-title' }, 'Inspector'),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Name'), h('span', { class: 'v' }, record.name ?? '—')),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Id'), h('span', { class: 'v mono' }, record.id)),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Type'), h('span', { class: 'v' }, type?.singularName ?? record.typeId)),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'File'), h('span', { class: 'v mono' }, record.file)),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'State'),
        h('span', { class: 'v' },
          record.fileDirty ? h('span', { class: 'badge accent' }, 'unsaved') : h('span', { class: 'badge dim' }, 'saved'),
          ' ',
          record.conflict ? h('span', { class: 'badge err' }, 'disk conflict') : null))),

    h('div', { class: 'inspector-section' },
      h('div', { class: 'inspector-title' }, `Validation ${problems.length ? `(${problems.length})` : ''}`),
      problems.length === 0
        ? h('div', { style: { color: 'var(--ok)', fontSize: '12px' } }, '✓ No problems')
        : problems.map((problem) => h('div', { class: `problem-item ${problem.severity}` },
            h('div', { class: 'problem-source' }, `${problem.source} · ${problem.category}`),
            problem.message))),

    referencesSection,

    h('div', { class: 'inspector-section' },
      h('div', { class: 'inspector-title' }, 'Actions'),
      h('div', { style: { display: 'flex', flexWrap: 'wrap', gap: '6px' } },
        h('button', { class: 'button compact', onclick: () => navigator.clipboard.writeText(record.id).then(() => toast('Copied id', record.id, 'ok', 1200)) }, 'Copy ID'),
        h('button', { class: 'button compact', onclick: () => navigator.clipboard.writeText(JSON.stringify(record.data, null, 2)).then(() => toast('Copied JSON', '', 'ok', 1200)) }, 'Copy JSON'),
        h('button', { class: 'button compact', onclick: () => api.open(record.file, false).catch((e) => toast('Open failed', e.message, 'err')) }, 'Open File'),
        h('button', { class: 'button compact', onclick: () => api.open(record.file, true).catch((e) => toast('Reveal failed', e.message, 'err')) }, 'Reveal'),
        h('button', { class: 'button compact', onclick: () => { location.hash = `#/deps/${encodeURIComponent(record.id)}`; } }, 'Dependencies'))));

  try {
    const deps = await api.deps(id);
    const link = (edge) => h('div', {
      class: 'ref-link',
      onclick: () => { location.hash = `#/record/${encodeURIComponent(edge.id)}`; },
      title: edge.id,
    },
      h('span', { class: 'ref-link-name' }, edge.name ?? edge.id),
      h('span', { class: 'ref-link-path' }, edge.fieldPath));
    replaceChildren(referencesSection,
      h('div', { class: 'inspector-title' }, 'References'),
      h('div', { style: { fontSize: '11px', color: 'var(--fg-faint)', margin: '2px 0 4px' } }, `Uses ${deps.outgoing.length}`),
      deps.outgoing.length ? deps.outgoing.slice(0, 40).map(link) : h('div', { style: { color: 'var(--fg-faint)', fontSize: '11.5px' } }, 'nothing'),
      h('div', { style: { fontSize: '11px', color: 'var(--fg-faint)', margin: '8px 0 4px' } }, `Used by ${deps.incoming.length}`),
      deps.incoming.length ? deps.incoming.slice(0, 40).map(link) : h('div', { style: { color: 'var(--fg-faint)', fontSize: '11.5px' } }, 'nothing'));
  } catch {
    replaceChildren(referencesSection,
      h('div', { class: 'inspector-title' }, 'References'),
      h('div', { style: { color: 'var(--fg-faint)' } }, 'unavailable'));
  }
}

export function renderInspectorForSelection(typeId, ids) {
  const type = typeOf(typeId);
  replaceChildren(inspector(),
    h('div', { class: 'inspector-section' },
      h('div', { class: 'inspector-title' }, 'Selection'),
      h('div', { class: 'big-stat' }, h('span', { class: 'n' }, ids.length), h('span', { class: 'lbl' }, `${type?.displayName ?? typeId} selected`)),
      h('div', { style: { marginTop: '10px', fontSize: '11.5px', color: 'var(--fg-faint)', lineHeight: 1.6 } },
        'Use the bulk bar above the table to compare, tag, edit a field across the selection, or delete.')));
}
