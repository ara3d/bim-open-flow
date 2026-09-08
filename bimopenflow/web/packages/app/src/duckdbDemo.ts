import { ApiClient } from '@bimopenflow/api-client';
import { createApp, type App } from './app.js';
import workflows from '../../../../../samples/duckdb-analyses/workflows.json';
import './duckdbDemo.css';

const root = document.querySelector<HTMLDivElement>('#duckdb-demo')!;
root.innerHTML = `
  <header class="duck-header">
    <div class="duck-brand"><span class="duck-symbol" aria-hidden="true">B/</span><div><strong>BIM FLOW</strong><span>WORKFLOW STUDIO</span></div></div>
    <div class="duck-source"><span class="duck-dot"></span> SNOWDON TOWERS <span class="duck-source-type">TYPED DUCKDB</span></div>
    <a href="/duckdb.html">Explore the data ↗</a>
  </header>
  <section class="duck-intro">
    <div><p class="duck-eyebrow">BUILDING DATA, CONNECTED</p><h1>From model facts to useful answers.</h1></div>
    <p>Nine working node graphs. One source database.<br>Select a workflow, then click any node to inspect its results.</p>
  </section>
  <nav class="duck-workflows" aria-label="Sample workflows"></nav>
  <section class="duck-context"><div><span id="duck-tag"></span><h2 id="duck-title"></h2><p id="duck-description"></p></div><button id="duck-download" type="button">Download graph ↓</button></section>
  <div id="duck-error" role="alert" hidden></div>
  <main id="duck-editor" aria-label="Graph and results workspace"></main>
  <footer class="duck-footer"><span>GRAPH ON THE LEFT · RESULTS ON THE RIGHT</span><span>Live C# evaluation · Read-only database queries · NULL means unavailable</span></footer>`;

const editor = root.querySelector<HTMLElement>('#duck-editor')!;
const error = root.querySelector<HTMLElement>('#duck-error')!;
const api = new ApiClient();
let app: App | undefined;
let selected = workflows[0]!;
let opening = false;
let downloadUrl: string | undefined;
const buttons = new Map<string, HTMLButtonElement>();

function showWorkflow(id: string) {
  const workflow = workflows.find(item => item.id === id);
  if (!workflow) return;
  selected = workflow;
  root.querySelector('#duck-tag')!.textContent = workflow.tag;
  root.querySelector('#duck-title')!.textContent = workflow.title;
  root.querySelector('#duck-description')!.textContent = workflow.description;
  for (const [key, button] of buttons) button.setAttribute('aria-pressed', String(key === id));
}

function fail(message: string) {
  error.hidden = false;
  error.textContent = message;
}

for (const [index, workflow] of workflows.entries()) {
  const button = document.createElement('button');
  button.type = 'button';
  button.disabled = true;
  const number = document.createElement('span');
  number.textContent = String(index + 1).padStart(2, '0');
  button.append(number, document.createTextNode(workflow.title));
  button.addEventListener('click', async () => {
    if (!app || opening) return;
    opening = true;
    for (const item of buttons.values()) item.disabled = true;
    error.hidden = true;
    try { await app.openAnalysis(workflow.id); sync(); }
    catch (cause) { fail(String(cause)); }
    finally { opening = false; sync(); }
  });
  buttons.set(workflow.id, button);
  root.querySelector('nav')!.append(button);
}
showWorkflow(selected.id);

function sync() {
  const picker = editor.querySelector<HTMLSelectElement>('select[aria-label="Open flow"]');
  if (picker?.value) showWorkflow(picker.value);
  const connected = editor.querySelector('.bof-app-conn')?.textContent === 'connected';
  for (const button of buttons.values()) button.disabled = !connected || opening;
}
const observer = new MutationObserver(sync);
// Watch connection and flow changes made by the shared editor, including its own picker.
observer.observe(editor, { childList: true, subtree: true, characterData: true });

root.querySelector('#duck-download')!.addEventListener('click', async () => {
  try {
    const document = await api.getAnalysis(selected.id);
    if (downloadUrl) URL.revokeObjectURL(downloadUrl);
    downloadUrl = URL.createObjectURL(new Blob([document], { type: 'application/json' }));
    const link = window.document.createElement('a');
    link.href = downloadUrl;
    link.download = selected.id + '.dfg.json';
    link.click();
  } catch (cause) { fail(`Could not download graph: ${String(cause)}`); }
});

async function start() {
  try {
    const available = await api.listAnalyses();
    const missing = workflows.filter(workflow => !available.some(item => item.id === workflow.id));
    if (missing.length) throw new Error('Demo workflows are missing. Run scripts/start-bim-flow-duckdb.ps1, then reload this page.');
    app = createApp(editor, api, { graphDemo: true, tableOnly: true, autoLayout: true, initialAnalysis: selected.id });
  } catch (cause) { fail(`Could not open the DuckDB demo. ${String(cause)} Start scripts/start-bim-flow-duckdb.ps1 and reload.`); }
}
void start();
window.addEventListener('pagehide', () => {
  observer.disconnect();
  app?.dispose();
  if (downloadUrl) URL.revokeObjectURL(downloadUrl);
}, { once: true });
