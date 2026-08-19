// Server-Sent Events wiring: validation refreshes, external file changes and conflicts.

import { emit, applyValidation } from './state.js';
import { api } from './api.js';
import { toast } from './ui.js';

let source = null;
let refreshTimer = null;

export function connectEvents() {
  source?.close();
  source = new EventSource('/api/events');

  source.addEventListener('validation', () => {
    // Debounce: several mutations in a burst produce several events.
    clearTimeout(refreshTimer);
    refreshTimer = setTimeout(async () => {
      try { applyValidation(await api.validation()); } catch { /* transient */ }
      emit('workspace-changed', {});
    }, 200);
  });

  source.addEventListener('file', (event) => {
    const payload = JSON.parse(event.data);
    if (payload.reason === 'conflict') {
      toast('File changed on disk', `${payload.path} — you also have unsaved edits. Resolve in Files.`, 'warn', 7000);
    } else if (payload.reason === 'reloaded') {
      toast('Reloaded from disk', payload.path, 'ok', 2200);
    }
    emit('file-event', payload);
  });

  source.onerror = () => {
    // The browser auto-reconnects; nothing to do.
  };
}
