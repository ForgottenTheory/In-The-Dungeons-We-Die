// Drop Analysis tab for one loot table: expected value per roll under a chosen context.

import { api } from '../api.js';
import { h, replaceChildren, formatNumber } from '../ui.js';
import { findRecord } from '../state.js';
import { barChart, chartCard } from '../charts.js';

const context = { depth: 1, active: true, rank: '' };

export function renderLootAnalysisPanel(container, tableId) {
  const controls = h('div', { class: 'toolbar', style: { padding: '0 0 10px', border: 'none' } },
    h('span', { style: { color: 'var(--fg-dim)' } }, 'Context:'),
    labelled('Depth', h('input', {
      type: 'number', min: 0, max: 9, value: context.depth, style: { width: '58px' },
      onchange: (event) => { context.depth = Number(event.target.value) || 0; refresh(); },
    })),
    pill(['active', 'passive'], context.active ? 'active' : 'passive', (value) => { context.active = value === 'active'; refresh(); }),
    pill(['normal', 'elite', 'boss'], context.rank || 'normal', (value) => { context.rank = value === 'normal' ? '' : value; refresh(); }));

  const results = h('div', {});
  replaceChildren(container, controls, results);

  async function refresh() {
    replaceChildren(results, h('div', { class: 'spinner' }));
    try {
      const expectation = await api.analysisLootTable(tableId, { depth: context.depth, active: context.active, rank: context.rank || null });
      const items = expectation.items ?? [];
      replaceChildren(results,
        h('div', { style: { display: 'flex', gap: '18px', margin: '4px 0 12px' } },
          h('div', { class: 'big-stat' }, h('span', { class: 'n' }, items.length), h('span', { class: 'lbl' }, 'distinct items reachable')),
          h('div', { class: 'big-stat' }, h('span', { class: 'n' }, formatNumber(expectation.expectedGold, 2)), h('span', { class: 'lbl' }, 'expected gold / roll'))),
        items.length
          ? chartCard('Expected drops per roll (nested tables walked)', barChart({
              items: items.slice(0, 40).map((item) => ({
                label: findRecord(item.itemId)?.name ?? item.itemId,
                value: item.expectedPerRoll,
                onClick: () => { location.hash = `#/record/${encodeURIComponent(item.itemId)}`; },
                tooltip: [item.itemId, `EV ${formatNumber(item.expectedPerRoll, 4)} / roll`, `≈ 1 per ${formatNumber(1 / Math.max(item.expectedPerRoll, 1e-9), 1)} rolls`],
              })),
              width: 640,
            }))
          : h('div', { class: 'empty-state' }, h('div', {}, 'This table pays nothing under this context.')),
        expectation.nestedTableIds?.length
          ? h('div', { style: { marginTop: '12px' } },
              h('div', { class: 'section-title' }, 'Nested tables walked'),
              expectation.nestedTableIds.map((nestedId) => h('a', {
                class: 'plain-link mono', style: { display: 'inline-block', marginRight: '12px', fontSize: '11.5px' },
                onclick: () => { location.hash = `#/record/${encodeURIComponent(nestedId)}`; },
              }, nestedId)))
          : null);
    } catch (error) {
      replaceChildren(results, h('div', { class: 'empty-state' }, h('div', {}, error.message)));
    }
  }
  refresh();
}

function labelled(label, control) {
  return h('label', { style: { display: 'flex', gap: '6px', alignItems: 'center', fontSize: '12px', color: 'var(--fg-dim)' } }, label, control);
}

function pill(values, current, onPick) {
  return h('div', { class: 'pill-toggle' },
    values.map((value) => h('button', { class: value === current ? 'active' : '', onclick: () => onPick(value) }, value)));
}
