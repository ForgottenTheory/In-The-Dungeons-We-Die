// Comparison mode: N records side by side, fields as rows, differences highlighted,
// numeric best/worst tinted.

import { h, replaceChildren, formatNumber } from '../ui.js';
import { findRecord, typeOf } from '../state.js';

export function openCompare(workspace, ids) {
  const records = ids.map(findRecord).filter(Boolean);
  if (records.length < 2) {
    replaceChildren(workspace, h('div', { class: 'empty-state' },
      h('div', { class: 'big' }, '⇄'),
      h('div', {}, 'Select two or more records in a list and choose Compare.')));
    return;
  }
  const type = typeOf(records[0].typeId);

  // Collect the union of leaf paths across all records so nothing silently disappears.
  const paths = [];
  const seen = new Set();
  const collect = (value, path) => {
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      for (const key of Object.keys(value)) collect(value[key], path ? `${path}.${key}` : key);
      return;
    }
    if (!seen.has(path)) { seen.add(path); paths.push(path); }
  };
  for (const record of records) collect(record.data, '');

  const valueAt = (record, path) => path.split('.').reduce((current, key) => current?.[key], record.data);
  const display = (value) => {
    if (value === undefined) return h('span', { class: 'low' }, '—');
    if (Array.isArray(value)) return value.length <= 4 && value.every((item) => typeof item !== 'object')
      ? value.join(', ')
      : `[${value.length} items]`;
    if (typeof value === 'number') return formatNumber(value);
    if (typeof value === 'boolean') return value ? 'true' : 'false';
    return String(value);
  };

  const rows = paths.map((path) => {
    const values = records.map((record) => valueAt(record, path));
    const allEqual = values.every((value) => JSON.stringify(value) === JSON.stringify(values[0]));
    if (allEqual && values[0] === undefined) return null;
    const numeric = values.every((value) => typeof value === 'number' || value === undefined) && values.some((value) => typeof value === 'number');
    let best = null; let worst = null;
    if (numeric && !allEqual) {
      const numbers = values.filter((value) => typeof value === 'number');
      best = Math.max(...numbers);
      worst = Math.min(...numbers);
    }
    return h('tr', { class: allEqual ? '' : '' },
      h('td', { class: 'id-cell', style: { position: 'sticky', left: 0, background: 'var(--bg-app)' } }, path),
      values.map((value) => h('td', {
        class: [
          !allEqual ? 'cell-diff' : '',
          numeric && value === best && best !== worst ? 'cell-best' : '',
          numeric && value === worst && best !== worst ? 'cell-worst' : '',
          typeof value === 'number' ? 'num' : '',
        ].join(' '),
      }, display(value))));
  }).filter(Boolean);

  replaceChildren(workspace,
    h('div', { class: 'view-header' },
      h('span', { class: 'view-title' }, `Compare ${records.length} ${type?.displayName ?? 'records'}`),
      h('div', { class: 'toolbar-spacer', style: { flex: 1 } }),
      h('button', { class: 'button compact', onclick: () => { location.hash = `#/type/${records[0].typeId}`; } }, 'Back to list'),
      h('div', { class: 'view-subtitle' }, 'Differing rows are tinted; green = highest, red = lowest. Identical undefined rows are hidden.')),
    h('div', { class: 'view-body' },
      h('table', { class: 'grid' },
        h('thead', {}, h('tr', {},
          h('th', { style: { position: 'sticky', left: 0, zIndex: 3, background: 'var(--bg-app)' } }, 'Field'),
          records.map((record) => h('th', { style: { cursor: 'pointer' }, onclick: () => { location.hash = `#/record/${encodeURIComponent(record.id)}`; } }, record.name ?? record.id)))),
        h('tbody', {}, rows))));
}
