import type { ControlDescriptor, ParamKind } from "@bimopenflow/contracts";
import { normalizeInteger, normalizeNumber } from "./paramText";

export const isNumericParam = (kind: ParamKind): boolean =>
  ["Integer","Number","Fraction","Percent"].includes(kind);

export const displayScale = (control?: ControlDescriptor): number => control?.unit === "percent" ? 100 : 1;
export const numericDisplay = (text: string, control?: ControlDescriptor): string =>
  text === "" || displayScale(control) === 1 ? text : String(Number((Number(text)*displayScale(control)).toFixed(10)));
export const numericFromDisplay = (text: string, control?: ControlDescriptor): string =>
  text.trim() === "" || displayScale(control) === 1 ? text : String(Number(text)/displayScale(control));
export const paramLabel = (name: string, kind: ParamKind, control?: ControlDescriptor): string => {
  const label = control?.label ?? name.replace(/([a-z])([A-Z])/g,"$1 $2");
  return label.charAt(0).toUpperCase()+label.slice(1)+(kind === "Percent" || control?.unit === "percent" ? " (%)" : "");
};

/** Presentation hints can narrow a type's domain, but can never widen it. */
export function numericLimits(kind: ParamKind, control?: ControlDescriptor) {
  const bounded = kind === "Fraction" || kind === "Percent";
  return {
    min: bounded ? Math.max(0, control?.min ?? 0) : control?.min,
    max: bounded ? Math.min(kind === "Fraction" ? 1 : 100, control?.max ?? Infinity) : control?.max,
    step: control?.step ?? (kind === "Integer" || kind === "Percent" ? 1 : kind === "Fraction" ? .01 : undefined),
  };
}

/** Clamp a completed edit; empty/NaN drafts never enter a bounded graph value. */
export function numericValue(kind: ParamKind, text: string, control?: ControlDescriptor): string | null {
  const canonical = kind === "Integer" ? normalizeInteger(text) : normalizeNumber(text);
  if (canonical === null) return null;
  if (kind === "Integer" && control?.min === undefined && control?.max === undefined && control?.step === undefined) return canonical;
  const {min,max,step} = numericLimits(kind,control);
  if (min === undefined && max === undefined && step === undefined) return canonical;
  let value = Number(canonical);
  value = Math.max(min ?? -Infinity,Math.min(max ?? Infinity,value));
  if (step !== undefined && step > 0) value = Number(((min ?? 0)+Math.round((value-(min ?? 0))/step)*step).toFixed(12));
  value = Math.max(min ?? -Infinity,Math.min(max ?? Infinity,value));
  return Number.isFinite(value) ? String(value) : null;
}
