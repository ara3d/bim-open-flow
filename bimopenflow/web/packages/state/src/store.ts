import type { Action } from "./actions.js";
import { initialState, reduce, type State } from "./reducer.js";

export interface Store {
  getState(): State;
  /** The single choke point for every graph mutation (P2). */
  dispatch(action: Action): void;
  /** Notified at most once per dispatch (only when the state changed). */
  subscribe(listener: () => void): () => void;
}

export function createStore(initial: State = initialState): Store {
  let state = initial;
  const listeners = new Set<() => void>();
  return {
    getState: () => state,
    dispatch: (action: Action) => {
      const next = reduce(state, action);
      if (next === state) return;
      state = next;
      for (const listener of [...listeners]) listener();
    },
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}
