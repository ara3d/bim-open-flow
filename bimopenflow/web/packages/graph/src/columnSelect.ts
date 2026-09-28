import type { SuggestionList } from '@bimopenflow/contracts';

interface Entry {
  el: HTMLDivElement; select: HTMLSelectElement; direction: HTMLButtonElement;
  value: string; descending: boolean; values: string[]; ready: boolean; ticket: number;
  refresh(): Promise<void>; render(): void;
}

/** Live column choices. Requests are invalidated on schema changes, flow switches and disposal. */
export class ColumnSelects {
  private entries = new Map<string, Entry>();
  constructor(private fetch: (node: string, param: string) => Promise<SuggestionList>,
    private commit: (node: string, param: string, value: string) => void) {}

  get(node: string, param: string, value: string, descending: boolean): HTMLDivElement {
    const key = `${node}::${param}`;
    let entry = this.entries.get(key);
    if (!entry) {
      const el = document.createElement('div');
      el.style.cssText = 'display:flex;gap:4px;width:100%;height:100%;font:14px system-ui';
      const select = document.createElement('select');
      select.setAttribute('aria-label', `${node} ${param}`);
      select.style.cssText = 'flex:1;min-width:0;font:inherit;border:1px solid #aaa;border-radius:4px;padding:0 6px';
      const direction = document.createElement('button');
      direction.setAttribute('aria-label', `${node} ${param} direction`);
      direction.style.cssText = 'width:30px;flex-shrink:0;border:1px solid #aaa;border-radius:4px;font:inherit;cursor:pointer';
      el.append(select, direction);
      entry = { el, select, direction, value, descending, values: [], ready: false, ticket: 0,
        render: () => {}, refresh: async () => {} };
      const state = entry;
      state.render = () => {
        select.replaceChildren(new Option('— None —', ''), ...state.values.map(name => new Option(name, name)));
        if (state.value && !state.values.includes(state.value))
          select.append(new Option(`${state.value} (unavailable)`, state.value));
        select.value = state.value;
        select.disabled = !state.ready;
        direction.textContent = state.descending ? '↓' : '↑';
        direction.title = state.descending ? 'Descending' : 'Ascending';
        direction.setAttribute('aria-pressed', String(state.descending));
      };
      state.refresh = async () => {
        const ticket = ++state.ticket;
        state.ready = false;
        state.values = [];
        state.render();
        try {
          const result = await this.fetch(node, param);
          if (ticket !== state.ticket) return;
          state.ready = result.status === 'Ok';
          state.values = state.ready ? result.values.map(option => option.value) : [];
          select.title = result.reason ?? '';
        } catch (error) {
          if (ticket !== state.ticket) return;
          select.title = String(error);
        }
        state.render();
      };
      select.addEventListener('change', () => this.commit(node, param, select.value));
      direction.addEventListener('click', () => this.commit(node, 'descending' + param, String(!state.descending)));
      select.addEventListener('focus', () => { if (!state.ready) void state.refresh(); });
      this.entries.set(key, state);
      void state.refresh();
    }
    if (entry.value !== value || entry.descending !== descending) {
      entry.value = value; entry.descending = descending; entry.render();
    }
    return entry.el;
  }

  refresh(): void { for (const entry of this.entries.values()) void entry.refresh(); }
  prune(keys: ReadonlySet<string>): void {
    for (const [key, entry] of this.entries) if (!keys.has(key)) {
      entry.ticket++; entry.el.remove(); this.entries.delete(key);
    }
  }
}
