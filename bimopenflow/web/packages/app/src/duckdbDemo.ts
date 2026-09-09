// The DuckDB demo: the shared graph-demo shell (same as 3d.html) over the
// nine sample workflows, plus an Ask box. Typing a request there posts it to
// the studio host's /api/ask, which has an agent build the graph through the
// MCP tools; the transcript streams in below the top bar and the new graph
// opens when it is done.
import { ApiClient } from '@bimopenflow/api-client';
import { createApp, type App } from './app.js';
import workflows from '../../../../../samples/duckdb-analyses/workflows.json';
import './duckdbDemo.css';

const START = 'Run `npm run duckdb:host --prefix bimopenflow/web`, then reload this page.';
const EXAMPLES = [
  'How many rooms are on each storey? Sort by count, largest first.',
  'A room schedule: room number, name, storey and floor area, sorted by storey then room number.',
  'Which door types are used most often? Show the top ten with counts.',
  'List every roof with its name, storey and area, and flag which ones have no area.',
  'Which source documents contributed elements, and how many elements came from each?',
  'For every table in the database, how many rows does it have? Only tables with more than 100 rows, largest first.',
];
const root = document.querySelector<HTMLDivElement>('#duckdb-demo')!;
root.innerHTML = `<div id="duck-error" role="alert" hidden></div><div id="duck-ask-log" hidden></div><div id="duck-editor"></div>`;
const editor = root.querySelector<HTMLElement>('#duck-editor')!;
const error = root.querySelector<HTMLElement>('#duck-error')!;
const log = root.querySelector<HTMLElement>('#duck-ask-log')!;
const api = new ApiClient();
let app: App | undefined;
let downloadUrl: string | undefined;
/** The graph the last Ask built; a follow-up continues its conversation. */
let lastAskId: string | undefined;
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

// ── Ask ──────────────────────────────────────────────────────────────────────

interface AskEvent {
  type: 'start' | 'tool' | 'text' | 'check' | 'done' | 'error';
  verified?: boolean;
  problem?: string | null;
  analysisId?: string;
  model?: string;
  name?: string;
  args?: Record<string, unknown> | null;
  ok?: boolean | null;
  summary?: string | null;
  text?: string | null;
  message?: string;
  built?: boolean;
  turns?: number;
  inputTokens?: number;
  outputTokens?: number;
}

function line(kind: string, text: string): HTMLElement {
  const item = document.createElement('div');
  item.className = `duck-ask-line duck-ask-${kind}`;
  item.textContent = text;
  log.append(item);
  log.scrollTop = log.scrollHeight;
  return item;
}

function shortArgs(args: Record<string, unknown> | null | undefined): string {
  if (!args) return '';
  const shown = Object.entries(args).filter(([key]) => key !== 'id')
    .map(([key, value]) => `${key}=${typeof value === 'string' ? JSON.stringify(value) : String(value)}`).join(', ');
  return shown.length > 140 ? shown.slice(0, 140) + '…' : shown;
}

async function readEvents(response: Response, onEvent: (event: AskEvent) => Promise<void> | void) {
  const reader = response.body!.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    let end: number;
    while ((end = buffer.indexOf('\n\n')) >= 0) {
      const chunk = buffer.slice(0, end);
      buffer = buffer.slice(end + 2);
      const data = chunk.split('\n').filter(l => l.startsWith('data: ')).map(l => l.slice(6)).join('\n');
      if (data) await onEvent(JSON.parse(data) as AskEvent);
    }
  }
}

async function ask(request: string, form: HTMLFormElement, followUp: HTMLInputElement) {
  const controls = form.querySelectorAll<HTMLInputElement | HTMLButtonElement>('input, button');
  controls.forEach(control => { control.disabled = true; });
  const continuing = followUp.checked && lastAskId ? lastAskId : undefined;
  log.hidden = false;
  if (!continuing) log.replaceChildren();
  line('you', `You: ${request}`);
  let analysisId: string | undefined;
  try {
    const response = await fetch('/api/ask', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(continuing ? { request, analysisId: continuing } : { request }),
    });
    if (!response.ok || !response.body) throw new Error(`${response.status} ${response.statusText}`);
    await readEvents(response, async event => {
      switch (event.type) {
        case 'start':
          analysisId = event.analysisId;
          lastAskId = event.analysisId;
          line('note', `${continuing ? 'Continuing' : 'Building'} "${event.analysisId}" with ${event.model}…`);
          break;
        case 'tool':
          line(event.ok ? 'tool' : 'bad', `→ ${event.name}(${shortArgs(event.args)})${event.summary ? `  · ${event.summary}` : ''}`);
          break;
        case 'text':
          line('agent', `Agent: ${event.text}`);
          break;
        case 'check':
          line('bad', `Check: ${event.summary}`);
          break;
        case 'done': {
          const verdict = event.built ? (event.verified ? 'Done, checked' : `Done, unverified (${event.problem ?? 'see above'})`) : 'Answered';
          line(event.built && !event.verified ? 'bad' : 'done', `${verdict} in ${event.turns} turns (${event.inputTokens} in, ${event.outputTokens} out): ${event.text}`);
          if (event.built && event.analysisId && app) {
            await app.refreshAnalyses();
            await app.openAnalysis(event.analysisId);
          }
          break;
        }
        case 'error':
          line('bad', `Error: ${event.message}`);
          break;
      }
    });
  } catch (cause) {
    line('bad', `Error: ${String(cause)}${analysisId ? ` (graph ${analysisId} may be partial)` : ''}`);
  } finally {
    controls.forEach(control => { control.disabled = false; });
    followUp.disabled = !lastAskId;
    followUp.parentElement!.title = lastAskId ? `Continue the conversation about ${lastAskId}` : 'Ask something first';
  }
}

function mountAsk() {
  const form = document.createElement('form');
  form.className = 'duck-ask';
  const input = document.createElement('input');
  input.type = 'text';
  input.placeholder = 'Describe the graph you want…';
  input.setAttribute('list', 'duck-ask-examples');
  input.setAttribute('aria-label', 'Ask for a graph');
  input.autocomplete = 'off';
  const examples = document.createElement('datalist');
  examples.id = 'duck-ask-examples';
  for (const example of EXAMPLES) {
    const option = document.createElement('option');
    option.value = example;
    examples.append(option);
  }
  const button = document.createElement('button');
  button.type = 'submit';
  button.textContent = 'Ask';
  button.title = 'Have an agent build this graph through the MCP tools';
  const followUpLabel = document.createElement('label');
  followUpLabel.className = 'duck-ask-followup';
  followUpLabel.title = 'Ask something first';
  const followUp = document.createElement('input');
  followUp.type = 'checkbox';
  followUp.disabled = true;
  followUp.setAttribute('aria-label', 'Follow up on the last graph');
  followUpLabel.append(followUp, ' follow-up');
  form.append(input, examples, button, followUpLabel);
  // With the suggestion list open, Enter would only close it; submit outright.
  input.addEventListener('keydown', event => {
    if (event.key === 'Enter') { event.preventDefault(); form.requestSubmit(); }
  });
  form.addEventListener('submit', event => {
    event.preventDefault();
    const request = input.value.trim();
    if (request) void ask(request, form, followUp);
  });
  editor.querySelector('.bof-app-conn')!.before(form);
  void fetch('/api/ask/model').then(async response => {
    if (!response.ok) throw new Error('This host has no /api/ask; start the studio host (npm run duckdb:host).');
    const info = await response.json() as { model: string; configured: boolean; problem: string | null };
    input.title = `Model: ${info.model}`;
    if (!info.configured) { log.hidden = false; line('bad', info.problem ?? 'The OpenAI key is not configured.'); }
  }).catch(cause => { log.hidden = false; line('bad', `Ask is unavailable: ${cause instanceof Error ? cause.message : String(cause)}`); });
}

async function start() {
  try {
    const available = await api.listAnalyses();
    if (workflows.some(workflow => !available.some(item => item.id === workflow.id)))
      throw new Error('Demo workflows are missing.');
    app = createApp(editor, api, { graphDemo: true, tableOnly: true, autoLayout: true, initialAnalysis: workflows[0]!.id, heading: 'BimOpenFlow · Snowdon DuckDB' });
    mountAsk();
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
