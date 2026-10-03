// @bimopenflow/app as a library: the editor for a page to boot. A page picks
// the chrome (the look), the panes, and the start page's templates; the
// toolkit's pages (@bimopenflow/studio-web) add the 3D pane and the samples.
// Helpers an entry page loads before the editor are in "@bimopenflow/app/entry".

export { createApp, AUTOSAVE_MS, primaryNodeId, type App, type AppOptions } from "./app.js";
export { bootEditor, type BootOptions } from "./bootEditor.js";
export type { AppChrome, ChromeActions, ChromeFactory } from "./chrome.js";
export { classicChrome, type ClassicChromeOptions } from "./classicChrome.js";
export { studioChrome, type StudioChromeOptions } from "./studio/studioChrome.js";
export { groupTemplates, type FlowTemplate } from "./templates.js";
