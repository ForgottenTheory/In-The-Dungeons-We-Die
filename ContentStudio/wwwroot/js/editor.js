// The record editor: grouped form (schema-driven), Advanced raw-JSONC tab, and per-type
// extra tabs (Resolved view for enemies, Drop analysis for loot tables, Graph for realms).

import { api } from './api.js';
import { h, replaceChildren, toast, confirmModal, promptModal, debounce, formatNumber } from './ui.js';
import { state, typeOf, findRecord, problemsOf, applyRecordUpdate, applyValidation, emit, refreshTypeRecords } from './state.js';
import { renderFields } from './fields.js';
import { renderInspectorForRecord } from './inspector.js';
import { pushUndo } from './undo.js';
import { renderResolvedActorPanel } from './views/resolved.js';
import { renderLootAnalysisPanel } from './views/lootpanel.js';
import { renderRealmGraphPanel } from './views/realmgraph.js';

/** Field grouping per type; anything unlisted lands in "Details". Order matters. */
const EDITOR_LAYOUTS = {
  actors: [
    { title: 'Identity', fields: ['id', 'name', 'tags'] },
    { title: 'Composition', fields: ['family', 'role', 'ai_profile'] },
    { title: 'Stat Tweaks', fields: ['attribute_tweaks', 'resource_tweaks', 'armor', 'resolve'] },
    { title: 'Standalone Stats (no family)', fields: ['attributes', 'resources'], collapsed: true },
    { title: 'Defences', fields: ['resistances', 'vulnerable'] },
    { title: 'Moves & AI', fields: ['moves', 'ai'] },
    { title: 'Loot', fields: ['loot_table'] },
  ],
  enemy_families: [
    { title: 'Identity', fields: ['id', 'name', 'tags'] },
    { title: 'Body', fields: ['attributes', 'resources', 'armor', 'resolve'] },
    { title: 'Defences', fields: ['resistances', 'vulnerable'] },
    { title: 'Loot', fields: ['loot_table'] },
  ],
  enemy_roles: [
    { title: 'Identity', fields: ['id', 'name', 'tags'] },
    { title: 'Deltas', fields: ['attribute_tweaks', 'resource_tweaks', 'armor', 'resolve'] },
    { title: 'Defences', fields: ['resistances', 'vulnerable'] },
    { title: 'Behaviour', fields: ['ai_profile'] },
    { title: 'Loot', fields: ['loot_table'] },
  ],
  moves: [
    { title: 'Identity', fields: ['id', 'name', 'kind', 'tags', 'description'] },
    { title: 'Timing', fields: ['timing', 'cooldown_ticks', 'interruptible'] },
    { title: 'Costs & Requirements', fields: ['costs', 'requires'] },
    { title: 'Damage', fields: ['packets', 'stagger_power'] },
    { title: 'Targeting', fields: ['targeting', 'max_targets'] },
    { title: 'Effects', fields: ['effects'] },
  ],
  statuses: [
    { title: 'Identity', fields: ['id', 'name', 'category', 'tags', 'description'] },
    { title: 'Lifetime', fields: ['duration_ticks', 'tick_interval', 'stack_policy', 'max_stacks', 'cleanse_group'] },
    { title: 'Magnitude & Lane', fields: ['magnitude', 'lane', 'control_buildup'] },
    { title: 'While Active', fields: ['while_active'] },
    { title: 'Hooks', fields: ['on_apply', 'per_tick', 'on_expire'] },
    { title: 'Special', fields: ['requires_status', 'stores_move'] },
  ],
  materials: [
    { title: 'Identity', fields: ['id', 'name', 'tags'] },
    { title: 'Properties (0–100)', fields: ['properties'] },
    { title: 'Essence', fields: ['essence'] },
    { title: 'Overrides', fields: ['materialStrength', 'workability'], collapsed: true },
  ],
  loot_tables: [
    { title: 'Identity', fields: ['id', 'name', 'tags'] },
    { title: 'Always Drops', fields: ['alwaysDrops'] },
    { title: 'Chance Drops', fields: ['chanceDrops'] },
    { title: 'Weighted Draws', fields: ['weightedDraws'] },
    { title: 'Gold', fields: ['gold'] },
  ],
  profession_actions: [
    { title: 'Identity', fields: ['id', 'name', 'professionId', 'requiredLevel'] },
    { title: 'Timing & XP', fields: ['baseIntervalTicks', 'experience', 'successChance'] },
    { title: 'Economy', fields: ['inputs', 'outputs', 'bonusOutputs'] },
    { title: 'Extras', fields: ['loot_table', 'opportunities', 'realmKnowledgeGain'] },
  ],
  forms: [
    { title: 'Identity', fields: ['id', 'name', 'name_variants', 'type', 'tags', 'description'] },
    { title: 'Slots', fields: ['slots'] },
    { title: 'Stat Map', fields: ['stat_map'] },
    { title: 'Traits', fields: ['trait_cap'] },
    { title: 'Moves', fields: ['moves'] },
  ],
  affixes: [
    { title: 'Identity', fields: ['id', 'name', 'slot', 'family', 'class', 'tags', 'description', 'drawback'] },
    { title: 'Availability', fields: ['availability', 'chance_weight'] },
    { title: 'Tiers', fields: ['tiers'] },
    { title: 'Grants', fields: ['grants'] },
  ],
  realms: [
    { title: 'Identity', fields: ['id', 'name', 'tags', 'supportedTiers'] },
    { title: 'Locations', fields: ['locations'] },
  ],
  processes: [
    { title: 'Identity', fields: ['id', 'name'] },
    { title: 'Reaction', fields: ['medium', 'severity', 'affected_qualities', 'essence_rate'] },
    { title: 'Roles', fields: ['role_weights'] },
    { title: 'Gates', fields: ['profession', 'requires'] },
    { title: 'Tag Effects', fields: ['tag_effects'] },
  ],
  equipment: [
    { title: 'Identity', fields: ['id', 'name', 'slot', 'tags'] },
    { title: 'Combat', fields: ['moves', 'move_modifiers', 'armor'] },
    { title: 'Profile', fields: ['properties', 'essence'] },
  ],
};

const activeEditors = new Map(); // recordId → editor instance (for SSE refresh)

export async function openRecordEditor(workspace, recordId) {
  let record = findRecord(recordId);
  if (!record) {
    // Cold navigation (fresh tab straight to a record URL): resolve the type, then load it.
    replaceChildren(workspace, h('div', { class: 'spinner' }));
    try {
      const detail = await api.record(recordId);
      const { loadRecords } = await import('./state.js');
      await loadRecords(detail.record.typeId, true);
      record = findRecord(recordId);
    } catch {
      record = null;
    }
  }
  if (!record) {
    replaceChildren(workspace, h('div', { class: 'empty-state' },
      h('div', { class: 'big' }, '∅'),
      h('div', {}, `No record with id `, h('code', {}, recordId)),
    ));
    return;
  }
  const type = typeOf(record.typeId);
  const editor = new RecordEditor(workspace, record, type);
  editor.render();
  return editor;
}

class RecordEditor {
  constructor(workspace, record, type) {
    this.workspace = workspace;
    this.record = record;
    this.type = type;
    this.working = structuredClone(record.data);
    this.activeTab = 'form';
    this.saveState = 'idle'; // idle | pending | saving | error
    this.pushDebounced = debounce(() => this.push(), 450);
    activeEditors.set(record.id, this);
  }

  fieldContext() {
    return {
      typeId: this.type.typeId,
      get: (name) => this.working[name],
      set: (name, value) => { this.working[name] = value; },
      remove: (name) => { delete this.working[name]; },
      onChange: () => this.markChanged(),
      workingRoot: () => this.working,
      problemFields: this.problemFieldNames(),
      onIdEdited: (input) => this.handleIdEdit(input),
    };
  }

  problemFieldNames() {
    const names = new Set();
    for (const problem of problemsOf(this.record.id)) {
      const schema = this.type.schema ?? [];
      for (const field of schema) {
        if (problem.message.toLowerCase().includes(field.name.toLowerCase()) && field.name.length > 3)
          names.add(field.name);
      }
    }
    return names;
  }

  markChanged() {
    pushUndo(this.record.id, this.lastPushedValue ?? this.record.data);
    this.saveState = 'pending';
    this.renderSaveState();
    this.pushDebounced();
  }

  async handleIdEdit(input) {
    const newId = input.value.trim();
    const oldId = this.record.id;
    if (newId === oldId) return;
    const incoming = (await api.deps(oldId)).incoming;
    const message = incoming.length
      ? h('div', {},
          h('p', {}, `${incoming.length} other definition(s) reference `, h('code', {}, oldId), '. They will NOT be rewritten and will break until you update them:'),
          h('ul', {}, incoming.slice(0, 12).map((edge) => h('li', {}, h('code', {}, edge.id), ` — ${edge.fieldPath}`))),
          incoming.length > 12 ? h('p', {}, `…and ${incoming.length - 12} more.`) : null)
      : h('p', {}, 'Nothing references this id yet — safe to rename.');
    const proceed = await confirmModal(`Rename ${oldId} → ${newId}?`, message, 'Rename');
    if (!proceed) {
      input.value = oldId;
      this.working.id = oldId;
      return;
    }
    this.working.id = newId;
    this.markChanged();
    this.pushDebounced.flush();
  }

  async push() {
    this.saveState = 'saving';
    this.renderSaveState();
    const previousId = this.record.id;
    try {
      const payload = await api.saveRecord(previousId, this.working);
      this.lastPushedValue = structuredClone(this.working);
      this.record = payload.record;
      this.rawText = payload.rawText;
      applyRecordUpdate(previousId, payload);
      applyValidation({ ...state.validation, ...payload.validation, problems: state.validation.problems });
      const validation = await api.validation();
      applyValidation(validation);
      if (previousId !== this.record.id) {
        activeEditors.delete(previousId);
        activeEditors.set(this.record.id, this);
        history.replaceState(null, '', `#/record/${encodeURIComponent(this.record.id)}`);
      }
      this.saveState = 'idle';
      this.renderSaveState();
      this.renderInspector();
    } catch (error) {
      this.saveState = 'error';
      this.saveError = error.message;
      this.renderSaveState();
      toast('Edit rejected', error.message, 'err', 5200);
    }
  }

  /** Re-adopts server data after an external reload (file watcher). */
  adoptExternal(record) {
    this.record = record;
    this.working = structuredClone(record.data);
    this.render();
  }

  render() {
    const record = this.record;
    const type = this.type;
    const problems = problemsOf(record.id);

    const tabs = [
      { key: 'form', label: 'Form' },
      type.typeId === 'actors' ? { key: 'resolved', label: 'Resolved' } : null,
      type.typeId === 'loot_tables' ? { key: 'analysis', label: 'Drop Analysis' } : null,
      type.typeId === 'realms' ? { key: 'graph', label: 'Graph' } : null,
      ['materials', 'consumables', 'techniques'].includes(type.typeId) ? { key: 'sources', label: 'Where It Drops' } : null,
      { key: 'raw', label: 'Raw JSONC' },
    ].filter(Boolean);

    this.saveStateElement = h('span', { class: 'badge dim', style: { marginLeft: 'auto' } }, '');

    const header = h('div', { class: 'view-header' },
      h('div', { class: 'breadcrumbs' },
        h('a', { onclick: () => { location.hash = `#/type/${type.typeId}`; } }, type.displayName),
        h('span', { class: 'sep' }, '›'),
        h('span', { style: { color: 'var(--fg)' } }, record.name ?? record.id)),
      this.saveStateElement,
      h('button', { class: 'button compact', onclick: () => this.validateNow() }, 'Validate'),
      h('button', { class: 'button compact', onclick: () => duplicateRecordInteractive(record.id) }, 'Duplicate'),
      h('button', { class: 'button compact danger', onclick: () => deleteRecordInteractive(record.id) }, 'Delete'),
      h('div', { class: 'view-subtitle' },
        h('code', {}, record.id), '  ·  ', record.file,
        problems.length ? `  ·  ${problems.length} problem(s)` : ''));

    const tabBar = h('div', { class: 'editor-tabs' },
      tabs.map((tab) => h('div', {
        class: `editor-tab${this.activeTab === tab.key ? ' active' : ''}`,
        onclick: () => { this.activeTab = tab.key; this.render(); },
      }, tab.label)));

    this.body = h('div', { class: 'editor-scroll' });
    this.renderActiveTab();

    replaceChildren(this.workspace, h('div', { class: 'editor-layout' }, header, tabBar, this.body));
    this.renderSaveState();
    this.renderInspector();
  }

  renderSaveState() {
    if (!this.saveStateElement) return;
    const map = {
      idle: ['saved', 'badge ok'],
      pending: ['editing…', 'badge dim'],
      saving: ['saving…', 'badge accent'],
      error: [`error: ${this.saveError ?? ''}`, 'badge err'],
    };
    const [label, cls] = map[this.saveState] ?? map.idle;
    this.saveStateElement.textContent = this.record.dirty || this.saveState !== 'idle' ? label : 'unchanged';
    this.saveStateElement.className = cls;
    this.saveStateElement.style.marginLeft = 'auto';
  }

  async validateNow() {
    const payload = await api.revalidate();
    applyValidation(payload);
    const problems = problemsOf(this.record.id);
    toast(problems.length ? `${problems.length} problem(s) on this record` : 'Record is valid',
      problems[0]?.message ?? '', problems.some((p) => p.severity === 'error') ? 'err' : problems.length ? 'warn' : 'ok');
    this.render();
  }

  renderActiveTab() {
    replaceChildren(this.body);
    switch (this.activeTab) {
      case 'form': this.renderForm(); break;
      case 'raw': this.renderRaw(); break;
      case 'resolved': renderResolvedActorPanel(this.body, this.record.id); break;
      case 'analysis': renderLootAnalysisPanel(this.body, this.record.id); break;
      case 'graph': renderRealmGraphPanel(this.body, this.record.id); break;
      case 'sources': this.renderSources(); break;
    }
  }

  renderForm() {
    const schema = this.type.schema ?? [];
    const layout = EDITOR_LAYOUTS[this.type.typeId];
    const schemaByName = new Map(schema.map((field) => [field.name, field]));
    const rendered = new Set();
    const context = this.fieldContext();

    const renderGroup = (title, fields, collapsed) => {
      const group = h('div', { class: `field-group${collapsed ? ' collapsed' : ''}` });
      const body = h('div', { class: 'field-group-body' });
      const titleRow = h('div', { class: 'field-group-title', onclick: () => group.classList.toggle('collapsed') },
        h('span', { class: 'twist' }, '▾'), title);
      group.append(titleRow, body);
      renderFields(fields, body, context);
      this.body.append(group);
    };

    if (layout) {
      for (const section of layout) {
        const fields = section.fields.map((name) => schemaByName.get(name)).filter(Boolean);
        fields.forEach((field) => rendered.add(field.name));
        if (fields.length) renderGroup(section.title, fields, section.collapsed);
      }
      const remaining = schema.filter((field) => !rendered.has(field.name));
      if (remaining.length) renderGroup('Details', remaining, false);
    } else {
      const identity = schema.filter((field) => ['id', 'name', 'tags', 'description'].includes(field.name));
      const rest = schema.filter((field) => !identity.includes(field));
      if (identity.length) renderGroup('Identity', identity, false);
      if (rest.length) renderGroup('Details', rest, false);
    }

    // Fields present in the JSON but unknown to the game — surfaced, marked, editable raw.
    const unknownFields = Object.keys(this.working)
      .filter((name) => !schemaByName.has(name))
      .map((name) => ({ name, label: `${name} ⚠`, kind: 'json', optional: true, help: 'Not a known field of this type — the game silently ignores it.' }));
    if (unknownFields.length) renderGroup('Unknown Fields', unknownFields, false);
  }

  renderRaw() {
    const area = h('textarea', { spellcheck: 'false' });
    area.value = this.rawText ?? '';
    const status = h('span', { class: 'badge dim' }, 'comments in this record are preserved');
    const apply = h('button', {
      class: 'button primary compact',
      onclick: async () => {
        try {
          const payload = await api.saveRecordRaw(this.record.id, area.value);
          const previousId = this.record.id;
          this.record = payload.record;
          this.working = structuredClone(payload.record.data);
          this.rawText = payload.rawText;
          applyRecordUpdate(previousId, payload);
          const validation = await api.validation();
          applyValidation(validation);
          toast('Raw edit applied', '', 'ok');
          this.render();
        } catch (error) {
          toast('Raw edit rejected', error.message, 'err', 5200);
        }
      },
    }, 'Apply');

    const load = async () => {
      if (this.rawText === undefined) {
        const detail = await api.record(this.record.id);
        this.rawText = detail.rawText;
        area.value = this.rawText;
      }
    };
    load();

    replaceChildren(this.body, h('div', { class: 'raw-editor', style: { height: '100%' } },
      h('div', { class: 'raw-actions' }, apply, status),
      area));
  }

  async renderSources() {
    this.body.append(h('div', { class: 'spinner' }));
    try {
      const payload = await api.analysisLootItem(this.record.id, { depth: 2, active: true });
      replaceChildren(this.body);
      if (!payload.sources?.length) {
        this.body.append(h('div', { class: 'empty-state' },
          h('div', { class: 'big' }, '🕳'),
          h('div', {}, 'No loot source pays this item under depth ≤ 2, active play.'),
          h('div', { style: { fontSize: '11.5px' } }, 'It may come from profession outputs instead — check incoming references in the inspector.')));
        return;
      }
      this.body.append(h('div', { class: 'section-title' }, `Drops from ${payload.sources.length} source(s) — expected per kill/gather (depth 2, active)`));
      this.body.append(h('table', { class: 'grid' },
        h('thead', {}, h('tr', {}, h('th', {}, 'Source'), h('th', {}, 'Kind'), h('th', {}, 'Expected / event'))),
        h('tbody', {}, payload.sources.map((source) => h('tr', {
          onclick: () => {
            if (source.sourceKind === 'enemy') location.hash = `#/record/${encodeURIComponent(source.sourceId)}`;
            else if (source.sourceKind === 'profession-action') location.hash = `#/record/${encodeURIComponent(source.sourceId)}`;
          },
        },
          h('td', {}, source.sourceName),
          h('td', {}, h('span', { class: 'badge dim' }, source.sourceKind)),
          h('td', { class: 'num' }, formatNumber(source.expectedPerEvent, 4)))))));
    } catch (error) {
      replaceChildren(this.body, h('div', { class: 'empty-state' }, h('div', {}, error.message)));
    }
  }

  renderInspector() {
    renderInspectorForRecord(this.record.id);
  }
}

export function editorFor(recordId) {
  return activeEditors.get(recordId);
}

// ── Shared record actions (used by editor, lists, palette, context menus) ─────────────────

export async function duplicateRecordInteractive(id) {
  const record = findRecord(id);
  if (!record) return;
  const newId = await promptModal(`Duplicate ${id}`, {
    label: 'New id',
    initial: `${id}_copy`,
    validate: (value) => !value.trim() ? 'An id is required.' : (findRecord(value.trim()) ? 'That id already exists.' : null),
  });
  if (!newId) return;
  try {
    const payload = await api.duplicateRecord(id, newId.trim());
    await refreshTypeRecords(record.typeId);
    const validation = await api.validation();
    applyValidation(validation);
    toast('Duplicated', `${id} → ${newId}`, 'ok');
    location.hash = `#/record/${encodeURIComponent(payload.record.id)}`;
  } catch (error) {
    toast('Duplicate failed', error.message, 'err');
  }
}

export async function deleteRecordInteractive(id) {
  const record = findRecord(id);
  if (!record) return;
  try {
    const deps = await api.deps(id);
    const body = h('div', {},
      deps.incoming.length
        ? h('div', {},
            h('p', {}, `Referenced by ${deps.incoming.length} other definition(s):`),
            h('div', { style: { maxHeight: '200px', overflow: 'auto' } },
              deps.incoming.map((edge) => h('div', { class: 'ref-link' },
                h('span', { class: 'ref-link-name' }, `${edge.name ?? edge.id}`),
                h('span', { class: 'ref-link-path' }, edge.fieldPath)))),
            h('p', { style: { color: 'var(--warn)' } }, 'Deleting will break these references.'))
        : h('p', {}, 'Nothing references this record.'),
      h('p', {}, 'The change stays unsaved until you Save — and every save is backed up.'));
    const confirmed = await confirmModal(`Delete ${id}?`, body, deps.incoming.length ? 'Force Delete' : 'Delete', 'danger');
    if (!confirmed) return;
    await api.deleteRecord(id, deps.incoming.length > 0);
    const { removeRecordFromCaches } = await import('./state.js');
    removeRecordFromCaches(id);
    await refreshTypeRecords(record.typeId);
    const validation = await api.validation();
    applyValidation(validation);
    toast('Deleted', id, 'ok');
    if (location.hash.includes(encodeURIComponent(id))) location.hash = `#/type/${record.typeId}`;
    emit('records', { typeId: record.typeId });
  } catch (error) {
    toast('Delete failed', error.message, 'err');
  }
}
