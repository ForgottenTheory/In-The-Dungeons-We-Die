// Schema-driven field renderers. Each renderer binds one JSON field to a control; commits
// mutate the editor's working copy and call ctx.onChange(). Unknown shapes always fall back
// to a raw JSON sub-editor, so nothing in a record is ever invisible or uneditable.

import { h, replaceChildren } from './ui.js';
import { vocabList, dictKeys, allRecords, findRecord, typeOf } from './state.js';

const LONG_TEXT_FIELDS = new Set([
  'description', 'prompt', 'gloss', 'fantasy', 'mechanic', 'drawback', 'eventText',
  'clause', 'example', 'weakness', 'custom_phrase',
]);

export function renderFields(schemaFields, container, ctx) {
  for (const field of schemaFields) container.append(renderFieldRow(field, ctx));
}

export function renderFieldRow(field, ctx) {
  const control = renderControl(field, ctx);
  const hasProblem = ctx.problemFields?.has(field.name);
  const label = h('label', { title: field.name },
    field.label,
    field.help ? h('span', { class: 'help-icon', title: field.help }, 'ⓘ') : null);
  return h('div', { class: `field-row${hasProblem ? ' problem-field' : ''}`, dataset: { field: field.name } },
    label,
    h('div', { class: 'field-control' }, control,
      field.help && field.kind !== 'string' && field.kind !== 'ref' ? null : null));
}

export function renderControl(field, ctx) {
  switch (field.kind) {
    case 'string': return stringControl(field, ctx);
    case 'int':
    case 'number': return numberControl(field, ctx);
    case 'bool': return boolControl(field, ctx);
    case 'enum': return enumControl(field, ctx);
    case 'ref': return refControl(field, ctx);
    case 'refList': return refListControl(field, ctx);
    case 'tags': return chipsControl(field, ctx, tagSuggestions(field, ctx));
    case 'stringList': return chipsControl(field, ctx, field.enumSourceName ? vocabList(field.enumSourceName) : (field.enumValues ?? []));
    case 'numberList': return chipsControl(field, ctx, [], { numeric: true });
    case 'numberDict': return numberDictControl(field, ctx);
    case 'object': return objectControl(field, ctx);
    case 'objectList': return objectListControl(field, ctx);
    case 'objectDict': return objectDictControl(field, ctx);
    case 'objectListDict': return objectListDictControl(field, ctx);
    case 'moveGrant': return moveGrantControl(field, ctx, ctx.get(field.name), (value) => setOrDelete(ctx, field, value));
    case 'moveGrantList': return moveGrantListControl(field, ctx);
    default: return jsonControl(field, ctx);
  }
}

function setOrDelete(ctx, field, value) {
  if (value === undefined || value === null || value === '') ctx.remove(field.name);
  else ctx.set(field.name, value);
  ctx.onChange();
}

// ── Scalars ───────────────────────────────────────────────────────────────────────────────

function stringControl(field, ctx) {
  const value = ctx.get(field.name);
  if (LONG_TEXT_FIELDS.has(field.name)) {
    const area = h('textarea', { rows: 2, value: value ?? '', placeholder: field.optional ? '(unset)' : '' });
    area.addEventListener('change', () => setOrDelete(ctx, field, area.value === '' ? undefined : area.value));
    return area;
  }
  const suggestions = field.enumSourceName ? vocabList(field.enumSourceName) : field.enumValues;
  const input = h('input', { type: 'text', value: value ?? '', placeholder: field.optional ? '(unset)' : '' });
  let wrapped = input;
  if (suggestions?.length) {
    const listId = `dl-${field.name}-${Math.random().toString(36).slice(2, 8)}`;
    input.setAttribute('list', listId);
    wrapped = h('span', {}, input, h('datalist', { id: listId }, suggestions.map((option) => h('option', { value: option }))));
  }
  if (field.name === 'id') {
    input.classList.add('mono');
    input.addEventListener('change', () => ctx.onIdEdited ? ctx.onIdEdited(input) : setOrDelete(ctx, field, input.value));
    return wrapped;
  }
  input.addEventListener('change', () => setOrDelete(ctx, field, input.value === '' && field.optional ? undefined : input.value));
  return wrapped;
}

function numberControl(field, ctx) {
  const value = ctx.get(field.name);
  const input = h('input', {
    type: 'number',
    value: value ?? '',
    placeholder: field.optional ? 'unset' : '',
    min: field.min ?? null,
    max: field.max ?? null,
    step: field.step ?? (field.kind === 'int' ? 1 : 'any'),
  });
  input.addEventListener('change', () => {
    if (input.value === '') { setOrDelete(ctx, field, undefined); return; }
    const parsed = Number(input.value);
    if (Number.isNaN(parsed)) { input.classList.add('invalid'); return; }
    input.classList.remove('invalid');
    setOrDelete(ctx, field, field.kind === 'int' ? Math.round(parsed) : parsed);
  });
  if (field.min !== null && field.min !== undefined && field.max !== null && field.max !== undefined) {
    return h('div', { class: 'field-inline' }, input,
      h('span', { style: { fontSize: '10.5px', color: 'var(--fg-faint)', whiteSpace: 'nowrap' } }, `${field.min}–${field.max}`));
  }
  return input;
}

function boolControl(field, ctx) {
  const input = h('input', { type: 'checkbox', checked: !!ctx.get(field.name) });
  input.addEventListener('change', () => {
    // false is the default everywhere in this codebase; keep files clean by removing it.
    setOrDelete(ctx, field, input.checked ? true : undefined);
  });
  return input;
}

function enumControl(field, ctx) {
  const values = field.enumSourceName ? vocabList(field.enumSourceName) : (field.enumValues ?? []);
  const current = ctx.get(field.name);
  const select = h('select', {},
    h('option', { value: '' }, field.optional ? '(unset)' : '(default)'),
    values.map((option) => h('option', { value: option, selected: option === current }, option)));
  if (current && !values.includes(current))
    select.append(h('option', { value: current, selected: true }, `${current} (unknown)`));
  select.addEventListener('change', () => setOrDelete(ctx, field, select.value === '' ? undefined : select.value));
  return select;
}

// ── Reference pickers ─────────────────────────────────────────────────────────────────────

export function refInput(refTypes, value, onCommit, { placeholder = '' } = {}) {
  const input = h('input', { type: 'text', value: value ?? '', placeholder, spellcheck: 'false' });
  const jump = h('button', { class: 'icon-button ref-jump', title: 'Open referenced record', tabindex: '-1' }, '→');
  const wrap = h('div', { class: 'ref-picker' }, input, jump);
  let menu = null;
  let focusedIndex = -1;
  let options = [];

  const candidates = () => {
    const pool = [];
    for (const typeId of refTypes ?? []) {
      const type = typeOf(typeId);
      for (const record of (allRecords().filter((candidate) => candidate.typeId === typeId)))
        pool.push({ id: record.id, name: record.name ?? record.id, type: type?.singularName ?? typeId });
    }
    return pool;
  };

  const closeMenu = () => { menu?.remove(); menu = null; focusedIndex = -1; };

  const openMenu = () => {
    closeMenu();
    const query = input.value.trim().toLowerCase();
    options = candidates()
      .filter((option) => !query || option.id.toLowerCase().includes(query) || option.name.toLowerCase().includes(query))
      .slice(0, 60);
    if (!options.length) return;
    menu = h('div', { class: 'ref-menu' }, options.map((option, index) => h('div', {
      class: 'ref-option', dataset: { index },
      onmousedown: (event) => { event.preventDefault(); choose(index); },
    },
      h('span', { class: 'ref-name' }, option.name),
      h('span', { class: 'ref-type' }, option.type),
      h('span', { class: 'ref-id' }, option.id))));
    document.body.append(menu);
    const rect = input.getBoundingClientRect();
    menu.style.left = `${rect.left}px`;
    menu.style.top = `${Math.min(rect.bottom + 2, window.innerHeight - 330)}px`;
    menu.style.minWidth = `${rect.width}px`;
  };

  const choose = (index) => {
    const option = options[index];
    if (!option) return;
    input.value = option.id;
    onCommit(option.id);
    closeMenu();
  };

  input.addEventListener('focus', openMenu);
  input.addEventListener('input', openMenu);
  input.addEventListener('blur', () => setTimeout(() => { closeMenu(); }, 120));
  input.addEventListener('change', () => onCommit(input.value.trim() === '' ? undefined : input.value.trim()));
  input.addEventListener('keydown', (event) => {
    if (!menu && (event.key === 'ArrowDown' || event.key === 'ArrowUp')) { openMenu(); return; }
    if (!menu) return;
    const rows = [...menu.querySelectorAll('.ref-option')];
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      focusedIndex = Math.max(0, Math.min(options.length - 1, focusedIndex + (event.key === 'ArrowDown' ? 1 : -1)));
      rows.forEach((row, index) => row.classList.toggle('focused', index === focusedIndex));
      rows[focusedIndex]?.scrollIntoView({ block: 'nearest' });
    } else if (event.key === 'Enter' && focusedIndex >= 0) {
      event.preventDefault();
      choose(focusedIndex);
    } else if (event.key === 'Escape') closeMenu();
  });
  jump.addEventListener('click', () => {
    const id = input.value.trim();
    if (id && findRecord(id)) location.hash = `#/record/${encodeURIComponent(id)}`;
  });
  return wrap;
}

function refControl(field, ctx) {
  return refInput(field.refTypes, ctx.get(field.name), (value) => setOrDelete(ctx, field, value),
    { placeholder: field.optional ? '(none)' : `Search ${field.refTypes?.join(', ') ?? ''}…` });
}

function refListControl(field, ctx) {
  const container = h('div', { class: 'list-editor' });
  const rebuild = () => {
    const values = ctx.get(field.name) ?? [];
    replaceChildren(container,
      values.map((value, index) => h('div', { class: 'list-item' },
        h('div', { style: { flex: 1 } }, refInput(field.refTypes, value, (next) => {
          const list = [...(ctx.get(field.name) ?? [])];
          if (next === undefined) list.splice(index, 1); else list[index] = next;
          ctx.set(field.name, list); ctx.onChange(); rebuild();
        })),
        listItemTools(values, index, (list) => { ctx.set(field.name, list); ctx.onChange(); rebuild(); }))),
      h('button', { class: 'button compact list-add', onclick: () => {
        ctx.set(field.name, [...(ctx.get(field.name) ?? []), '']);
        rebuild();
      } }, '+ Add'));
  };
  rebuild();
  return container;
}

function listItemTools(list, index, commit) {
  return h('div', { class: 'list-item-tools' },
    h('button', { class: 'icon-button', title: 'Move up', onclick: () => {
      if (index === 0) return;
      const next = [...list];
      [next[index - 1], next[index]] = [next[index], next[index - 1]];
      commit(next);
    } }, '↑'),
    h('button', { class: 'icon-button', title: 'Move down', onclick: () => {
      if (index >= list.length - 1) return;
      const next = [...list];
      [next[index + 1], next[index]] = [next[index], next[index + 1]];
      commit(next);
    } }, '↓'),
    h('button', { class: 'icon-button danger', title: 'Remove', onclick: () => {
      const next = [...list];
      next.splice(index, 1);
      commit(next);
    } }, '✕'));
}

// ── Chips (tags / string lists) ───────────────────────────────────────────────────────────

function tagSuggestions(field, ctx) {
  if (field.enumSourceName) return vocabList(field.enumSourceName);
  if (ctx.typeId === 'materials') return vocabList('materialTags');
  if (ctx.typeId === 'moves') return vocabList('moveTagsAll');
  if (ctx.typeId === 'loot_tables') return vocabList('lootContextTags');
  return [];
}

function chipsControl(field, ctx, suggestions, { numeric = false } = {}) {
  const container = h('div', { class: 'chips-editor' });
  const listId = `chips-${Math.random().toString(36).slice(2, 8)}`;
  const rebuild = () => {
    const values = ctx.get(field.name) ?? [];
    const input = h('input', { type: 'text', list: suggestions?.length ? listId : null, placeholder: '+ add' });
    input.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' && input.value.trim()) {
        event.preventDefault();
        commitAdd(input.value.trim());
      } else if (event.key === 'Backspace' && input.value === '' && values.length) {
        commitList(values.slice(0, -1));
      }
    });
    input.addEventListener('change', () => { if (input.value.trim()) commitAdd(input.value.trim()); });
    const commitAdd = (raw) => {
      let value = raw;
      if (numeric) {
        value = Number(raw);
        if (Number.isNaN(value)) { input.classList.add('invalid'); return; }
        input.classList.remove('invalid');
      }
      if (!values.includes(value)) commitList([...values, value]);
    };
    const commitList = (list) => {
      if (list.length === 0 && field.optional) ctx.remove(field.name);
      else ctx.set(field.name, list);
      ctx.onChange(); rebuild();
    };
    replaceChildren(container,
      values.map((value) => h('span', { class: `chip ${chipFamilyClass(value)}` }, String(value),
        h('span', { class: 'chip-x', onclick: () => commitList(values.filter((candidate) => candidate !== value)) }, '×'))),
      input,
      suggestions?.length ? h('datalist', { id: listId }, suggestions.map((option) => h('option', { value: option }))) : null);
  };
  rebuild();
  return container;
}

function chipFamilyClass(tag) {
  const text = String(tag);
  const family = text.includes(':') ? text.split(':')[0] : '';
  return family ? `family-${family}` : '';
}

// ── Dictionaries ──────────────────────────────────────────────────────────────────────────

function numberDictControl(field, ctx) {
  const container = h('div', {});
  const listId = `dk-${Math.random().toString(36).slice(2, 8)}`;
  const rebuild = () => {
    const dict = ctx.get(field.name) ?? {};
    const keys = Object.keys(dict);
    const suggestions = dictKeys(field.keySource, { ownSlots: Object.keys(ctx.workingRoot()?.slots ?? {}) })
      .filter((key) => !keys.includes(key));
    const grid = h('div', { class: 'kv-grid' });
    for (const key of keys) {
      const valueInput = h('input', {
        type: 'number', value: dict[key],
        min: field.min ?? null, max: field.max ?? null, step: field.step ?? 'any',
      });
      valueInput.addEventListener('change', () => {
        const parsed = Number(valueInput.value);
        if (Number.isNaN(parsed)) return;
        const next = { ...(ctx.get(field.name) ?? {}) };
        next[key] = parsed;
        ctx.set(field.name, next); ctx.onChange();
      });
      grid.append(
        h('span', { class: 'kv-key', title: key }, key),
        valueInput,
        h('button', { class: 'icon-button danger', title: `Remove ${key}`, onclick: () => {
          const next = { ...(ctx.get(field.name) ?? {}) };
          delete next[key];
          if (Object.keys(next).length === 0 && field.optional) ctx.remove(field.name);
          else ctx.set(field.name, next);
          ctx.onChange(); rebuild();
        } }, '✕'));
    }
    const addInput = h('input', { type: 'text', list: listId, placeholder: '+ key', style: { fontFamily: 'var(--mono)', fontSize: '11.5px' } });
    const commitKey = () => {
      const key = addInput.value.trim();
      if (!key) return;
      const next = { ...(ctx.get(field.name) ?? {}) };
      if (!(key in next)) next[key] = field.min ?? 0;
      ctx.set(field.name, next); ctx.onChange(); rebuild();
    };
    addInput.addEventListener('keydown', (event) => { if (event.key === 'Enter') { event.preventDefault(); commitKey(); } });
    addInput.addEventListener('change', commitKey);
    replaceChildren(container, grid,
      h('div', { style: { marginTop: '4px' } }, addInput,
        h('datalist', { id: listId }, suggestions.map((option) => h('option', { value: option })))));
  };
  rebuild();
  return container;
}

// ── Nested objects & lists ────────────────────────────────────────────────────────────────

function childCtx(ctx, getParent, setParent) {
  return {
    ...ctx,
    get: (name) => getParent()?.[name],
    set: (name, value) => {
      const parent = { ...(getParent() ?? {}) };
      parent[name] = value;
      setParent(parent);
    },
    remove: (name) => {
      const parent = { ...(getParent() ?? {}) };
      delete parent[name];
      setParent(parent);
    },
    onIdEdited: null,
    problemFields: new Set(),
  };
}

function objectControl(field, ctx) {
  const container = h('div', { class: 'subobject' });
  const nested = childCtx(ctx,
    () => ctx.get(field.name),
    (parent) => { ctx.set(field.name, parent); ctx.onChange(); });
  if (!field.fields?.length) return jsonControl(field, ctx);
  renderFields(field.fields, container, nested);
  if (field.optional) {
    const hasValue = ctx.get(field.name) !== undefined;
    if (!hasValue) {
      return h('button', { class: 'button compact', onclick: (event) => {
        ctx.set(field.name, {});
        ctx.onChange();
        event.target.replaceWith(objectControl(field, ctx));
      } }, `+ Add ${field.label}`);
    }
    const remove = h('button', { class: 'button compact danger', style: { marginTop: '6px' }, onclick: () => {
      ctx.remove(field.name); ctx.onChange();
      container.replaceWith(objectControl(field, ctx));
    } }, `Remove ${field.label}`);
    container.append(remove);
  }
  return container;
}

function itemSummary(item) {
  if (item === null || item === undefined) return '';
  if (typeof item !== 'object') return String(item);
  for (const key of ['id', 'name', 'itemId', 'tableId', 'move', 'moveTag', 'kind', 'op', 'event', 'stance', 'property', 'key', 'slot', 'tag', 'resource', 'with', 'channel']) {
    if (item[key] !== undefined && item[key] !== null && item[key] !== '') {
      const extra = item.amount ?? item.value ?? item.weight ?? item.chance ?? item.quantity;
      return `${item[key]}${extra !== undefined ? ` · ${extra}` : ''}`;
    }
  }
  return Object.keys(item).slice(0, 3).join(', ');
}

function objectListControl(field, ctx) {
  const container = h('div', { class: 'list-editor' });
  const openItems = new Set();
  const rebuild = () => {
    const list = ctx.get(field.name) ?? [];
    const commit = (next) => {
      if (next.length === 0 && field.optional) ctx.remove(field.name);
      else ctx.set(field.name, next);
      ctx.onChange(); rebuild();
    };
    replaceChildren(container,
      list.map((item, index) => {
        const isOpen = openItems.has(index) || list.length <= 3;
        const body = h('div', { class: 'subobject', style: { display: isOpen ? '' : 'none' } });
        if (field.fields?.length) {
          renderFields(field.fields, body, childCtx(ctx,
            () => (ctx.get(field.name) ?? [])[index],
            (updated) => {
              const next = [...(ctx.get(field.name) ?? [])];
              next[index] = updated;
              ctx.set(field.name, next); ctx.onChange();
            }));
        } else {
          body.append(inlineJsonEditor(item, (value) => {
            const next = [...(ctx.get(field.name) ?? [])];
            next[index] = value;
            ctx.set(field.name, next); ctx.onChange();
          }));
        }
        return h('div', { class: 'list-item' },
          h('div', { style: { flex: 1, minWidth: 0 } },
            h('div', { class: 'item-header', onclick: () => {
              if (openItems.has(index)) openItems.delete(index); else openItems.add(index);
              body.style.display = body.style.display === 'none' ? '' : 'none';
            } },
              h('span', { class: 'twist' }, isOpen ? '▾' : '▸'),
              h('span', {}, `${index + 1}.`),
              h('span', { class: 'item-summary' }, itemSummary(item))),
            body),
          h('div', { class: 'list-item-tools' },
            h('button', { class: 'icon-button', title: 'Move up', onclick: () => {
              if (index === 0) return;
              const next = [...list]; [next[index - 1], next[index]] = [next[index], next[index - 1]]; commit(next);
            } }, '↑'),
            h('button', { class: 'icon-button', title: 'Move down', onclick: () => {
              if (index >= list.length - 1) return;
              const next = [...list]; [next[index + 1], next[index]] = [next[index], next[index + 1]]; commit(next);
            } }, '↓'),
            h('button', { class: 'icon-button', title: 'Duplicate', onclick: () => {
              const next = [...list]; next.splice(index + 1, 0, structuredClone(item)); commit(next);
            } }, '⧉'),
            h('button', { class: 'icon-button danger', title: 'Remove', onclick: () => {
              const next = [...list]; next.splice(index, 1); commit(next);
            } }, '✕')));
      }),
      h('button', { class: 'button compact list-add', onclick: () => {
        openItems.add(list.length);
        commit([...list, {}]);
      } }, `+ Add ${field.label.replace(/s$/, '')}`));
  };
  rebuild();
  return container;
}

function keyedControl(field, ctx, renderValue, emptyValue) {
  const container = h('div', { class: 'list-editor' });
  const rebuild = () => {
    const dict = ctx.get(field.name) ?? {};
    const commit = (next) => { ctx.set(field.name, next); ctx.onChange(); rebuild(); };
    replaceChildren(container,
      Object.keys(dict).map((key) => h('div', {},
        h('div', { class: 'item-header' },
          h('span', { class: 'mono', style: { color: 'var(--accent)' } }, key),
          h('button', { class: 'icon-button danger', style: { marginLeft: 'auto' }, title: 'Remove', onclick: () => {
            const next = { ...dict };
            delete next[key];
            commit(next);
          } }, '✕')),
        renderValue(key))),
      h('button', { class: 'button compact list-add', onclick: async () => {
        const { promptModal } = await import('./ui.js');
        const key = await promptModal(`New ${field.label} key`, {
          validate: (value) => !value.trim() ? 'A key is required.' : (dict[value.trim()] ? 'That key already exists.' : null),
        });
        if (!key) return;
        commit({ ...dict, [key.trim()]: structuredClone(emptyValue) });
      } }, '+ Add key'));
  };
  rebuild();
  return container;
}

function objectDictControl(field, ctx) {
  return keyedControl(field, ctx, (key) => {
    const body = h('div', { class: 'subobject' });
    if (field.fields?.length) {
      renderFields(field.fields, body, childCtx(ctx,
        () => (ctx.get(field.name) ?? {})[key],
        (updated) => {
          const next = { ...(ctx.get(field.name) ?? {}) };
          next[key] = updated;
          ctx.set(field.name, next); ctx.onChange();
        }));
    }
    return body;
  }, {});
}

function objectListDictControl(field, ctx) {
  return keyedControl(field, ctx, (key) => {
    const listField = { ...field, name: key, label: key, kind: 'objectList', optional: false };
    const nested = childCtx(ctx,
      () => ctx.get(field.name),
      (parent) => { ctx.set(field.name, parent); ctx.onChange(); });
    return objectListControl(listField, nested);
  }, []);
}

// ── Move grants (string-or-object) ────────────────────────────────────────────────────────

function moveGrantControl(field, ctx, value, commit) {
  const container = h('div', {});
  const rebuild = (current) => {
    const id = typeof current === 'string' ? current : current?.id ?? '';
    const replaces = typeof current === 'object' && current !== null ? current.replaces ?? '' : '';
    const showReplaces = replaces !== '';
    const idPicker = refInput(['moves'], id, (nextId) => {
      commit(normalizeGrant(nextId, replaces));
    });
    const replacesRow = h('div', { class: 'field-inline', style: { marginTop: '4px', display: showReplaces ? '' : 'none' } },
      h('span', { style: { fontSize: '11px', color: 'var(--fg-faint)' } }, 'replaces'),
      h('div', { style: { flex: 1 } }, refInput(['moves'], replaces, (nextReplaces) => {
        commit(normalizeGrant(id, nextReplaces ?? ''));
      })));
    const toggle = h('button', { class: 'button ghost compact', title: 'This grant can replace another move', onclick: () => {
      replacesRow.style.display = replacesRow.style.display === 'none' ? '' : 'none';
    } }, showReplaces ? '⇄' : '+ replaces');
    replaceChildren(container, h('div', { class: 'field-inline' }, h('div', { style: { flex: 1 } }, idPicker), toggle), replacesRow);
  };
  rebuild(value);
  return container;
}

function normalizeGrant(id, replaces) {
  if (!id) return undefined;
  return replaces ? { id, replaces } : id;
}

function moveGrantListControl(field, ctx) {
  const container = h('div', { class: 'list-editor' });
  const rebuild = () => {
    const list = ctx.get(field.name) ?? [];
    const commit = (next) => {
      if (next.length === 0 && field.optional) ctx.remove(field.name);
      else ctx.set(field.name, next);
      ctx.onChange(); rebuild();
    };
    replaceChildren(container,
      list.map((item, index) => h('div', { class: 'list-item' },
        h('div', { style: { flex: 1, minWidth: 0 } },
          moveGrantControl(field, ctx, item, (value) => {
            const next = [...(ctx.get(field.name) ?? [])];
            if (value === undefined) next.splice(index, 1); else next[index] = value;
            commit(next);
          })),
        listItemTools(list, index, commit))),
      h('button', { class: 'button compact list-add', onclick: () => commit([...list, '']) }, '+ Add move'));
  };
  rebuild();
  return container;
}

// ── Raw JSON fallback ─────────────────────────────────────────────────────────────────────

function inlineJsonEditor(value, commit) {
  const area = h('textarea', { rows: 4, spellcheck: 'false', style: { fontFamily: 'var(--mono)', fontSize: '11.5px', width: '100%' } });
  area.value = JSON.stringify(value ?? null, null, 2);
  area.addEventListener('change', () => {
    try {
      commit(JSON.parse(area.value));
      area.classList.remove('invalid');
    } catch {
      area.classList.add('invalid');
    }
  });
  return area;
}

function jsonControl(field, ctx) {
  const wrapper = h('div', {});
  const value = ctx.get(field.name);
  wrapper.append(inlineJsonEditor(value, (parsed) => {
    if (parsed === null && field.optional) ctx.remove(field.name);
    else ctx.set(field.name, parsed);
    ctx.onChange();
  }));
  if (field.help) wrapper.append(h('div', { class: 'field-help' }, field.help));
  return wrapper;
}
