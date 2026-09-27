// The main editor's Ask panel (TKT-84): a compact, collapsible strip that
// lets a person edit the OPEN flow by typing a request, the same way the
// DuckDB demo's Ask box builds a graph (duckdbDemo.ts), but always aimed at
// whatever analysis the editor has open rather than a freshly named one.
//
// Mounted only when the host answers GET /api/ask/model (see probeAsk): the
// plain BimOpenFlow.Host has no /api/ask, so most editor sessions show
// nothing here at all, and a mount against such a host resolves to a no-op
// handle instead of an empty or broken panel.
import { appendAskLine, postAsk, probeAsk } from './askClient.js';
import { shortArgs } from './askClient.js';
import type { HostStatusSource } from './hostStatus.js';
import './askPanel.css';

const PREFIX = 'bof-ask';

export interface AskPanelOptions {
  host: HostStatusSource;
  /** The flow the agent should edit; undefined when nothing is open yet. */
  getAnalysisId(): string | undefined;
  /** Called once the agent finished and the graph changed, so the caller can
   *  reload it (its edits then arrive on the canvas through the normal open). */
  onBuilt(analysisId: string): Promise<void> | void;
  /** A request with no open flow, or a request that failed outright. */
  onError(message: string): void;
}

export interface AskPanel {
  dispose(): void;
}

const NO_PANEL: AskPanel = { dispose() {} };

/** Probes the host for /api/ask/model and, only if it answers, builds the
 *  panel and appends it to `root`; otherwise resolves to a handle whose
 *  dispose does nothing, and nothing is ever added to the page. */
export async function mountAskPanel(root: HTMLElement, options: AskPanelOptions): Promise<AskPanel> {
  const info = await probeAsk(options.host.fetch);
  if (!info) return NO_PANEL;

  const doc = root.ownerDocument;
  const panel = doc.createElement('div');
  panel.className = 'bof-ask-panel';

  const toggle = doc.createElement('button');
  toggle.type = 'button';
  toggle.className = 'bof-ask-toggle';
  toggle.textContent = 'Ask ▾';
  toggle.setAttribute('aria-expanded', 'true');
  toggle.title = `Model: ${info.model}${info.provider ? ` (${info.provider})` : ''}`;

  const body = doc.createElement('div');
  body.className = 'bof-ask-body';

  const log = doc.createElement('div');
  log.className = 'bof-ask-log';

  const form = doc.createElement('form');
  form.className = 'bof-ask-form';
  const input = doc.createElement('input');
  input.type = 'text';
  input.placeholder = 'Ask Claude to edit this flow…';
  input.setAttribute('aria-label', 'Ask for an edit to the open flow');
  input.autocomplete = 'off';
  const button = doc.createElement('button');
  button.type = 'submit';
  button.textContent = 'Send';
  form.append(input, button);

  body.append(log, form);
  panel.append(toggle, body);

  toggle.addEventListener('click', () => {
    const open = toggle.getAttribute('aria-expanded') !== 'true';
    toggle.setAttribute('aria-expanded', String(open));
    toggle.textContent = open ? 'Ask ▾' : 'Ask ▸';
    body.hidden = !open;
  });

  if (!info.configured) appendAskLine(log, 'bad', info.problem ?? 'No model key is configured.', PREFIX);

  async function send(request: string): Promise<void> {
    const analysisId = options.getAnalysisId();
    if (!analysisId) {
      options.onError('Open a flow first.');
      return;
    }
    const controls = form.querySelectorAll<HTMLInputElement | HTMLButtonElement>('input, button');
    controls.forEach(control => { control.disabled = true; });
    appendAskLine(log, 'you', `You: ${request}`, PREFIX);
    let built = false;
    let builtId: string | undefined;
    try {
      await postAsk(options.host.fetch, { request, analysisId }, event => {
        switch (event.type) {
          case 'start':
            appendAskLine(log, 'note', `Editing "${event.analysisId}" with ${event.model}…`, PREFIX);
            break;
          case 'tool':
            appendAskLine(log, event.ok ? 'tool' : 'bad',
              `→ ${event.name}(${shortArgs(event.args)})${event.summary ? `  · ${event.summary}` : ''}`, PREFIX);
            break;
          case 'text':
            appendAskLine(log, 'agent', `Agent: ${event.text}`, PREFIX);
            break;
          case 'check':
            appendAskLine(log, 'bad', `Check: ${event.summary}`, PREFIX);
            break;
          case 'done': {
            const verdict = event.built ? (event.verified ? 'Done, checked' : `Done, unverified (${event.problem ?? 'see above'})`) : 'Answered';
            appendAskLine(log, event.built && !event.verified ? 'bad' : 'done',
              `${verdict} in ${event.turns} turns: ${event.text}`, PREFIX);
            built = !!event.built;
            builtId = event.analysisId;
            break;
          }
          case 'error':
            appendAskLine(log, 'bad', `Error: ${event.message}`, PREFIX);
            break;
        }
      });
      if (built && builtId) await options.onBuilt(builtId);
    } catch (cause) {
      appendAskLine(log, 'bad', `Error: ${String(cause)}`, PREFIX);
    } finally {
      controls.forEach(control => { control.disabled = false; });
    }
  }

  input.addEventListener('keydown', event => {
    if (event.key === 'Enter') { event.preventDefault(); form.requestSubmit(); }
  });
  form.addEventListener('submit', event => {
    event.preventDefault();
    const request = input.value.trim();
    if (!request) return;
    input.value = '';
    void send(request);
  });

  root.append(panel);
  return { dispose() { panel.remove(); } };
}
