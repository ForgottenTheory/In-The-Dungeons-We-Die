// The AUTHORED → RESOLVED view for enemies: the game's real ActorResolver output with
// per-layer provenance, so "where did 45 armour come from" has an answer.

import { api } from '../api.js';
import { h, replaceChildren, formatNumber } from '../ui.js';

export async function renderResolvedActorPanel(container, actorId) {
  container.append(h('div', { class: 'spinner' }));
  let explain;
  try {
    explain = await api.analysisActor(actorId);
  } catch (error) {
    replaceChildren(container, h('div', { class: 'empty-state' }, h('div', {}, error.message)));
    return;
  }
  if (explain.error) {
    replaceChildren(container, h('div', { class: 'empty-state' }, h('div', {}, explain.error)));
    return;
  }

  const layerCell = (value, isWinner) => h('td', {
    class: `num${isWinner ? ' cell-best' : value === null || value === undefined || value === 0 ? ' low' : ''}`,
  }, value === null || value === undefined ? '—' : formatNumber(value));

  const layeredTable = (title, rows, note) => h('div', { class: 'field-group' },
    h('div', { class: 'field-group-title' }, title),
    note ? h('div', { style: { fontSize: '11px', color: 'var(--fg-faint)', margin: '-4px 0 6px' } }, note) : null,
    h('table', { class: 'grid' },
      h('thead', {}, h('tr', {},
        h('th', {}, ''), h('th', {}, 'Family'), h('th', {}, 'Role'), h('th', {}, 'Actor'), h('th', {}, 'Final'))),
      h('tbody', {}, rows)));

  const attributeRows = Object.entries(explain.attributes).map(([name, parts]) => h('tr', {},
    h('td', {}, name),
    layerCell(parts.family, false), layerCell(parts.role, false), layerCell(parts.actor, false),
    h('td', { class: 'num', style: { fontWeight: 700 } }, parts.final)));

  const resourceRows = ['health', 'mana', 'stamina'].map((pool) => h('tr', {},
    h('td', {}, pool),
    layerCell(explain.resources.family[pool], false),
    layerCell(explain.resources.role[pool], false),
    layerCell(explain.resources.actor[pool], false),
    h('td', { class: 'num', style: { fontWeight: 700 } }, explain.resources.final[pool])));

  const overrideRow = (label, entry) => h('tr', {},
    h('td', {}, label),
    layerCell(entry.family, entry.winner === 'family'),
    layerCell(entry.role, entry.winner === 'role'),
    layerCell(entry.actor, entry.winner === 'actor'),
    h('td', { class: 'num', style: { fontWeight: 700 } }, formatNumber(entry.final ?? 0)));

  const overlayRows = (dict) => Object.entries(dict).map(([key, entry]) => overrideRow(key, entry));

  const link = (id) => id
    ? h('a', { class: 'plain-link mono', style: { fontSize: '11.5px' }, onclick: () => { location.hash = `#/record/${encodeURIComponent(id)}`; } }, id)
    : h('span', { class: 'low' }, '—');

  replaceChildren(container,
    h('div', { class: 'field-group' },
      h('div', { class: 'field-group-title' }, 'Composition'),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Family'), h('span', { class: 'v' }, link(explain.familyId))),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Role'), h('span', { class: 'v' }, link(explain.roleId))),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'AI'), h('span', { class: 'v' }, link(explain.aiProfileId),
        explain.aiInlineRuleCount ? ` + ${explain.aiInlineRuleCount} inline rule(s)` : '')),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Tags'), h('span', { class: 'v' }, explain.tags.map((tag) => h('span', { class: 'chip', style: { marginRight: '4px' } }, tag))))),

    layeredTable('Attributes — additive (family + role Δ + actor Δ)', attributeRows),
    layeredTable('Resources — additive', resourceRows),
    layeredTable('Armour & Resolve — later layer wins outright', [
      overrideRow('armor', explain.armor),
      overrideRow('resolve', explain.resolve),
    ], 'Highlighted cell = the layer that decided the value.'),
    Object.keys(explain.resistances).length
      ? layeredTable('Resistances — per-lane overlay, later layer wins', overlayRows(explain.resistances))
      : null,
    Object.keys(explain.vulnerable).length
      ? layeredTable('Vulnerabilities — per-type overlay, clamped 0.50–1.50 at runtime', overlayRows(explain.vulnerable))
      : null,

    h('div', { class: 'field-group' },
      h('div', { class: 'field-group-title' }, 'Moves — actor-only, never inherited'),
      explain.moves.map((moveId) => h('div', {}, link(moveId)))),

    h('div', { class: 'field-group' },
      h('div', { class: 'field-group-title' }, 'Loot — all layers roll and merge'),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Family'), h('span', { class: 'v' }, link(explain.lootTables.family))),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Role'), h('span', { class: 'v' }, link(explain.lootTables.role))),
      h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Actor'), h('span', { class: 'v' }, link(explain.lootTables.actor)))));
}
