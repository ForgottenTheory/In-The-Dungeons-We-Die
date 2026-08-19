// The Dependency Explorer: pick any record, see what it uses (grouped by field) and what
// uses it (grouped by type). Clicking re-centres, building a breadcrumb trail.

import { api } from '../api.js';
import { h, replaceChildren } from '../ui.js';
import { typeOf, findRecord } from '../state.js';
import { refInput } from '../fields.js';
import { state } from '../state.js';

const trail = [];

export async function openDependencyExplorer(workspace, recordId) {
  if (!recordId) {
    replaceChildren(workspace,
      h('div', { class: 'view-header' }, h('span', { class: 'view-title' }, 'Dependency Explorer')),
      h('div', { class: 'view-body padded' },
        h('div', { style: { maxWidth: '440px' } },
          h('div', { style: { marginBottom: '8px', color: 'var(--fg-dim)' } }, 'Pick any record to explore its reference graph:'),
          refInput(state.meta?.types.map((type) => type.typeId) ?? [], '', (id) => {
            if (id) location.hash = `#/deps/${encodeURIComponent(id)}`;
          }, { placeholder: 'Search everything…' }))));
    return;
  }

  if (trail[trail.length - 1] !== recordId) trail.push(recordId);
  if (trail.length > 12) trail.shift();

  replaceChildren(workspace, h('div', { class: 'spinner' }));
  const deps = await api.deps(recordId);
  const record = findRecord(recordId);
  const type = record ? typeOf(record.typeId) : null;

  const nodeChip = (edge) => h('div', {
    class: 'ref-link', style: { padding: '5px 8px', border: '1px solid var(--border)', borderRadius: '6px', marginBottom: '4px', background: 'var(--bg-panel)' },
    onclick: () => { location.hash = `#/deps/${encodeURIComponent(edge.id)}`; },
    oncontextmenu: (event) => { event.preventDefault(); location.hash = `#/record/${encodeURIComponent(edge.id)}`; },
    title: `${edge.id}\nClick: explore · Right-click: open editor`,
  },
    h('span', { class: 'badge dim', style: { flex: 'none' } }, typeOf(edge.typeId)?.singularName ?? edge.typeId ?? '?'),
    h('span', { class: 'ref-link-name' }, edge.name ?? edge.id),
    h('span', { class: 'ref-link-path' }, edge.fieldPath));

  const groupBy = (edges, keyOf) => {
    const groups = new Map();
    for (const edge of edges) {
      const key = keyOf(edge);
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key).push(edge);
    }
    return [...groups.entries()].sort((left, right) => left[0].localeCompare(right[0]));
  };

  const outgoingGroups = groupBy(deps.outgoing, (edge) => edge.fieldPath.replace(/\[\d+\].*$/, '').split('.')[0]);
  const incomingGroups = groupBy(deps.incoming, (edge) => typeOf(edge.typeId)?.displayName ?? edge.typeId ?? '?');

  replaceChildren(workspace,
    h('div', { class: 'view-header' },
      h('span', { class: 'view-title' }, 'Dependency Explorer'),
      h('div', { class: 'toolbar-spacer', style: { flex: 1 } }),
      h('button', { class: 'button compact', onclick: () => { location.hash = `#/record/${encodeURIComponent(recordId)}`; } }, 'Open in Editor'),
      h('div', { class: 'view-subtitle' },
        h('span', { class: 'breadcrumbs' },
          trail.slice(-6).map((id, index, shown) => [
            index > 0 ? h('span', { class: 'sep' }, '→') : null,
            id === recordId
              ? h('span', { style: { color: 'var(--fg)' } }, id)
              : h('a', { onclick: () => { location.hash = `#/deps/${encodeURIComponent(id)}`; } }, id),
          ])))),
    h('div', { class: 'view-body padded' },
      h('div', { class: 'deps-columns' },
        h('div', {},
          h('div', { class: 'deps-group-title', style: { textAlign: 'right' } }, `Uses (${deps.outgoing.length})`),
          outgoingGroups.length === 0 ? h('div', { style: { color: 'var(--fg-faint)', textAlign: 'right' } }, 'nothing') : null,
          outgoingGroups.map(([field, edges]) => h('div', { class: 'deps-group' },
            h('div', { class: 'deps-group-title', style: { textAlign: 'right' } }, field),
            edges.map(nodeChip)))),
        h('div', { class: 'deps-center' },
          h('div', { class: 'deps-node' },
            h('div', { class: 'deps-node-name' }, record?.name ?? recordId),
            h('div', { class: 'deps-node-id' }, recordId),
            type ? h('div', { style: { marginTop: '4px' } }, h('span', { class: 'badge accent' }, type.singularName)) : null),
          h('div', { style: { marginTop: '10px', fontSize: '10.5px', color: 'var(--fg-faint)' } },
            '← what it needs · what needs it →')),
        h('div', {},
          h('div', { class: 'deps-group-title' }, `Used by (${deps.incoming.length})`),
          incomingGroups.length === 0 ? h('div', { style: { color: 'var(--fg-faint)' } }, 'nothing — check for orphaned content') : null,
          incomingGroups.map(([typeName, edges]) => h('div', { class: 'deps-group' },
            h('div', { class: 'deps-group-title' }, `${typeName} (${edges.length})`),
            edges.slice(0, 60).map(nodeChip),
            edges.length > 60 ? h('div', { style: { color: 'var(--fg-faint)', fontSize: '11px' } }, `…and ${edges.length - 60} more`) : null))))));
}
