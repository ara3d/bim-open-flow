export interface VizHandle<TData> {
  update(data: TData): void;
  destroy(): void;
}

export interface VizComponent<TData, TOptions> {
  mount(container: HTMLElement, data: TData, options?: TOptions): VizHandle<TData>;
}
