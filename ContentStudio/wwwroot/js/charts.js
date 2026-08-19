// A small hand-rolled SVG chart library: bars, histograms, scatter plots, heatmaps and
// line charts, with hover tooltips and click-through. No external dependencies — the tool
// must run fully offline.

import { h, formatNumber } from './ui.js';

const NS = 'http://www.w3.org/2000/svg';

function svgElement(tag, attributes = {}, ...children) {
  const element = document.createElementNS(NS, tag);
  for (const [key, value] of Object.entries(attributes)) {
    if (value === null || value === undefined) continue;
    if (key.startsWith('on') && typeof value === 'function') element.addEventListener(key.slice(2), value);
    else element.setAttribute(key, value);
  }
  for (const child of children.flat(Infinity)) if (child) element.append(child);
  return element;
}

let tooltip = null;

function showTooltip(event, title, lines) {
  hideTooltip();
  tooltip = h('div', { class: 'chart-tooltip' },
    h('div', { class: 'tt-title' }, title),
    lines.map((line) => h('div', { class: 'tt-line' }, line)));
  document.body.append(tooltip);
  moveTooltip(event);
}

function moveTooltip(event) {
  if (!tooltip) return;
  const rect = tooltip.getBoundingClientRect();
  tooltip.style.left = `${Math.min(event.clientX + 14, window.innerWidth - rect.width - 10)}px`;
  tooltip.style.top = `${Math.min(event.clientY + 12, window.innerHeight - rect.height - 10)}px`;
}

function hideTooltip() {
  tooltip?.remove();
  tooltip = null;
}

function niceTicks(min, max, count = 5) {
  if (min === max) { max = min + 1; }
  const span = max - min;
  const step = Math.pow(10, Math.floor(Math.log10(span / count)));
  const candidates = [step, step * 2, step * 2.5, step * 5, step * 10];
  const chosen = candidates.find((candidate) => span / candidate <= count) ?? step * 10;
  const ticks = [];
  for (let tick = Math.ceil(min / chosen) * chosen; tick <= max + 1e-9; tick += chosen)
    ticks.push(Number(tick.toFixed(10)));
  return ticks;
}

const PALETTE = ['#7aa2ff', '#57d98a', '#f4bf4f', '#f47272', '#4fc3f7', '#c792ea', '#f78c6c', '#89ddff'];

export function colorAt(index) { return PALETTE[index % PALETTE.length]; }

/** Horizontal bar chart. items: [{label, value, color?, onClick?, tooltip?}] */
export function barChart({ items, width = 460, valueLabel = '', maxBars = 40 }) {
  const shown = items.slice(0, maxBars);
  const rowHeight = 20;
  const labelWidth = 150;
  const height = shown.length * rowHeight + 8;
  const maxValue = Math.max(1e-9, ...shown.map((item) => item.value));
  const svg = svgElement('svg', { class: 'chart-svg', viewBox: `0 0 ${width} ${height}`, height });

  shown.forEach((item, index) => {
    const y = index * rowHeight + 4;
    const barWidth = Math.max(1, (item.value / maxValue) * (width - labelWidth - 60));
    const group = svgElement('g', {
      class: 'mark',
      onclick: () => item.onClick?.(),
      onmousemove: (event) => { showTooltip(event, item.label, item.tooltip ?? [`${valueLabel}${formatNumber(item.value)}`]); moveTooltip(event); },
      onmouseleave: hideTooltip,
    },
      svgElement('text', { x: labelWidth - 6, y: y + 12, 'text-anchor': 'end' }, truncate(item.label, 24)),
      svgElement('rect', { x: labelWidth, y, width: barWidth, height: rowHeight - 6, rx: 2, fill: item.color ?? PALETTE[0] }),
      svgElement('text', { x: labelWidth + barWidth + 5, y: y + 12 }, formatNumber(item.value)));
    svg.append(group);
  });
  if (items.length > maxBars)
    svg.append(svgElement('text', { x: labelWidth, y: height - 2 }, `…and ${items.length - maxBars} more`));
  return svg;
}

/** Histogram of numeric values. */
export function histogram({ values, width = 440, height = 160, buckets = 20, min = null, max = null, color = PALETTE[0], format = (v) => formatNumber(v) }) {
  const finite = values.filter(Number.isFinite);
  if (!finite.length) return h('div', { class: 'empty-state' }, 'no data');
  const lo = min ?? Math.min(...finite);
  const hi = max ?? Math.max(...finite);
  const span = hi - lo || 1;
  const counts = new Array(buckets).fill(0);
  for (const value of finite) {
    const bucket = Math.min(buckets - 1, Math.floor(((value - lo) / span) * buckets));
    counts[bucket]++;
  }
  const maxCount = Math.max(...counts, 1);
  const pad = { left: 30, bottom: 18, top: 6, right: 6 };
  const plotWidth = width - pad.left - pad.right;
  const plotHeight = height - pad.top - pad.bottom;
  const svg = svgElement('svg', { class: 'chart-svg', viewBox: `0 0 ${width} ${height}`, height });

  counts.forEach((count, index) => {
    const barHeight = (count / maxCount) * plotHeight;
    const bucketLo = lo + (index / buckets) * span;
    const bucketHi = lo + ((index + 1) / buckets) * span;
    svg.append(svgElement('rect', {
      class: 'mark',
      x: pad.left + (index / buckets) * plotWidth + 1,
      y: pad.top + plotHeight - barHeight,
      width: plotWidth / buckets - 2,
      height: Math.max(barHeight, count > 0 ? 2 : 0),
      fill: color, rx: 1,
      onmousemove: (event) => { showTooltip(event, `${format(bucketLo)} – ${format(bucketHi)}`, [`${count} record(s)`]); moveTooltip(event); },
      onmouseleave: hideTooltip,
    }));
  });
  svg.append(svgElement('line', { class: 'axis-line', x1: pad.left, y1: pad.top + plotHeight, x2: width - pad.right, y2: pad.top + plotHeight }));
  svg.append(svgElement('text', { x: pad.left, y: height - 4 }, format(lo)));
  svg.append(svgElement('text', { x: width - pad.right, y: height - 4, 'text-anchor': 'end' }, format(hi)));
  svg.append(svgElement('text', { x: 4, y: pad.top + 10 }, String(maxCount)));
  return svg;
}

/** Scatter plot. points: [{x, y, label, color?, onClick?, size?}] */
export function scatterPlot({ points, width = 440, height = 260, xLabel = '', yLabel = '', color = PALETTE[0] }) {
  const finite = points.filter((point) => Number.isFinite(point.x) && Number.isFinite(point.y));
  if (!finite.length) return h('div', { class: 'empty-state' }, 'no data');
  const pad = { left: 42, bottom: 26, top: 8, right: 10 };
  const xs = finite.map((point) => point.x);
  const ys = finite.map((point) => point.y);
  let [xLo, xHi] = [Math.min(...xs), Math.max(...xs)];
  let [yLo, yHi] = [Math.min(...ys), Math.max(...ys)];
  if (xLo === xHi) { xLo -= 1; xHi += 1; }
  if (yLo === yHi) { yLo -= 1; yHi += 1; }
  const plotWidth = width - pad.left - pad.right;
  const plotHeight = height - pad.top - pad.bottom;
  const toX = (x) => pad.left + ((x - xLo) / (xHi - xLo)) * plotWidth;
  const toY = (y) => pad.top + plotHeight - ((y - yLo) / (yHi - yLo)) * plotHeight;

  const svg = svgElement('svg', { class: 'chart-svg', viewBox: `0 0 ${width} ${height}`, height });
  for (const tick of niceTicks(yLo, yHi, 4)) {
    svg.append(svgElement('line', { class: 'grid-line', x1: pad.left, y1: toY(tick), x2: width - pad.right, y2: toY(tick) }));
    svg.append(svgElement('text', { x: pad.left - 5, y: toY(tick) + 3, 'text-anchor': 'end' }, formatNumber(tick, 1)));
  }
  for (const tick of niceTicks(xLo, xHi, 5)) {
    svg.append(svgElement('text', { x: toX(tick), y: height - 10, 'text-anchor': 'middle' }, formatNumber(tick, 1)));
  }
  svg.append(svgElement('line', { class: 'axis-line', x1: pad.left, y1: pad.top + plotHeight, x2: width - pad.right, y2: pad.top + plotHeight }));
  svg.append(svgElement('line', { class: 'axis-line', x1: pad.left, y1: pad.top, x2: pad.left, y2: pad.top + plotHeight }));
  if (xLabel) svg.append(svgElement('text', { x: width - pad.right, y: height - 10, 'text-anchor': 'end', 'font-weight': 600 }, xLabel));
  if (yLabel) svg.append(svgElement('text', { x: pad.left + 4, y: pad.top + 8, 'font-weight': 600 }, yLabel));

  for (const point of finite) {
    svg.append(svgElement('circle', {
      class: 'mark',
      cx: toX(point.x), cy: toY(point.y), r: point.size ?? 4,
      fill: point.color ?? color, 'fill-opacity': 0.82,
      onclick: () => point.onClick?.(),
      onmousemove: (event) => { showTooltip(event, point.label, [`${xLabel || 'x'}: ${formatNumber(point.x)}`, `${yLabel || 'y'}: ${formatNumber(point.y)}`]); moveTooltip(event); },
      onmouseleave: hideTooltip,
    }));
  }
  return svg;
}

/** Heatmap. rows × columns of numbers; color scale diverging around zero when signed. */
export function heatmap({ rowLabels, columnLabels, values, width = 560, onCellClick, format = (v) => formatNumber(v, 2), rowLabelWidth = 130 }) {
  const cellHeight = 20;
  const headerHeight = 60;
  const cellWidth = Math.max(34, (width - rowLabelWidth) / Math.max(1, columnLabels.length));
  const height = headerHeight + rowLabels.length * cellHeight + 4;
  const flat = values.flat().filter(Number.isFinite);
  const maxAbs = Math.max(1e-9, ...flat.map(Math.abs));
  const anyNegative = flat.some((value) => value < 0);

  const colorFor = (value) => {
    if (!Number.isFinite(value) || value === 0) return 'rgba(255,255,255,0.03)';
    const intensity = Math.min(1, Math.abs(value) / maxAbs);
    if (anyNegative)
      return value > 0 ? `rgba(87,217,138,${0.12 + intensity * 0.65})` : `rgba(244,114,114,${0.12 + intensity * 0.65})`;
    return `rgba(122,162,255,${0.10 + intensity * 0.7})`;
  };

  const svg = svgElement('svg', { class: 'chart-svg', viewBox: `0 0 ${width} ${height}`, height });
  columnLabels.forEach((label, columnIndex) => {
    svg.append(svgElement('text', {
      x: rowLabelWidth + columnIndex * cellWidth + cellWidth / 2,
      y: headerHeight - 8,
      transform: `rotate(-38 ${rowLabelWidth + columnIndex * cellWidth + cellWidth / 2} ${headerHeight - 8})`,
    }, truncate(label, 14)));
  });
  rowLabels.forEach((rowLabel, rowIndex) => {
    const y = headerHeight + rowIndex * cellHeight;
    svg.append(svgElement('text', { x: rowLabelWidth - 6, y: y + 14, 'text-anchor': 'end' }, truncate(rowLabel, 20)));
    columnLabels.forEach((columnLabel, columnIndex) => {
      const value = values[rowIndex]?.[columnIndex];
      svg.append(svgElement('rect', {
        class: 'mark',
        x: rowLabelWidth + columnIndex * cellWidth + 1, y: y + 1,
        width: cellWidth - 2, height: cellHeight - 2, rx: 2,
        fill: colorFor(value),
        onclick: () => onCellClick?.(rowIndex, columnIndex),
        onmousemove: (event) => { showTooltip(event, `${rowLabel} · ${columnLabel}`, [Number.isFinite(value) ? format(value) : '—']); moveTooltip(event); },
        onmouseleave: hideTooltip,
      }));
      if (Number.isFinite(value) && value !== 0 && cellWidth > 40) {
        svg.append(svgElement('text', {
          x: rowLabelWidth + columnIndex * cellWidth + cellWidth / 2, y: y + 14,
          'text-anchor': 'middle', 'pointer-events': 'none',
        }, format(value)));
      }
    });
  });
  return svg;
}

/** Step/line chart. series: [{name, points: [{x, y}], color?}] */
export function lineChart({ series, width = 460, height = 200, xLabel = '', yLabel = '', step = false }) {
  const allPoints = series.flatMap((entry) => entry.points);
  if (!allPoints.length) return h('div', { class: 'empty-state' }, 'no data');
  const pad = { left: 46, bottom: 22, top: 8, right: 10 };
  let [xLo, xHi] = [Math.min(...allPoints.map((point) => point.x)), Math.max(...allPoints.map((point) => point.x))];
  let [yLo, yHi] = [0, Math.max(...allPoints.map((point) => point.y))];
  if (xLo === xHi) xHi = xLo + 1;
  if (yHi === 0) yHi = 1;
  const plotWidth = width - pad.left - pad.right;
  const plotHeight = height - pad.top - pad.bottom;
  const toX = (x) => pad.left + ((x - xLo) / (xHi - xLo)) * plotWidth;
  const toY = (y) => pad.top + plotHeight - ((y - yLo) / (yHi - yLo)) * plotHeight;

  const svg = svgElement('svg', { class: 'chart-svg', viewBox: `0 0 ${width} ${height}`, height });
  for (const tick of niceTicks(yLo, yHi, 4)) {
    svg.append(svgElement('line', { class: 'grid-line', x1: pad.left, y1: toY(tick), x2: width - pad.right, y2: toY(tick) }));
    svg.append(svgElement('text', { x: pad.left - 5, y: toY(tick) + 3, 'text-anchor': 'end' }, formatNumber(tick, 0)));
  }
  series.forEach((entry, index) => {
    const color = entry.color ?? PALETTE[index % PALETTE.length];
    const sorted = [...entry.points].sort((left, right) => left.x - right.x);
    let path = '';
    sorted.forEach((point, pointIndex) => {
      const x = toX(point.x);
      const y = toY(point.y);
      if (pointIndex === 0) path += `M ${x} ${y}`;
      else if (step) path += ` H ${x} V ${y}`;
      else path += ` L ${x} ${y}`;
    });
    svg.append(svgElement('path', { d: path, fill: 'none', stroke: color, 'stroke-width': 1.8 }));
    for (const point of sorted) {
      svg.append(svgElement('circle', {
        class: 'mark', cx: toX(point.x), cy: toY(point.y), r: 3, fill: color,
        onmousemove: (event) => { showTooltip(event, entry.name, [`${xLabel || 'x'}: ${formatNumber(point.x)}`, `${yLabel || 'y'}: ${formatNumber(point.y)}`, point.note ?? '']); moveTooltip(event); },
        onmouseleave: hideTooltip,
      }));
    }
  });
  svg.append(svgElement('line', { class: 'axis-line', x1: pad.left, y1: pad.top + plotHeight, x2: width - pad.right, y2: pad.top + plotHeight }));
  for (const tick of niceTicks(xLo, xHi, 6)) {
    svg.append(svgElement('text', { x: toX(tick), y: height - 8, 'text-anchor': 'middle' }, formatNumber(tick, 0)));
  }
  return svg;
}

export function chartCard(title, chart, subtitle = '') {
  return h('div', { class: 'chart-card' },
    h('div', { class: 'chart-title' }, title),
    subtitle ? h('div', { style: { fontSize: '10.5px', color: 'var(--fg-faint)', marginBottom: '6px' } }, subtitle) : null,
    chart);
}

function truncate(text, max) {
  text = String(text);
  return text.length > max ? `${text.slice(0, max - 1)}…` : text;
}
