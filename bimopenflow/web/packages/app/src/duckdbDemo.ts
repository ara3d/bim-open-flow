// The DuckDB demo: the shared graph-demo shell (same as 3d.html) over the
// nine sample workflows. The only additions are workflow titles in the flow
// picker and a Download button in the topbar.
import { ApiClient } from '@bimopenflow/api-client';
import { createApp, type App } from './app.js';
import workflows from '../../../../../samples/duckdb-analyses/workflows.json';
import './duckdbDemo.css';

const START = 'Run `npm run duckdb:host --prefix bimopenflow/web`, then reload this page.';
const root = document.querySelector<HTMLDivElement>('#duckdb-demo')!;
root.innerHTML = `<div id="duck-error" role="alert" hidden></div><div id="duck-editor"></div>`;
const editor = root.querySelector<HTMLElement>('#duck-editor')!;
const error = root.querySelector<HTMLElement>('#duck-error')!;
const api = new ApiClient();
let app: App | undefined;
let downloadUrl: string | undefined;
const titles = new Map(workflows.map(workflow => [workflow.id, workflow.title]));

function fail(message: string) {
  error.hidden = false;
  error.textContent = message;
}

const picker = () => editor.querySelector<HTMLSelectElement>('select[aria-label="Open flow"]');

// The shared topbar lists flow ids; show the workflow titles instead.
function relabel() {
  for (const option of picker()?.options ?? []) {
    const title = titles.get(option.value);
    if (title && option.textContent !== title) option.textContent = title;
  }
}
const observer = new MutationObserver(relabel);
observer.observe(editor, { childList: true, subtree: true });

async function download() {
  const id = picker()?.value;
  if (!id) return;
  try {
    const document = await api.getAnalysis(id);
    if (downloadUrl) URL.revokeObjectURL(downloadUrl);
    downloadUrl = URL.createObjectURL(new Blob([document], { type: 'application/json' }));
    const link = window.document.createElement('a');
    link.href = downloadUrl;
    link.download = id + '.dfg.json';
    link.click();
  } catch (cause) { fail(`Could not download graph: ${String(cause)}`); }
}

async function start() {
  try {
    const available = await api.listAnalyses();
    if (workflows.some(workflow => !available.some(item => item.id === workflow.id)))
      throw new Error('Demo workflows are missing.');
    app = createApp(editor, api, { graphDemo: true, tableOnly: true, autoLayout: true, initialAnalysis: workflows[0]!.id, heading: 'BimOpenFlow · Snowdon DuckDB' });
    const button = document.createElement('button');
    button.textContent = 'Download';
    button.title = 'Download the current graph as a .dfg.json document';
    button.addEventListener('click', () => void download());
    editor.querySelector('.bof-app-conn')!.before(button);
  } catch (cause) { fail(`Could not open the DuckDB demo. ${String(cause)} ${START}`); }
}
void start();
window.addEventListener('pagehide', () => {
  observer.disconnect();
  app?.dispose();
  if (downloadUrl) URL.revokeObjectURL(downloadUrl);
}, { once: true });
