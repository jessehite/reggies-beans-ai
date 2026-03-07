#!/usr/bin/env node
// render-runs.js — converts workflow run JSON files to readable HTML
// Usage: node render-runs.js [runs-dir] [output-dir]
//   runs-dir defaults to ../runs relative to this script
//   output-dir defaults to <runs-dir>/html

const fs = require('fs');
const path = require('path');

const runsDir = process.argv[2] || path.join(__dirname, '..', 'runs');
const outDir  = process.argv[3] || path.join(runsDir, 'html');

fs.mkdirSync(outDir, { recursive: true });

// ── enum labels ────────────────────────────────────────────────────────────
const WORKFLOW_STATUS = ['Created','Running','Waiting for Input','Completed','Failed','Cancelled'];
const STAGE_STATUS    = ['Pending','Running','Completed','Failed','Skipped'];

function wfBadge(n)  { return badge(WORKFLOW_STATUS[n] ?? n, badgeClass(WORKFLOW_STATUS[n] ?? '')); }
function stBadge(n)  { return badge(STAGE_STATUS[n]    ?? n, badgeClass(STAGE_STATUS[n]    ?? '')); }

function badgeClass(label) {
  switch (label) {
    case 'Completed': return 'green';
    case 'Failed':    return 'red';
    case 'Running':   return 'blue';
    case 'Waiting for Input': return 'yellow';
    case 'Skipped':   return 'gray';
    default:          return 'gray';
  }
}
function badge(text, cls) {
  return `<span class="badge badge-${cls}">${esc(text)}</span>`;
}

// ── helpers ────────────────────────────────────────────────────────────────
function esc(s) {
  return String(s ?? '')
    .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')
    .replace(/"/g,'&quot;');
}

function fmtDate(s) {
  if (!s) return '—';
  const d = new Date(s);
  return d.toLocaleString('en-US', { dateStyle:'medium', timeStyle:'short' });
}

function prettyJson(raw) {
  if (!raw) return null;
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

function stageIdLabel(id) {
  return id.replace(/-/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
}

function duration(start, end) {
  if (!start || !end) return null;
  const ms = new Date(end) - new Date(start);
  if (ms < 1000) return `${ms}ms`;
  return `${(ms/1000).toFixed(1)}s`;
}

// ── JSON block renderer ────────────────────────────────────────────────────
function jsonSection(title, raw, open = false) {
  if (!raw) return '';
  const pretty = prettyJson(raw);
  // Try to render structured output as readable cards
  let structured = '';
  try {
    const obj = JSON.parse(raw);
    structured = renderValue(obj, 0);
  } catch { /* ignore */ }

  const id = `json-${Math.random().toString(36).slice(2)}`;
  return `
<div class="json-section">
  <div class="json-tabs">
    <button class="tab-btn active" onclick="showTab('${id}','structured')">Readable</button>
    <button class="tab-btn" onclick="showTab('${id}','raw')">Raw JSON</button>
  </div>
  <div id="${id}-structured" class="tab-pane">${structured || '<em class="muted">No structured view available.</em>'}</div>
  <div id="${id}-raw" class="tab-pane hidden"><pre class="json-pre">${esc(pretty)}</pre></div>
</div>`;
}

function renderValue(val, depth) {
  if (val === null || val === undefined) return '<span class="null">null</span>';
  if (typeof val === 'boolean') return `<span class="bool">${val}</span>`;
  if (typeof val === 'number') return `<span class="num">${val}</span>`;
  if (typeof val === 'string') return renderString(val);
  if (Array.isArray(val)) return renderArray(val, depth);
  if (typeof val === 'object') return renderObject(val, depth);
  return esc(String(val));
}

function renderString(s) {
  // Long text gets a paragraph; short values get inline span
  if (s.length > 120) {
    return `<p class="text-val">${esc(s)}</p>`;
  }
  return `<span class="str">${esc(s)}</span>`;
}

function renderArray(arr, depth) {
  if (arr.length === 0) return '<span class="muted">(empty list)</span>';

  // Simple scalar arrays → inline
  if (arr.every(x => typeof x !== 'object' || x === null)) {
    return '<ul class="scalar-list">' +
      arr.map(x => `<li>${renderValue(x, depth+1)}</li>`).join('') +
      '</ul>';
  }

  // Object arrays → cards
  return '<div class="card-list">' +
    arr.map((item, i) => {
      const inner = (typeof item === 'object' && item !== null)
        ? renderObject(item, depth+1, false)
        : renderValue(item, depth+1);
      return `<div class="card">${inner}</div>`;
    }).join('') +
    '</div>';
}

function renderObject(obj, depth, wrapTable = true) {
  const entries = Object.entries(obj);
  if (entries.length === 0) return '<span class="muted">(empty)</span>';

  let html = wrapTable ? '<table class="kv-table">' : '<table class="kv-table kv-nested">';
  for (const [k, v] of entries) {
    const label = k.replace(/([A-Z])/g,' $1').replace(/^./,c=>c.toUpperCase());
    const isComplex = typeof v === 'object' && v !== null;
    html += `<tr>
      <th>${esc(label)}</th>
      <td>${renderValue(v, depth+1)}</td>
    </tr>`;
  }
  html += '</table>';
  return html;
}

// ── per-run HTML ───────────────────────────────────────────────────────────
function renderRun(run, filename) {
  const wfLabel = (run.workflowId ?? '').replace(/-/g,' ').replace(/\b\w/g,c=>c.toUpperCase());
  const dur = duration(run.createdAt, run.completedAt);

  const stagesHtml = (run.stages ?? []).map((stage, i) => {
    const stNum = stage.status ?? 0;
    const label = stageIdLabel(stage.stageId ?? `Stage ${i+1}`);
    const stageDur = duration(stage.startedAt, stage.completedAt);

    return `
<section class="stage stage-status-${stNum}">
  <div class="stage-header">
    <div class="stage-title">
      <span class="stage-num">${String(i+1).padStart(2,'0')}</span>
      <span class="stage-name">${esc(label)}</span>
    </div>
    <div class="stage-meta">
      ${stBadge(stNum)}
      ${stageDur ? `<span class="duration">${esc(stageDur)}</span>` : ''}
      ${stage.attemptCount > 1 ? `<span class="badge badge-yellow">${stage.attemptCount} attempts</span>` : ''}
    </div>
  </div>
  ${stage.startedAt ? `<div class="stage-dates">
    <span>Started: ${fmtDate(stage.startedAt)}</span>
    ${stage.completedAt ? `<span>Completed: ${fmtDate(stage.completedAt)}</span>` : ''}
  </div>` : ''}
  ${stage.error ? `<div class="stage-error"><strong>Error:</strong> ${esc(stage.error)}</div>` : ''}
  ${stage.inputJson ? `
  <details class="io-block" open>
    <summary>Input</summary>
    ${jsonSection('Input', stage.inputJson, true)}
  </details>` : ''}
  ${stage.outputJson ? `
  <details class="io-block" ${stNum === 2 ? 'open' : ''}>
    <summary>Output</summary>
    ${jsonSection('Output', stage.outputJson, false)}
  </details>` : ''}
</section>`;
  }).join('\n');

  return html(`
<div class="run-header">
  <div>
    <h1>${esc(wfLabel)}</h1>
    <div class="run-id">Run ID: <code>${esc(run.runId)}</code></div>
  </div>
  <div class="run-status-block">
    ${wfBadge(run.status ?? 0)}
    ${dur ? `<div class="duration">Total: ${esc(dur)}</div>` : ''}
  </div>
</div>

<div class="run-dates card">
  <div><strong>Created:</strong> ${fmtDate(run.createdAt)}</div>
  ${run.completedAt ? `<div><strong>Completed:</strong> ${fmtDate(run.completedAt)}</div>` : ''}
  <div><strong>Progress:</strong> Stage ${(run.currentStageIndex ?? 0) + 1} of ${(run.stages ?? []).length}</div>
</div>

<div class="stages">
${stagesHtml}
</div>

<p class="back-link"><a href="index.html">&larr; All Runs</a></p>
`, `${esc(wfLabel)} — ${esc(run.runId.slice(0,8))}`);
}

// ── index HTML ─────────────────────────────────────────────────────────────
function renderIndex(runs) {
  const rows = runs.map(({ run, htmlFile }) => {
    const wfLabel = (run.workflowId ?? '').replace(/-/g,' ').replace(/\b\w/g,c=>c.toUpperCase());
    const total = (run.stages ?? []).length;
    const done  = (run.stages ?? []).filter(s => s.status === 2).length;
    const dur = duration(run.createdAt, run.completedAt);
    return `
<tr>
  <td><a href="${esc(htmlFile)}">${esc(run.runId.slice(0,8))}…</a></td>
  <td>${esc(wfLabel)}</td>
  <td>${wfBadge(run.status ?? 0)}</td>
  <td>${done}/${total}</td>
  <td>${fmtDate(run.createdAt)}</td>
  <td>${dur ? esc(dur) : '—'}</td>
</tr>`;
  }).join('');

  return html(`
<h1>Workflow Runs</h1>
<p class="muted">${runs.length} run${runs.length !== 1 ? 's' : ''} found</p>
<table class="index-table">
  <thead>
    <tr>
      <th>Run ID</th><th>Workflow</th><th>Status</th><th>Stages</th><th>Created</th><th>Duration</th>
    </tr>
  </thead>
  <tbody>${rows}</tbody>
</table>
`, 'All Workflow Runs');
}

// ── HTML shell ─────────────────────────────────────────────────────────────
function html(body, title) {
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>${esc(title)}</title>
<style>
  :root {
    --bg: #0f1117; --surface: #1a1d27; --surface2: #22263a;
    --border: #2e3350; --text: #e2e8f0; --muted: #8892aa;
    --green: #34d399; --red: #f87171; --blue: #60a5fa;
    --yellow: #fbbf24; --gray: #6b7280; --purple: #a78bfa;
    --radius: 8px; --font: 'Segoe UI', system-ui, sans-serif;
    --mono: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { background: var(--bg); color: var(--text); font-family: var(--font);
         font-size: 15px; line-height: 1.6; padding: 2rem; }
  a { color: var(--blue); text-decoration: none; }
  a:hover { text-decoration: underline; }
  h1 { font-size: 1.8rem; font-weight: 700; margin-bottom: .5rem; }
  h2 { font-size: 1.2rem; font-weight: 600; margin-bottom: .5rem; }
  code { font-family: var(--mono); font-size: .85em; }

  .badge { display: inline-block; padding: .2em .7em; border-radius: 99px;
           font-size: .78rem; font-weight: 600; letter-spacing: .03em; }
  .badge-green  { background: #064e3b; color: var(--green); }
  .badge-red    { background: #450a0a; color: var(--red); }
  .badge-blue   { background: #1e3a5f; color: var(--blue); }
  .badge-yellow { background: #422006; color: var(--yellow); }
  .badge-gray   { background: #1f2937; color: var(--gray); }

  .muted { color: var(--muted); font-size: .9em; }
  .duration { color: var(--muted); font-size: .85rem; margin-top: .2rem; }

  /* run header */
  .run-header { display: flex; justify-content: space-between; align-items: flex-start;
                gap: 1rem; margin-bottom: 1.5rem; flex-wrap: wrap; }
  .run-id { color: var(--muted); font-size: .85rem; margin-top: .3rem; }
  .run-status-block { text-align: right; }
  .run-dates { display: flex; gap: 2rem; flex-wrap: wrap; margin-bottom: 1.5rem; }
  .run-dates > div { font-size: .9rem; }

  .card { background: var(--surface); border: 1px solid var(--border);
          border-radius: var(--radius); padding: 1rem 1.25rem; }
  .card-list { display: flex; flex-direction: column; gap: .75rem; }
  .card-list > .card { background: var(--surface2); }

  /* stages */
  .stages { display: flex; flex-direction: column; gap: 1rem; }
  .stage { background: var(--surface); border: 1px solid var(--border);
           border-radius: var(--radius); overflow: hidden; }
  .stage-status-2 { border-left: 4px solid var(--green); }
  .stage-status-3 { border-left: 4px solid var(--red); }
  .stage-status-1 { border-left: 4px solid var(--blue); }
  .stage-status-4 { border-left: 4px solid var(--gray); }
  .stage-status-0 { border-left: 4px solid var(--gray); opacity: .6; }

  .stage-header { display: flex; justify-content: space-between; align-items: center;
                  padding: .85rem 1.25rem; gap: 1rem; flex-wrap: wrap; }
  .stage-title { display: flex; align-items: center; gap: .75rem; }
  .stage-num { font-family: var(--mono); font-size: .8rem; color: var(--muted);
               background: var(--surface2); padding: .15em .5em;
               border-radius: 4px; }
  .stage-name { font-weight: 600; font-size: 1rem; }
  .stage-meta { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
  .stage-dates { padding: 0 1.25rem .5rem; color: var(--muted); font-size: .82rem;
                 display: flex; gap: 1.5rem; }
  .stage-error { margin: .5rem 1.25rem 1rem; background: #2d0a0a; border: 1px solid #7f1d1d;
                 border-radius: 6px; padding: .75rem 1rem; font-size: .88rem;
                 color: var(--red); }

  /* io blocks */
  .io-block { margin: 0 1.25rem 1rem; }
  .io-block summary { cursor: pointer; color: var(--muted); font-size: .88rem;
                      font-weight: 600; padding: .3rem 0; user-select: none; }
  .io-block summary:hover { color: var(--text); }

  /* json tabs */
  .json-section { margin-top: .5rem; }
  .json-tabs { display: flex; gap: .5rem; margin-bottom: .5rem; }
  .tab-btn { background: var(--surface2); border: 1px solid var(--border);
             color: var(--muted); border-radius: 6px; padding: .3em .9em;
             font-size: .8rem; cursor: pointer; }
  .tab-btn.active { background: var(--border); color: var(--text); }
  .tab-pane.hidden { display: none; }

  /* structured view */
  .kv-table { width: 100%; border-collapse: collapse; font-size: .88rem; }
  .kv-table th { text-align: left; color: var(--muted); padding: .4rem .75rem .4rem 0;
                 font-weight: 500; white-space: nowrap; vertical-align: top;
                 min-width: 140px; }
  .kv-table td { padding: .4rem 0; vertical-align: top; }
  .kv-nested { margin-left: 1rem; }
  .scalar-list { padding-left: 1.25rem; }
  .scalar-list li { margin: .2rem 0; }
  .str  { color: var(--text); }
  .num  { color: var(--purple); font-family: var(--mono); }
  .bool { color: var(--yellow); font-family: var(--mono); }
  .null { color: var(--gray); font-family: var(--mono); }
  .text-val { margin: .2rem 0; line-height: 1.5; }

  /* raw JSON */
  .json-pre { background: #0a0c14; border: 1px solid var(--border);
              border-radius: 6px; padding: 1rem; overflow-x: auto;
              font-family: var(--mono); font-size: .82rem; line-height: 1.6;
              max-height: 500px; overflow-y: auto; white-space: pre; }

  /* index table */
  .index-table { width: 100%; border-collapse: collapse; margin-top: 1.25rem; }
  .index-table th { text-align: left; padding: .6rem .75rem;
                    border-bottom: 2px solid var(--border); color: var(--muted);
                    font-size: .85rem; font-weight: 600; }
  .index-table td { padding: .6rem .75rem; border-bottom: 1px solid var(--border);
                    font-size: .9rem; }
  .index-table tr:hover td { background: var(--surface); }

  .back-link { margin-top: 2rem; font-size: .9rem; }
</style>
</head>
<body>
${body}
<script>
function showTab(id, pane) {
  ['structured','raw'].forEach(p => {
    const el = document.getElementById(id + '-' + p);
    if (el) el.classList.toggle('hidden', p !== pane);
  });
  const parent = document.getElementById(id + '-structured')?.closest('.json-section');
  if (parent) parent.querySelectorAll('.tab-btn').forEach(btn => {
    btn.classList.toggle('active', btn.textContent.toLowerCase().startsWith(pane === 'raw' ? 'raw' : 'read'));
  });
}
</script>
</body>
</html>`;
}

// ── main ───────────────────────────────────────────────────────────────────
const files = fs.readdirSync(runsDir).filter(f => f.endsWith('.json'));
console.log(`Found ${files.length} run files in ${runsDir}`);

const index = [];

for (const file of files) {
  const src = path.join(runsDir, file);
  let run;
  try {
    run = JSON.parse(fs.readFileSync(src, 'utf8'));
  } catch (e) {
    console.warn(`  Skipping ${file}: ${e.message}`);
    continue;
  }

  const htmlFile = file.replace(/\.json$/, '.html');
  const dest = path.join(outDir, htmlFile);
  fs.writeFileSync(dest, renderRun(run, file));
  console.log(`  Written: html/${htmlFile}`);
  index.push({ run, htmlFile });
}

// sort index by createdAt descending
index.sort((a, b) => new Date(b.run.createdAt ?? 0) - new Date(a.run.createdAt ?? 0));

fs.writeFileSync(path.join(outDir, 'index.html'), renderIndex(index));
console.log(`  Written: html/index.html`);
console.log(`\nDone. Open: ${path.join(outDir, 'index.html')}`);
