// The client-side store: metadata, per-type record caches, validation, selection.
// Views subscribe to change topics and re-render themselves.

import { api } from './api.js';

const listeners = new Map(); // topic → Set<fn>

export const state = {
  status: null,            // /api/status payload
  meta: null,              // /api/meta payload (types, schemas, vocabularies)
  typesById: new Map(),
  recordsByType: new Map(),   // typeId → [record payloads]
  recordIndex: new Map(),     // id → record payload (slim: id/name/typeId/file/status)
  validation: { errors: 0, warnings: 0, problems: [] },
  problemsByRecord: new Map(),
  selection: { typeId: null, ids: [] },
};

export function on(topic, handler) {
  if (!listeners.has(topic)) listeners.set(topic, new Set());
  listeners.get(topic).add(handler);
  return () => listeners.get(topic).delete(handler);
}

export function emit(topic, payload) {
  for (const handler of listeners.get(topic) ?? []) {
    try { handler(payload); } catch (error) { console.error(`listener for ${topic} failed`, error); }
  }
}

// ── Loading ───────────────────────────────────────────────────────────────────────────────

export async function loadStatus() {
  state.status = await api.status();
  emit('status', state.status);
  return state.status;
}

export async function loadMeta() {
  state.meta = await api.meta();
  state.typesById = new Map(state.meta.types.map((type) => [type.typeId, type]));
  emit('meta', state.meta);
  return state.meta;
}

export async function loadValidation() {
  const payload = await api.validation();
  applyValidation(payload);
  return payload;
}

export function applyValidation(payload) {
  state.validation = payload;
  state.problemsByRecord = new Map();
  for (const problem of payload.problems ?? []) {
    if (!problem.recordId) continue;
    if (!state.problemsByRecord.has(problem.recordId)) state.problemsByRecord.set(problem.recordId, []);
    state.problemsByRecord.get(problem.recordId).push(problem);
  }
  emit('validation', payload);
}

export async function loadRecords(typeId, force = false) {
  if (!force && state.recordsByType.has(typeId)) return state.recordsByType.get(typeId);
  const payload = await api.records(typeId);
  state.recordsByType.set(typeId, payload.records);
  for (const record of payload.records) state.recordIndex.set(record.id, record);
  emit('records', { typeId });
  return payload.records;
}

/** Loads every type's records (powers global search and cross-type reference pickers). */
export async function loadAllRecords() {
  const types = state.meta?.types ?? [];
  await Promise.all(types.map((type) => loadRecords(type.typeId)));
  emit('all-records-loaded', {});
}

/** Applies a server mutation response (updated record + validation counts) to the caches. */
export function applyRecordUpdate(previousId, payload) {
  const record = payload.record;
  const cached = state.recordsByType.get(record.typeId);
  if (cached) {
    const index = cached.findIndex((candidate) => candidate.id === previousId);
    if (index >= 0) cached[index] = record;
    else cached.push(record);
  }
  if (previousId !== record.id) state.recordIndex.delete(previousId);
  state.recordIndex.set(record.id, record);
  emit('record-updated', { previousId, record });
}

export function removeRecordFromCaches(id) {
  const record = state.recordIndex.get(id);
  if (record) {
    const cached = state.recordsByType.get(record.typeId);
    if (cached) {
      const index = cached.findIndex((candidate) => candidate.id === id);
      if (index >= 0) cached.splice(index, 1);
    }
  }
  state.recordIndex.delete(id);
  emit('record-updated', { previousId: id, record: null });
}

export async function refreshTypeRecords(typeId) {
  await loadRecords(typeId, true);
}

export function problemsOf(id) {
  return state.problemsByRecord.get(id) ?? [];
}

export function typeOf(typeId) {
  return state.typesById.get(typeId);
}

export function recordsOf(typeId) {
  return state.recordsByType.get(typeId) ?? [];
}

export function findRecord(id) {
  return state.recordIndex.get(id) ?? null;
}

/** Every loaded record, for search and pickers. */
export function allRecords() {
  const result = [];
  for (const records of state.recordsByType.values()) result.push(...records);
  return result;
}

// ── Vocabulary lookups (closed sets from Core + data-driven sets) ─────────────────────────

export function vocabList(name) {
  const vocabulary = state.meta?.vocabulary ?? {};
  const dynamicVocabulary = state.meta?.dynamicVocabulary ?? {};
  switch (name) {
    case 'moveTagsAll': return dynamicVocabulary.moveTagsAll ?? [];
    case 'essenceKeys': return dynamicVocabulary.essenceKeys ?? [];
    case 'propertyNames': return dynamicVocabulary.propertyNames ?? [];
    case 'moveOpFields': return dynamicVocabulary.moveOpFields ?? [];
    case 'damageLanesAndPhysical': return dynamicVocabulary.damageLanesAndPhysical ?? [];
    case 'affixFamilies': return dynamicVocabulary.affixFamilies ?? [];
    case 'lootContextTags': return dynamicVocabulary.lootContextTags ?? [];
    case 'materialTags': return dynamicVocabulary.materialTags ?? [];
    case 'damageLanes': return vocabulary.damageLanes ?? [];
    case 'damageAspects': return vocabulary.damageAspects ?? [];
    case 'ruleConditions': return vocabulary.ruleConditions ?? [];
    case 'ruleEffects': return vocabulary.ruleEffects ?? [];
    case 'gameEvents': return vocabulary.gameEvents ?? [];
    case 'moveOps': return vocabulary.moveOps ?? [];
    case 'scopeDimensions': return vocabulary.scopeDimensions ?? [];
    case 'traitCategories': return vocabulary.traitCategories ?? [];
    case 'nameFormats': return vocabulary.nameFormats ?? [];
    case 'courseBonuses': return vocabulary.courseBonusKeys ?? [];
    default: return [];
  }
}

/** Key suggestions for numberDict fields, by schema KeySource. */
export function dictKeys(keySource, context = {}) {
  switch (keySource) {
    case 'properties': return vocabList('propertyNames');
    case 'lanes': return vocabList('damageLanes');
    case 'damageTypes': return ['Slashing', 'Crushing', 'Piercing', 'Magic'];
    case 'essences': return vocabList('essenceKeys');
    case 'attributes': return ['strength', 'dexterity', 'intelligence', 'constitution', 'wisdom', 'endurance', 'luck'];
    case 'traitCategories': return vocabList('traitCategories');
    case 'courseBonuses': return vocabList('courseBonuses');
    case 'modifierKeys': return recordsOf('modifier_keys').map((record) => record.id);
    case 'ownSlots': return context.ownSlots ?? [];
    default: return [];
  }
}
