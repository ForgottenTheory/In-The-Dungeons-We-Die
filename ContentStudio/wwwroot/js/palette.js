// Ctrl+K command palette: fuzzy search across every record, type and tool.

import { h, replaceChildren } from './ui.js';
import { state, allRecords, typeOf } from './state.js';

let openPalette = null;

export function togglePalette() {
  if (openPalette) { closePalette(); return; }
  showPalette();
}

function closePalette() {
  openPalette?.remove();
  openPalette = null;
  document.removeEventListener('keydown', onGlobalKey, true);
}

function onGlobalKey(event) {
  if (event.key === 'Escape') { event.stopPropagation(); closePalette(); }
}

/** Simple subsequence-with-bonus fuzzy score; 0 = no match. */
function fuzzyScore(query, target) {
  if (!query) return 1;
  const lowerTarget = target.toLowerCase();
  const index = lowerTarget.indexOf(query);
  if (index >= 0) return 1000 - index - (lowerTarget.length - query.length) * 0.05;
  let queryIndex = 0;
  let score = 0;
  let streak = 0;
  for (let targetIndex = 0; targetIndex < lowerTarget.length && queryIndex < query.length; targetIndex++) {
    if (lowerTarget[targetIndex] === query[queryIndex]) {
      queryIndex++;
      streak++;
      score += 2 + streak;
    } else streak = 0;
  }
  return queryIndex === query.length ? score : 0;
}

function buildCandidates() {
  const candidates = [];
  for (const record of allRecords()) {
    candidates.push({
      kind: 'record',
      label: record.name ?? record.id,
      detail: record.id,
      type: typeOf(record.typeId)?.singularName ?? record.typeId,
      status: record.errors > 0 ? 'err' : record.warnings > 0 ? 'warn' : '',
      go: () => { location.hash = `#/record/${encodeURIComponent(record.id)}`; },
      haystack: `${record.name ?? ''} ${record.id} ${(record.data?.tags ?? []).join(' ')}`,
    });
  }
  for (const type of state.meta?.types ?? []) {
    candidates.push({
      kind: 'tool', label: type.displayName, detail: `${type.recordCount} records`, type: 'Browse',
      go: () => { location.hash = `#/type/${type.typeId}`; },
      haystack: `${type.displayName} ${type.typeId}`,
    });
  }
  const tools = [
    ['Dashboard', '#/dashboard'], ['Validation Center', '#/validation'], ['Files & Backups', '#/files'],
    ['Balance · Enemies', '#/balance/enemies'], ['Balance · Moves', '#/balance/moves'],
    ['Balance · Materials', '#/balance/materials'], ['Balance · Professions', '#/balance/professions'],
    ['Balance · Loot', '#/balance/loot'], ['Balance · Warnings', '#/balance/warnings'],
    ['Dependency Explorer', '#/deps'],
  ];
  for (const [label, hash] of tools) {
    candidates.push({ kind: 'tool', label, detail: '', type: 'Tool', go: () => { location.hash = hash; }, haystack: label });
  }
  return candidates;
}

function showPalette() {
  const input = h('input', { type: 'text', placeholder: 'Search records, types and tools… (e.g. "goblin brute", "move freeze", "loot")' });
  const results = h('div', { class: 'palette-results' });
  const modal = h('div', { class: 'modal palette' }, input, results);
  const backdrop = h('div', { class: 'modal-backdrop', onmousedown: (event) => { if (event.target === backdrop) closePalette(); } }, modal);
  document.getElementById('overlay-root').append(backdrop);
  openPalette = backdrop;
  document.addEventListener('keydown', onGlobalKey, true);

  const candidates = buildCandidates();
  let focused = 0;
  let shown = [];

  const rerender = () => {
    const query = input.value.trim().toLowerCase();
    shown = candidates
      .map((candidate) => ({ candidate, score: fuzzyScore(query, candidate.haystack) }))
      .filter((entry) => entry.score > 0)
      .sort((left, right) => right.score - left.score || left.candidate.label.localeCompare(right.candidate.label))
      .slice(0, 40)
      .map((entry) => entry.candidate);
    focused = Math.min(focused, Math.max(0, shown.length - 1));
    replaceChildren(results,
      shown.length === 0
        ? h('div', { class: 'palette-empty' }, 'No matches.')
        : shown.map((candidate, index) => h('div', {
            class: `palette-row${index === focused ? ' focused' : ''}`,
            onclick: () => { closePalette(); candidate.go(); },
            onmousemove: () => { if (focused !== index) { focused = index; rerender(); } },
          },
            h('span', { class: `p-type${candidate.kind === 'tool' ? ' tool' : ''}` }, candidate.type),
            h('span', { class: 'p-name' },
              candidate.status ? h('span', { class: `status-glyph ${candidate.status}` }, candidate.status === 'err' ? '✕ ' : '⚠ ') : null,
              candidate.label),
            h('span', { class: 'p-id' }, candidate.detail))));
  };

  input.addEventListener('input', () => { focused = 0; rerender(); });
  input.addEventListener('keydown', (event) => {
    if (event.key === 'ArrowDown') { event.preventDefault(); focused = Math.min(shown.length - 1, focused + 1); rerender(); }
    else if (event.key === 'ArrowUp') { event.preventDefault(); focused = Math.max(0, focused - 1); rerender(); }
    else if (event.key === 'Enter' && shown[focused]) { closePalette(); shown[focused].go(); }
  });

  rerender();
  setTimeout(() => input.focus(), 20);
}
