// The `analysis` query parameter shared by the editor page (index.html) and
// the 3D demo page (3d.html): both open the analysis it names.

export const DEFAULT_ANALYSIS = "snowdon-toolkit";

/** The `analysis` query value when present and non-blank, else undefined. */
export const analysisParam = (search: string): string | undefined =>
  new URLSearchParams(search).get("analysis")?.trim() || undefined;

/** The `analysis` query value when present and non-blank, else the default. */
export const analysisFromSearch = (search: string, fallback = DEFAULT_ANALYSIS): string =>
  analysisParam(search) ?? fallback;

/** `search` with `analysis` set to `id`, every other parameter kept. */
export const searchWithAnalysis = (search: string, id: string): string => {
  const params = new URLSearchParams(search);
  params.set("analysis", id);
  return `?${params}`;
};

/** What boot opens: the requested analysis when the host has it, else the
 *  first one; `missing` names a requested id the host lacks. */
export const chooseInitialAnalysis = (
  requested: string | undefined,
  ids: readonly string[],
): { open: string | undefined; missing: string | undefined } =>
  requested && ids.includes(requested)
    ? { open: requested, missing: undefined }
    : { open: ids[0], missing: requested };
