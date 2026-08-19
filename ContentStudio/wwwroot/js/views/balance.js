// Balance Studio: read-only analysis over the real content — resolved enemy stats, move
// economics, the material library's property space, profession rates and loot expectations.
// It flags and visualizes; it never changes a value.

import { api } from '../api.js';
import { h, replaceChildren, formatNumber } from '../ui.js';
import { recordsOf, loadRecords, findRecord, vocabList } from '../state.js';
import { barChart, histogram, scatterPlot, heatmap, lineChart, chartCard, colorAt } from '../charts.js';
import { refInput } from '../fields.js';

const SECTIONS = [
  ['enemies', 'Enemies'], ['moves', 'Moves'], ['materials', 'Materials'],
  ['professions', 'Professions'], ['loot', 'Loot'], ['warnings', 'Warnings'],
];

export async function openBalance(workspace, section = 'enemies') {
  const header = h('div', { class: 'view-header' },
    h('span', { class: 'view-title' }, 'Balance Studio'),
    h('div', { class: 'pill-toggle', style: { marginLeft: '12px' } },
      SECTIONS.map(([key, label]) => h('button', {
        class: key === section ? 'active' : '',
        onclick: () => { location.hash = `#/balance/${key}`; },
      }, label))),
    h('div', { class: 'view-subtitle' }, 'Analysis only — nothing here changes content. Click any mark or row to open the record behind it.'));

  const body = h('div', { class: 'view-body padded' }, h('div', { class: 'spinner' }));
  replaceChildren(workspace, header, body);

  try {
    switch (section) {
      case 'enemies': await renderEnemies(body); break;
      case 'moves': await renderMoves(body); break;
      case 'materials': await renderMaterials(body); break;
      case 'professions': await renderProfessions(body); break;
      case 'loot': await renderLoot(body); break;
      case 'warnings': await renderWarnings(body); break;
      default: await renderEnemies(body);
    }
  } catch (error) {
    replaceChildren(body, h('div', { class: 'empty-state' }, h('div', {}, error.message)));
  }
}

const open = (id) => () => { location.hash = `#/record/${encodeURIComponent(id)}`; };

function sortableTable({ columns, rows, initialSort }) {
  let sortKey = initialSort ?? columns[0].key;
  let sortDir = 1;
  const container = h('div', {});
  const render = () => {
    const sorted = [...rows].sort((left, right) => {
      const a = left[sortKey]; const b = right[sortKey];
      if (typeof a === 'number' && typeof b === 'number') return (a - b) * sortDir;
      return String(a ?? '').localeCompare(String(b ?? '')) * sortDir;
    });
    replaceChildren(container, h('table', { class: 'grid' },
      h('thead', {}, h('tr', {}, columns.map((column) => h('th', {
        onclick: () => { if (sortKey === column.key) sortDir *= -1; else { sortKey = column.key; sortDir = column.numeric ? -1 : 1; } render(); },
      }, column.label, sortKey === column.key ? h('span', { class: 'sort-arrow' }, sortDir > 0 ? '▲' : '▼') : null)))),
      h('tbody', {}, sorted.map((row) => h('tr', { onclick: row.__open },
        columns.map((column) => {
          const value = row[column.key];
          return h('td', { class: column.numeric ? 'num' : column.mono ? 'id-cell' : '' },
            column.render ? column.render(value, row) : (typeof value === 'number' ? formatNumber(value, column.digits ?? 1) : (value ?? '—')));
        }))))));
  };
  render();
  return container;
}

// ── Enemies ───────────────────────────────────────────────────────────────────────────────

async function renderEnemies(body) {
  const payload = await api.analysisEnemies();
  const rows = payload.rows;
  const lanes = vocabList('damageLanes');

  const tableRows = rows.map((row) => ({
    __open: open(row.id),
    name: row.name, id: row.id, rank: row.rank,
    family: row.familyId?.replace('family.', '') ?? '—',
    role: row.roleId?.replace('role.', '') ?? '—',
    hp: row.health, armour: row.effectiveArmour, resolve: row.resolve,
    ehpPhys: (row.effectiveHp.Slashing + row.effectiveHp.Crushing + row.effectiveHp.Piercing) / 3,
    ehpMagic: row.effectiveHp.Magic,
  }));

  // Heatmaps only earn their pixels for enemies that actually bend a lane or a type;
  // hundreds of all-zero rows would bury the signal (and the DOM).
  const HeatmapRowCap = 80;
  const resistanceRows = rows.filter((row) => lanes.some((lane) => (row.resistances[lane] ?? 0) !== 0)).slice(0, HeatmapRowCap);
  const resistanceMatrix = resistanceRows.map((row) => lanes.map((lane) => row.resistances[lane] ?? 0));
  const vulnerabilityTypes = ['Slashing', 'Crushing', 'Piercing', 'Magic'];
  const vulnerabilityRows = rows.filter((row) => vulnerabilityTypes.some((type) => (row.vulnerable[type] ?? 1) !== 1)).slice(0, HeatmapRowCap);
  const vulnerabilityMatrix = vulnerabilityRows.map((row) => vulnerabilityTypes.map((type) => (row.vulnerable[type] ?? 1) - 1));

  replaceChildren(body,
    h('div', { class: 'section-title', style: { marginTop: 0 } }, `${rows.length} enemies, resolved through the game's ActorResolver (family + role + actor)`),
    sortableTable({
      initialSort: 'hp',
      columns: [
        { key: 'name', label: 'Enemy' }, { key: 'rank', label: 'Rank' },
        { key: 'family', label: 'Family' }, { key: 'role', label: 'Role' },
        { key: 'hp', label: 'HP', numeric: true, digits: 0 },
        { key: 'armour', label: 'Armour*', numeric: true, digits: 1 },
        { key: 'resolve', label: 'Resolve', numeric: true, digits: 0 },
        { key: 'ehpPhys', label: 'EHP phys', numeric: true, digits: 0 },
        { key: 'ehpMagic', label: 'EHP magic', numeric: true, digits: 0 },
      ],
      rows: tableRows,
    }),
    h('div', { style: { fontSize: '11px', color: 'var(--fg-faint)', margin: '4px 0 14px' } },
      `*Effective armour = constitution × 0.3 + authored armour. EHP = health ÷ damage multiplier for a reference ${payload.referencePacket}-damage packet, using the real pipeline order (armour → capped resistance → clamped vulnerability).`),
    h('div', { class: 'charts-grid' },
      chartCard('Health distribution', histogram({ values: rows.map((row) => row.health), buckets: 24 })),
      chartCard('Health vs effective armour', scatterPlot({
        points: rows.map((row, index) => ({
          x: row.effectiveArmour, y: row.health, label: row.name,
          color: row.rank === 'Boss' ? '#f47272' : row.rank === 'Elite' ? '#f4bf4f' : colorAt(0),
          onClick: open(row.id),
        })),
        xLabel: 'effective armour', yLabel: 'health',
      }), 'red = boss, amber = elite'),
      chartCard('Resolve distribution', histogram({ values: rows.map((row) => row.resolve), buckets: 20, color: '#c792ea' }))),
    h('div', { class: 'section-title' },
      `Resistance heatmap — ${resistanceRows.length} enemies with a non-zero lane (green resists, red is a real weakness)`),
    h('div', { class: 'chart-card', style: { overflowX: 'auto' } },
      heatmap({
        rowLabels: resistanceRows.map((row) => row.name),
        columnLabels: lanes,
        values: resistanceMatrix,
        width: Math.max(620, lanes.length * 64 + 150),
        onCellClick: (rowIndex) => open(resistanceRows[rowIndex].id)(),
      })),
    h('div', { class: 'section-title' },
      `Vulnerability heatmap — ${vulnerabilityRows.length} enemies deviating from ×1.0 (green takes less, red takes more)`),
    h('div', { class: 'chart-card', style: { overflowX: 'auto' } },
      heatmap({
        rowLabels: vulnerabilityRows.map((row) => row.name),
        columnLabels: vulnerabilityTypes,
        values: vulnerabilityMatrix,
        width: 560,
        format: (value) => `${value >= 0 ? '+' : ''}${Math.round(value * 100)}%`,
        onCellClick: (rowIndex) => open(vulnerabilityRows[rowIndex].id)(),
      })));
}

// ── Moves ─────────────────────────────────────────────────────────────────────────────────

async function renderMoves(body) {
  const payload = await api.analysisMoves();
  const rows = payload.rows;

  replaceChildren(body,
    h('div', { class: 'section-title', style: { marginTop: 0 } },
      `${rows.length} moves. DPS alone does not define quality — telegraphs, control, riders and costs sit beside it on purpose.`),
    sortableTable({
      initialSort: 'dps',
      columns: [
        { key: 'name', label: 'Move' },
        { key: 'kind', label: 'Kind' },
        { key: 'damage', label: 'Dmg', numeric: true, digits: 0 },
        { key: 'timeToImpact', label: 'Impact (t)', numeric: true, digits: 0 },
        { key: 'cycle', label: 'Cycle (t)', numeric: true, digits: 0 },
        { key: 'cooldown', label: 'CD', numeric: true, digits: 0 },
        { key: 'stagger', label: 'Stagger', numeric: true, digits: 0 },
        { key: 'cost', label: 'Cost', mono: true },
        { key: 'dps', label: 'Dmg/s cycle', numeric: true },
        { key: 'dpst', label: 'Dmg/stam', numeric: true },
        { key: 'riders', label: 'Effects' },
      ],
      rows: rows.map((row) => ({
        __open: open(row.id),
        name: row.name, kind: row.kind, damage: row.totalDamage,
        timeToImpact: row.timeToImpactTicks, cycle: row.cycleTicks, cooldown: row.cooldownTicks,
        stagger: row.staggerPower,
        cost: Object.entries(row.costs).map(([resource, amount]) => `${amount} ${resource.slice(0, 4)}`).join(', ') || '—',
        dps: row.damagePerSecondOfCycle, dpst: row.damagePerStamina,
        riders: row.effectSummaries.join('; ') || '—',
      })),
    }),
    h('div', { class: 'charts-grid', style: { marginTop: '14px' } },
      chartCard('Damage vs time-to-impact', scatterPlot({
        points: rows.filter((row) => row.totalDamage > 0).map((row) => ({
          x: row.timeToImpactTicks, y: row.totalDamage, label: row.name, onClick: open(row.id),
          color: row.kind === 'Spell' ? '#c792ea' : colorAt(0),
        })),
        xLabel: 'telegraph+windup ticks', yLabel: 'damage',
      }), 'slow moves should justify themselves — purple = spells'),
      chartCard('Stagger vs damage', scatterPlot({
        points: rows.filter((row) => row.staggerPower > 0 || row.totalDamage > 0).map((row) => ({
          x: row.totalDamage, y: row.staggerPower, label: row.name, onClick: open(row.id), color: '#f4bf4f',
        })),
        xLabel: 'damage', yLabel: 'stagger power',
      })),
      chartCard('Damage per second of cycle', barChart({
        items: rows.filter((row) => row.damagePerSecondOfCycle > 0)
          .sort((left, right) => right.damagePerSecondOfCycle - left.damagePerSecondOfCycle)
          .map((row) => ({ label: row.name, value: row.damagePerSecondOfCycle, onClick: open(row.id) })),
        maxBars: 24,
      }))));
}

// ── Materials ─────────────────────────────────────────────────────────────────────────────

const materialView = { x: 'hardness', y: 'flexibility', dist: 'conductivity', query: '' };

async function renderMaterials(body) {
  await loadRecords('materials');
  const materials = recordsOf('materials');
  const propertyNames = vocabList('propertyNames');

  const parseQuery = (query) => {
    // Grammar: "hardness > 70", "tag = form:metal", clauses joined by "and".
    const clauses = query.split(/\band\b/i).map((clause) => clause.trim()).filter(Boolean);
    const predicates = [];
    for (const clause of clauses) {
      const tagMatch = clause.match(/^tag\s*=\s*(\S+)$/i);
      if (tagMatch) { predicates.push((record) => (record.data.tags ?? []).includes(tagMatch[1])); continue; }
      const comparison = clause.match(/^([a-z_]+)\s*(>=|<=|>|<|=)\s*(-?\d+(?:\.\d+)?)$/i);
      if (comparison) {
        const [, property, operator, rawValue] = comparison;
        const threshold = Number(rawValue);
        predicates.push((record) => {
          const value = record.data.properties?.[property] ?? 0;
          switch (operator) {
            case '>': return value > threshold;
            case '<': return value < threshold;
            case '>=': return value >= threshold;
            case '<=': return value <= threshold;
            default: return value === threshold;
          }
        });
        continue;
      }
      const text = clause.toLowerCase();
      predicates.push((record) => record.id.includes(text) || (record.name ?? '').toLowerCase().includes(text));
    }
    return (record) => predicates.every((predicate) => predicate(record));
  };

  const filtered = materialView.query ? materials.filter(parseQuery(materialView.query)) : materials;

  const propertySelect = (current, onPick) => h('select', { onchange: (event) => onPick(event.target.value) },
    propertyNames.map((name) => h('option', { value: name, selected: name === current }, name)));

  const rarityColor = (record) => {
    const tags = record.data.tags ?? [];
    if (tags.includes('rarity:very_rare') || tags.includes('rarity:exceptional')) return '#f47272';
    if (tags.includes('rarity:rare')) return '#c792ea';
    if (tags.includes('rarity:uncommon')) return '#f4bf4f';
    return '#7aa2ff';
  };

  const valuesOf = (property) => filtered.map((record) => record.data.properties?.[property]).filter((value) => value !== undefined);

  const coverage = propertyNames.map((property) => ({
    property,
    count: materials.filter((record) => record.data.properties?.[property] !== undefined).length,
  })).sort((left, right) => right.count - left.count);

  replaceChildren(body,
    h('div', { class: 'toolbar', style: { padding: '0 0 10px', border: 'none' } },
      h('input', {
        type: 'search', style: { width: '380px', fontFamily: 'var(--mono)', fontSize: '11.5px' },
        placeholder: 'hardness > 70 and conductivity > 50 and tag = form:metal',
        value: materialView.query,
        onchange: (event) => { materialView.query = event.target.value; renderMaterials(body); },
      }),
      h('span', { class: 'result-count' }, `${filtered.length} / ${materials.length} materials`)),
    h('div', { class: 'charts-grid' },
      chartCard(
        h('span', {}, 'Scatter: ', propertySelect(materialView.x, (value) => { materialView.x = value; renderMaterials(body); }),
          ' vs ', propertySelect(materialView.y, (value) => { materialView.y = value; renderMaterials(body); })),
        scatterPlot({
          points: filtered.map((record) => ({
            x: record.data.properties?.[materialView.x] ?? 0,
            y: record.data.properties?.[materialView.y] ?? 0,
            label: record.name ?? record.id,
            color: rarityColor(record),
            onClick: open(record.id),
            size: 3.5,
          })),
          xLabel: materialView.x, yLabel: materialView.y, height: 300,
        }), 'colour = rarity tag (blue common → amber uncommon → purple rare → red very rare)'),
      chartCard(
        h('span', {}, 'Distribution: ', propertySelect(materialView.dist, (value) => { materialView.dist = value; renderMaterials(body); })),
        histogram({ values: valuesOf(materialView.dist), min: 0, max: 100, buckets: 25, height: 210 }),
        `${valuesOf(materialView.dist).length} materials carry ${materialView.dist}; absent = 0 by design and not plotted`),
      chartCard('Property coverage — how many materials carry each property', barChart({
        items: coverage.map((entry) => ({ label: entry.property, value: entry.count })),
        width: 460, maxBars: 25,
      }))),
    h('div', { class: 'section-title' }, `Matches (${filtered.length})`),
    sortableTable({
      initialSort: 'name',
      columns: [
        { key: 'name', label: 'Material' },
        { key: 'id', label: 'Id', mono: true },
        { key: 'rarity', label: 'Rarity' },
        { key: 'x', label: materialView.x, numeric: true, digits: 0 },
        { key: 'y', label: materialView.y, numeric: true, digits: 0 },
        { key: 'tags', label: 'Tags' },
      ],
      rows: filtered.slice(0, 400).map((record) => ({
        __open: open(record.id),
        name: record.name, id: record.id,
        rarity: (record.data.tags ?? []).find((tag) => tag.startsWith('rarity:'))?.slice(7) ?? '—',
        x: record.data.properties?.[materialView.x] ?? 0,
        y: record.data.properties?.[materialView.y] ?? 0,
        tags: (record.data.tags ?? []).filter((tag) => !tag.startsWith('rarity:')).join(' '),
      })),
    }));
}

// ── Professions ───────────────────────────────────────────────────────────────────────────

async function renderProfessions(body) {
  const payload = await api.analysisProfessions();
  const professions = payload.professions;
  const actions = payload.actions;

  replaceChildren(body,
    h('div', { class: 'charts-grid', style: { marginBottom: '14px' } },
      chartCard('Estimated hours to level 99 (passive, mastery 0, best action per level)', barChart({
        items: professions.filter((profession) => profession.actionCount > 0)
          .sort((left, right) => right.estimatedHoursTo99Passive - left.estimatedHoursTo99Passive)
          .map((profession) => ({ label: profession.name, value: profession.estimatedHoursTo99Passive })),
        width: 520, maxBars: 20,
      })),
      chartCard('XP/hour progression curves (passive)', lineChart({
        series: professions.filter((profession) => profession.timeline.length > 0).slice(0, 8).map((profession, index) => ({
          name: profession.name,
          color: colorAt(index),
          points: profession.timeline.map((point) => ({ x: point.level, y: point.xpPerHourPassive, note: point.bestActionId })),
        })),
        xLabel: 'level', yLabel: 'XP/h', step: true, width: 520, height: 240,
      }), 'each step = a better action unlocks')),
    h('div', { class: 'section-title' }, `${actions.length} actions — XP and throughput at mastery 0`),
    sortableTable({
      initialSort: 'xpHourPassive',
      columns: [
        { key: 'name', label: 'Action' },
        { key: 'profession', label: 'Profession' },
        { key: 'level', label: 'Lvl', numeric: true, digits: 0 },
        { key: 'interval', label: 'Interval (s)', numeric: true },
        { key: 'xp', label: 'XP', numeric: true, digits: 0 },
        { key: 'success', label: 'Success', numeric: true },
        { key: 'xpHourPassive', label: 'XP/h passive', numeric: true, digits: 0 },
        { key: 'xpHourActive', label: 'XP/h active', numeric: true, digits: 0 },
        { key: 'throughput', label: 'Outputs/h' },
        { key: 'extras', label: 'Extras' },
      ],
      rows: actions.map((action) => ({
        __open: open(action.id),
        name: action.name,
        profession: action.professionId.replace('profession.', ''),
        level: action.requiredLevel, interval: action.intervalSeconds, xp: action.experience,
        success: action.successChance,
        xpHourPassive: action.xpPerHourPassive, xpHourActive: action.xpPerHourActive,
        throughput: Object.entries(action.outputsPerHourPassive).map(([itemId, perHour]) =>
          `${formatNumber(perHour, 0)}× ${(findRecord(itemId)?.name ?? itemId.replace('material.', ''))}`).join(', ') || '—',
        extras: [action.lootTableId ? 'loot' : null, action.opportunityCount ? `${action.opportunityCount} opp` : null].filter(Boolean).join(', ') || '—',
      })),
    }));
}

// ── Loot ──────────────────────────────────────────────────────────────────────────────────

const lootView = { itemId: '' };

async function renderLoot(body) {
  const overview = await api.analysisLootOverview();
  await loadRecords('loot_tables');

  const searchRow = h('div', { style: { maxWidth: '460px' } },
    h('div', { class: 'section-title', style: { marginTop: 0 } }, 'Where does an item come from?'),
    refInput(['materials', 'consumables', 'techniques'], lootView.itemId, async (itemId) => {
      if (!itemId) return;
      lootView.itemId = itemId;
      await renderItemSources(sourcesContainer, itemId);
    }, { placeholder: 'Search an item… (e.g. Storm Core)' }));

  const sourcesContainer = h('div', { style: { marginTop: '10px' } });
  if (lootView.itemId) renderItemSources(sourcesContainer, lootView.itemId);

  replaceChildren(body,
    searchRow,
    sourcesContainer,
    h('div', { class: 'section-title' }, 'Library health'),
    h('div', { class: 'dash-grid' },
      h('div', { class: 'card' },
        h('div', { class: 'card-title' }, `Orphaned tables (${overview.orphanTableIds.length})`),
        overview.orphanTableIds.length === 0
          ? h('div', { style: { color: 'var(--ok)', fontSize: '12px' } }, '✓ Every table is reachable from a real source.')
          : overview.orphanTableIds.map((tableId) => h('div', { class: 'ref-link', onclick: open(tableId) },
              h('span', { class: 'ref-link-name mono' }, tableId)))),
      h('div', { class: 'card' },
        h('div', { class: 'card-title' }, `Tables that pay nothing (${overview.emptyPayoutTableIds.length})`),
        overview.emptyPayoutTableIds.length === 0
          ? h('div', { style: { color: 'var(--ok)', fontSize: '12px' } }, '✓ Every table pays out under some context.')
          : overview.emptyPayoutTableIds.map((tableId) => h('div', { class: 'ref-link', onclick: open(tableId) },
              h('span', { class: 'ref-link-name mono' }, tableId)))),
      h('div', { class: 'card' },
        h('div', { class: 'card-title' }, 'Per-table analysis'),
        h('div', { style: { fontSize: '12px', color: 'var(--fg-dim)', lineHeight: 1.55 } },
          'Open any loot table and use its ', h('b', {}, 'Drop Analysis'), ' tab for expected drops per roll under a chosen depth / activity / rank context.'))));
}

async function renderItemSources(container, itemId) {
  replaceChildren(container, h('div', { class: 'spinner' }));
  const payload = await api.analysisLootItem(itemId, { depth: 2, active: true, rank: 'boss' });
  const sources = payload.sources ?? [];
  replaceChildren(container,
    sources.length === 0
      ? h('div', { class: 'card' }, h('div', { style: { color: 'var(--fg-dim)' } },
          `No loot source pays ${findRecord(itemId)?.name ?? itemId} (checked at depth 2, active, boss). It may only come from profession outputs — check the record's incoming references.`))
      : h('div', { class: 'card' },
          h('div', { class: 'card-title' }, `${findRecord(itemId)?.name ?? itemId} — ${sources.length} source(s), expected per event (depth 2, active, boss-capable)`),
          sortableTable({
            initialSort: 'ev',
            columns: [
              { key: 'source', label: 'Source' },
              { key: 'kind', label: 'Kind' },
              { key: 'ev', label: 'Expected / event', numeric: true, digits: 4 },
              { key: 'per', label: '≈ 1 per', mono: true },
            ],
            rows: sources.map((source) => ({
              __open: source.sourceKind === 'realm-location' ? undefined : open(source.sourceId),
              source: source.sourceName, kind: source.sourceKind,
              ev: source.expectedPerEvent,
              per: `${formatNumber(1 / Math.max(source.expectedPerEvent, 1e-9), 1)} events`,
            })),
          })));
}

// ── Warnings ──────────────────────────────────────────────────────────────────────────────

async function renderWarnings(body) {
  const warnings = await api.analysisWarnings();
  const byArea = new Map();
  for (const warning of warnings) {
    if (!byArea.has(warning.area)) byArea.set(warning.area, []);
    byArea.get(warning.area).push(warning);
  }
  replaceChildren(body,
    h('div', { class: 'section-title', style: { marginTop: 0 } },
      `${warnings.length} non-destructive finding(s). These are prompts for a designer, never automatic changes.`),
    warnings.length === 0 ? h('div', { class: 'empty-state' }, h('div', { class: 'big' }, '✓'), h('div', {}, 'Nothing statistically suspicious right now.')) : null,
    [...byArea.entries()].map(([area, areaWarnings]) => h('div', { style: { marginBottom: '16px' } },
      h('div', { class: 'section-title' }, `${area} (${areaWarnings.length})`),
      areaWarnings.map((warning) => h('div', {
        class: 'warning-row',
        onclick: () => { if (warning.recordId) location.hash = `#/record/${encodeURIComponent(warning.recordId)}`; },
      }, warning.message)))));
}
