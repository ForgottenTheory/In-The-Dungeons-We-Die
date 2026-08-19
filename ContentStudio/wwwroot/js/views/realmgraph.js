// Realm graph view: locations laid out by depth, edges drawn, node details on click.
// Read-only visualization — the authoritative editing surface is the record's Form tab.

import { api } from '../api.js';
import { h, replaceChildren } from '../ui.js';

const NS = 'http://www.w3.org/2000/svg';

const TYPE_COLORS = {
  Entrance: '#57d98a', Extraction: '#57d98a',
  Combat: '#f47272', Gather: '#7aa2ff', Event: '#c792ea',
  Descent: '#f4bf4f', Travel: '#8b93a5', Camp: '#4fc3f7',
  Shrine: '#c792ea', Merchant: '#f4bf4f', Hazard: '#f78c6c',
};

export async function renderRealmGraphPanel(container, realmId) {
  container.append(h('div', { class: 'spinner' }));
  let realm;
  try {
    realm = await api.analysisRealm(realmId);
  } catch (error) {
    replaceChildren(container, h('div', { class: 'empty-state' }, h('div', {}, error.message)));
    return;
  }
  if (realm.error) {
    replaceChildren(container, h('div', { class: 'empty-state' }, h('div', {}, realm.error)));
    return;
  }

  const locations = realm.locations;
  const depths = [...new Set(locations.map((location) => location.depth))].sort((a, b) => a - b);
  const columnWidth = 190;
  const rowHeight = 88;
  const maxRows = Math.max(...depths.map((depth) => locations.filter((location) => location.depth === depth).length));
  const width = depths.length * columnWidth + 60;
  const height = maxRows * rowHeight + 70;

  const positions = new Map();
  depths.forEach((depth, columnIndex) => {
    const atDepth = locations.filter((location) => location.depth === depth);
    atDepth.forEach((location, rowIndex) => {
      positions.set(location.id, {
        x: 60 + columnIndex * columnWidth + columnWidth / 2,
        y: 50 + rowIndex * rowHeight + ((columnIndex % 2) * 22),
      });
    });
  });

  const svg = document.createElementNS(NS, 'svg');
  svg.setAttribute('class', 'chart-svg');
  svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
  svg.setAttribute('height', Math.min(height, 640));

  depths.forEach((depth, columnIndex) => {
    const text = document.createElementNS(NS, 'text');
    text.setAttribute('x', 60 + columnIndex * columnWidth + columnWidth / 2);
    text.setAttribute('y', 18);
    text.setAttribute('text-anchor', 'middle');
    text.setAttribute('font-weight', '700');
    text.textContent = `DEPTH ${depth}`;
    svg.append(text);
  });

  const drawnEdges = new Set();
  for (const location of locations) {
    for (const connection of location.connections ?? []) {
      const edgeKey = [location.id, connection].sort().join('→');
      if (drawnEdges.has(edgeKey)) continue;
      drawnEdges.add(edgeKey);
      const from = positions.get(location.id);
      const to = positions.get(connection);
      if (!from || !to) continue;
      const path = document.createElementNS(NS, 'path');
      const midX = (from.x + to.x) / 2;
      path.setAttribute('d', `M ${from.x} ${from.y} C ${midX} ${from.y}, ${midX} ${to.y}, ${to.x} ${to.y}`);
      path.setAttribute('fill', 'none');
      path.setAttribute('stroke', 'var(--border-strong)');
      path.setAttribute('stroke-width', '1.4');
      svg.append(path);
    }
  }

  const details = h('div', { class: 'card', style: { marginTop: '10px', minHeight: '90px' } },
    h('div', { class: 'card-title' }, 'Location'),
    h('div', { style: { color: 'var(--fg-faint)', fontSize: '12px' } }, 'Click a node.'));

  for (const location of locations) {
    const position = positions.get(location.id);
    const group = document.createElementNS(NS, 'g');
    group.setAttribute('class', 'realm-node');
    const circle = document.createElementNS(NS, 'circle');
    circle.setAttribute('cx', position.x);
    circle.setAttribute('cy', position.y);
    circle.setAttribute('r', 15);
    circle.setAttribute('fill', TYPE_COLORS[location.type] ?? '#8b93a5');
    circle.setAttribute('fill-opacity', location.hidden ? '0.35' : '0.85');
    circle.setAttribute('stroke', 'var(--bg-app)');
    circle.setAttribute('stroke-width', '2');
    if (location.hidden) circle.setAttribute('stroke-dasharray', '3 2');
    const label = document.createElementNS(NS, 'text');
    label.setAttribute('x', position.x);
    label.setAttribute('y', position.y + 30);
    label.setAttribute('text-anchor', 'middle');
    label.textContent = location.name.length > 22 ? `${location.name.slice(0, 21)}…` : location.name;
    const typeLabel = document.createElementNS(NS, 'text');
    typeLabel.setAttribute('x', position.x);
    typeLabel.setAttribute('y', position.y + 4);
    typeLabel.setAttribute('text-anchor', 'middle');
    typeLabel.setAttribute('font-size', '8');
    typeLabel.setAttribute('fill', '#0e1013');
    typeLabel.setAttribute('font-weight', '700');
    typeLabel.setAttribute('pointer-events', 'none');
    typeLabel.textContent = location.type.slice(0, 4).toUpperCase();
    group.append(circle, typeLabel, label);
    group.addEventListener('click', () => {
      replaceChildren(details,
        h('div', { class: 'card-title' }, location.name),
        h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Id'), h('span', { class: 'v mono' }, location.id)),
        h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Type'), h('span', { class: 'v' }, `${location.type} · depth ${location.depth}${location.hidden ? ' · hidden' : ''}`)),
        h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Links'), h('span', { class: 'v mono' }, (location.connections ?? []).join(', ') || '—')),
        location.actorId ? h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Enemy'),
          h('a', { class: 'plain-link mono v', onclick: () => { window.location.hash = `#/record/${encodeURIComponent(location.actorId)}`; } }, location.actorId)) : null,
        location.professionActionId ? h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Action'),
          h('a', { class: 'plain-link mono v', onclick: () => { window.location.hash = `#/record/${encodeURIComponent(location.professionActionId)}`; } }, location.professionActionId)) : null,
        location.lootTableId ? h('div', { class: 'inspector-row' }, h('span', { class: 'k' }, 'Loot'),
          h('a', { class: 'plain-link mono v', onclick: () => { window.location.hash = `#/record/${encodeURIComponent(location.lootTableId)}`; } }, location.lootTableId)) : null);
    });
    svg.append(group);
  }

  const legend = h('div', { style: { display: 'flex', gap: '12px', flexWrap: 'wrap', fontSize: '11px', color: 'var(--fg-dim)', margin: '6px 0' } },
    Object.entries(TYPE_COLORS).map(([type, color]) => h('span', { style: { display: 'inline-flex', alignItems: 'center', gap: '4px' } },
      h('span', { style: { width: '9px', height: '9px', borderRadius: '50%', background: color, display: 'inline-block' } }), type)));

  replaceChildren(container,
    h('div', { style: { fontSize: '12px', color: 'var(--fg-dim)', marginBottom: '4px' } },
      `${locations.length} locations across ${depths.length} depth(s). Dashed = hidden until revealed. Edges are validated symmetric.`),
    legend,
    h('div', { style: { overflow: 'auto', border: '1px solid var(--border)', borderRadius: '8px', background: 'var(--bg-panel)' } }, svg),
    details);
}
