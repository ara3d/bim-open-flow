// The main editor's Ask panel (TKT-84): a collapsible box that lets a person
// edit the OPEN flow by typing a request, the same way the DuckDB demo's Ask
// box builds a graph (duckdbDemo.ts), but always aimed at whatever analysis
// the editor has open rather than a freshly named one.
//
// It docks at the top of the right column (the shell's askHost, TKT-112) with
// a fixed height, so a streaming reply scrolls inside its log instead of
// growing the box and pushing the panes down. The height comes from a pref
// and changes only through the drag handle, applied once on release like the
// shell's column splitters so the panes below resize once, not per pixel.
//
// Mounted only when the host answers GET /api/ask/model (see probeAsk): the
// plain BimOpenFlow.Host has no /api/ask, so most editor sessions show
// nothing here at all, and a mount against such a host resolves to a no-op
// handle instead of an empty or broken panel.
import { appendAskLine, postAsk, probeAsk } from './askClient.js';
import { shortArgs } from './askClient.js';
import type { HostStatusSource } from './hostStatus.js';
import { readPref, writePref } from './prefs.js';
import './askPanel.css';

const PREFIX = 'bof-ask';
const HEIGHT_KEY = 'bof-ask-height';
const COLLAPSED_KEY = 'bof-ask-collapsed';
export const ASK_DEFAULT_HEIGHT = 200;
const MIN_HEIGHT = 96;
/** Room the pane area keeps below the panel: its header, tabs, and a body. */
const MIN_PANES = 240;

/** The panel height for a wanted height in a right column `columnHeight`
 *  tall; a column of 0 (not laid out yet) leaves only the lower bound. */
export function clampAskHeight(height: number, columnHeight: number): number {
  const max = columnHeight > 0 ? Math.max(MIN_HEIGHT, columnHeight - MIN_PANES) : Infinity;
  return Math.round(Math.min(max, Math.max(MIN_HEIGHT, height)));
}

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

  const log = doc.createElement('div');
  log.className = 'bof-ask-log';
  log.setAttribute('aria-live', 'polite');

  const form = doc.createElement('form');
  form.className = 'bof-ask-form';
  const toggle = doc.createElement('button');
  toggle.type = 'button';
  toggle.className = 'bof-ask-toggle';
  toggle.title = `Model: ${info.model}${info.provider ? ` (${info.provider})` : ''}`;
  const input = doc.createElement('input');
  input.type = 'text';
  input.placeholder = 'Ask Claude to edit this flow, or about the toolkit…';
  input.setAttribute('aria-label', 'Ask for an edit to the open flow');
  input.autocomplete = 'off';
  const button = doc.createElement('button');
  button.type = 'submit';
  button.textContent = 'Send';
  form.append(toggle, input, button);

  const handle = doc.createElement('div');
  handle.className = 'bof-ask-resize';
  handle.title = 'Drag to resize the Ask panel';
  handle.setAttribute('role', 'separator');
  handle.setAttribute('aria-orientation', 'horizontal');

  panel.append(log, form, handle);

  const columnHeight = () => root.parentElement?.clientHeight ?? 0;
  const setHeight = (height: number) => {
    panel.style.setProperty('--bof-ask-height', `${clampAskHeight(height, columnHeight())}px`);
  };
  const savedHeight = parseFloat(readPref(HEIGHT_KEY) ?? '');
  let height = Number.isFinite(savedHeight) ? savedHeight : ASK_DEFAULT_HEIGHT;
  setHeight(height);

  const setOpen = (open: boolean) => {
    panel.classList.toggle('bof-ask-collapsed', !open);
    toggle.setAttribute('aria-expanded', String(open));
    toggle.textContent = open ? 'Ask ▾' : 'Ask ▸';
    log.hidden = !open;
    handle.hidden = !open;
  };
  setOpen(readPref(COLLAPSED_KEY) !== 'true');
  toggle.addEventListener('click', () => {
    const open = toggle.getAttribute('aria-expanded') !== 'true';
    setOpen(open);
    writePref(COLLAPSED_KEY, String(!open));
    if (open) log.scrollTop = log.scrollHeight;
  });

  handle.addEventListener('pointerdown', down => {
    if (down.button !== 0) return;
    down.preventDefault();
    handle.setPointerCapture(down.pointerId);
    const box = panel.getBoundingClientRect();
    const start = box.height;
    const ghost = doc.createElement('div');
    ghost.className = 'bof-ask-ghost';
    ghost.style.left = `${box.left}px`;
    ghost.style.width = `${box.width}px`;
    ghost.style.top = `${box.bottom}px`;
    doc.body.appendChild(ghost);
    let wanted = start;
    const move = (e: PointerEvent) => {
      wanted = clampAskHeight(start + e.clientY - down.clientY, columnHeight());
      ghost.style.top = `${box.top + wanted}px`;
    };
    const finish = (apply: boolean) => {
      handle.removeEventListener('pointermove', move);
      handle.removeEventListener('pointerup', up);
      handle.removeEventListener('pointercancel', cancel);
      handle.removeEventListener('lostpointercapture', cancel);
      ghost.remove();
      if (!apply) return;
      height = wanted;
      setHeight(height);
      writePref(HEIGHT_KEY, String(Math.round(height)));
    };
    const up = () => finish(true);
    const cancel = () => finish(false);
    handle.addEventListener('pointermove', move);
    handle.addEventListener('pointerup', up);
    handle.addEventListener('pointercancel', cancel);
    handle.addEventListener('lostpointercapture', cancel);
  });

  // A shorter window re-clamps the saved height so the panes keep their room.
  const view = doc.defaultView;
  const onResize = () => setHeight(height);
  view?.addEventListener('resize', onResize);

  if (!info.configured) appendAskLine(log, 'bad', info.problem ?? 'No model key is configured.', PREFIX);

  async function send(request: string): Promise<void> {
    const analysisId = options.getAnalysisId();
    if (!analysisId) {
      options.onError('Open a flow first.');
      return;
    }
    const controls = form.querySelectorAll<HTMLInputElement | HTMLButtonElement>('input, button[type=submit]');
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
  // Laid out only now, so clamp again against the real column height.
  setHeight(height);
  return { dispose() { view?.removeEventListener('resize', onResize); panel.remove(); } };
}
