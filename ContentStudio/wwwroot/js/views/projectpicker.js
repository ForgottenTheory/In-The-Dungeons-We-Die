// Project selection: a server-backed directory browser (the browser sandbox cannot walk
// the filesystem itself) plus a paste-a-path shortcut. Directories containing game/data
// light up as openable projects.

import { api } from '../api.js';
import { h, replaceChildren, toast, openModal } from '../ui.js';

export function showProjectPicker({ currentRoot, onOpened, dismissable = true }) {
  const pathInput = h('input', {
    type: 'text', value: currentRoot ?? '', placeholder: 'C:\\Projects\\MyGame',
    style: { width: '100%', fontFamily: 'var(--mono)', fontSize: '12px' },
  });
  const listing = h('div', { style: { maxHeight: '320px', overflow: 'auto', border: '1px solid var(--border)', borderRadius: '6px', marginTop: '8px' } });
  const errorLine = h('div', { style: { color: 'var(--err)', fontSize: '11.5px', minHeight: '16px', marginTop: '6px' } });

  const browse = async (path) => {
    try {
      const payload = await api.browse(path);
      if (payload.path) pathInput.value = payload.path;
      const rows = [];
      if (payload.parent !== null && payload.parent !== undefined) {
        rows.push(h('div', { class: 'ref-option', onclick: () => browse(payload.parent) },
          h('span', { class: 'ref-name' }, '↰ ..')));
      }
      for (const directory of payload.directories) {
        rows.push(h('div', {
          class: 'ref-option',
          onclick: () => directory.isProject ? tryOpen(directory.path) : browse(directory.path),
        },
          h('span', { class: 'ref-name' }, directory.isProject ? '🎮 ' : '📁 ', directory.name),
          directory.isProject ? h('span', { class: 'ref-type' }, 'game project — click to open') : null));
      }
      if (payload.isProject) {
        rows.unshift(h('div', { class: 'ref-option', style: { background: 'var(--accent-soft)' }, onclick: () => tryOpen(payload.path) },
          h('span', { class: 'ref-name' }, `✓ Open this project: ${payload.path}`)));
      }
      replaceChildren(listing, rows.length ? rows : h('div', { style: { padding: '10px', color: 'var(--fg-faint)' } }, 'empty'));
      errorLine.textContent = '';
    } catch (error) {
      errorLine.textContent = error.message;
    }
  };

  const tryOpen = async (root) => {
    try {
      await api.openProject(root);
      toast('Project opened', root, 'ok');
      close();
      onOpened?.();
    } catch (error) {
      errorLine.textContent = error.message;
    }
  };

  const { close } = openModal({
    title: 'Game Project',
    body: h('div', {},
      h('p', { style: { color: 'var(--fg-dim)', marginTop: 0 } },
        'Pick the root folder of the game project — the one containing ', h('code', {}, 'game/data'), '. Content Studio edits those files directly and remembers the choice.'),
      h('div', { style: { display: 'flex', gap: '6px' } },
        pathInput,
        h('button', { class: 'button compact', onclick: () => browse(pathInput.value) }, 'Browse'),
        h('button', { class: 'button primary compact', onclick: () => tryOpen(pathInput.value) }, 'Open')),
      errorLine,
      listing),
    actions: dismissable ? [{ label: 'Cancel' }] : [],
  });

  browse(currentRoot ?? '');
}
