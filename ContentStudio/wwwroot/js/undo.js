// Session-scoped undo/redo over record edits. Each entry is a full before-image of one
// record; undo pushes the current server value onto the redo stack and PUTs the before-image
// back through the normal edit pipeline (so patches, validation and SSE all behave).

import { api } from './api.js';
import { toast } from './ui.js';
import { findRecord, applyRecordUpdate, applyValidation } from './state.js';
import { editorFor } from './editor.js';

const undoStack = [];
const redoStack = [];
const MaxEntries = 120;

export function pushUndo(recordId, beforeValue) {
  const top = undoStack[undoStack.length - 1];
  // One edit burst produces many field commits; keep only the first before-image per burst.
  if (top && top.recordId === recordId && Date.now() - top.at < 1200) return;
  undoStack.push({ recordId, value: structuredClone(beforeValue), at: Date.now() });
  if (undoStack.length > MaxEntries) undoStack.shift();
  redoStack.length = 0;
}

export async function undo() {
  const entry = undoStack.pop();
  if (!entry) { toast('Nothing to undo', '', 'warn', 1400); return; }
  const current = findRecord(entry.recordId);
  if (current) redoStack.push({ recordId: entry.recordId, value: structuredClone(current.data), at: Date.now() });
  await applyValue(entry, 'Undid');
}

export async function redo() {
  const entry = redoStack.pop();
  if (!entry) { toast('Nothing to redo', '', 'warn', 1400); return; }
  const current = findRecord(entry.recordId);
  if (current) undoStack.push({ recordId: entry.recordId, value: structuredClone(current.data), at: Date.now() });
  await applyValue(entry, 'Redid');
}

async function applyValue(entry, verb) {
  try {
    const payload = await api.saveRecord(entry.recordId, entry.value);
    applyRecordUpdate(entry.recordId, payload);
    const validation = await api.validation();
    applyValidation(validation);
    toast(`${verb} edit`, entry.recordId, 'ok', 1600);
    const editor = editorFor(payload.record.id);
    if (editor) editor.adoptExternal(payload.record);
  } catch (error) {
    toast(`${verb.replace(/d$/, '')} failed`, error.message, 'err');
  }
}
