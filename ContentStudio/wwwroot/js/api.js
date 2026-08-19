// Thin fetch wrappers over the Content Studio API. Every function returns parsed JSON and
// throws an Error carrying the server's message on failure.

async function request(method, url, body) {
  const options = { method, headers: {} };
  if (body !== undefined) {
    options.headers['Content-Type'] = 'application/json';
    options.body = JSON.stringify(body);
  }
  const response = await fetch(url, options);
  const text = await response.text();
  let payload = null;
  try { payload = text ? JSON.parse(text) : null; } catch { payload = { error: text }; }
  if (!response.ok) {
    const error = new Error(payload?.error || `${method} ${url} failed (${response.status})`);
    error.payload = payload;
    error.status = response.status;
    throw error;
  }
  return payload;
}

export const api = {
  status: () => request('GET', '/api/status'),
  openProject: (root) => request('POST', '/api/project', { root }),
  browse: (path) => request('GET', `/api/project/browse?path=${encodeURIComponent(path ?? '')}`),
  meta: () => request('GET', '/api/meta'),

  records: (typeId) => request('GET', `/api/records/${encodeURIComponent(typeId)}`),
  record: (id) => request('GET', `/api/record/${encodeURIComponent(id)}`),
  saveRecord: (id, data) => request('PUT', `/api/record/${encodeURIComponent(id)}`, data),
  saveRecordRaw: (id, text) => request('PUT', `/api/record/${encodeURIComponent(id)}/raw`, { text }),
  createRecord: (typeId, data, targetFile) => request('POST', `/api/records/${encodeURIComponent(typeId)}`, { data, targetFile }),
  duplicateRecord: (id, newId) => request('POST', `/api/record/${encodeURIComponent(id)}/duplicate`, { newId }),
  deleteRecord: (id, force) => request('DELETE', `/api/record/${encodeURIComponent(id)}${force ? '?force=true' : ''}`),
  bulkEdit: (payload) => request('POST', '/api/bulk', payload),

  validation: () => request('GET', '/api/validation'),
  revalidate: () => request('POST', '/api/validate'),

  files: () => request('GET', '/api/files'),
  save: (files, overwriteConflicts) => request('POST', '/api/save', { files: files ?? null, overwriteConflicts: !!overwriteConflicts }),
  revert: (path) => request('POST', '/api/revert', { path }),
  fileDiff: (path) => request('GET', `/api/file/diff?path=${encodeURIComponent(path)}`),
  keepMine: (path) => request('POST', '/api/file/keep-mine', { path }),
  backups: (path) => request('GET', `/api/backups?path=${encodeURIComponent(path)}`),
  restoreBackup: (path, version) => request('POST', '/api/backups/restore', { path, version }),
  open: (path, reveal) => request('POST', '/api/open', { path, reveal: !!reveal }),

  deps: (id) => request('GET', `/api/deps/${encodeURIComponent(id)}`),

  analysisEnemies: () => request('GET', '/api/analysis/enemies'),
  analysisActor: (id) => request('GET', `/api/analysis/actor/${encodeURIComponent(id)}`),
  analysisMoves: () => request('GET', '/api/analysis/moves'),
  analysisProfessions: () => request('GET', '/api/analysis/professions'),
  analysisLootTable: (id, options = {}) =>
    request('GET', `/api/analysis/loot/table/${encodeURIComponent(id)}?${lootQuery(options)}`),
  analysisLootItem: (id, options = {}) =>
    request('GET', `/api/analysis/loot/item/${encodeURIComponent(id)}?${lootQuery(options)}`),
  analysisLootOverview: () => request('GET', '/api/analysis/loot/overview'),
  analysisWarnings: () => request('GET', '/api/analysis/warnings'),
  analysisRealm: (id) => request('GET', `/api/analysis/realm/${encodeURIComponent(id)}`),
};

function lootQuery({ depth = 1, active = true, rank = null }) {
  const parts = [`depth=${depth}`, `active=${active}`];
  if (rank) parts.push(`rank=${encodeURIComponent(rank)}`);
  return parts.join('&');
}
