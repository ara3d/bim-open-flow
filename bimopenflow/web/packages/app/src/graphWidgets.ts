// Reuse the same Gratify widgets as its widget-board demo. These emit normal
// graph parameter intents; they never manipulate the 3D viewer directly.
import { part, rect, v, type Color, type Element } from "gratify";
import { Slider, Range } from "../../../../../submodules/gratify/examples/shared/widgets";
import type { CanvasParam } from "./canvasSlots";
import type { CanvasIntent } from "./canvasIntents";
import { COMPACT_SLOT_H, WIDGET_SLOT_H } from "./canvasSlots";
import { displayScale, numericLimits, numericValue, paramLabel } from "./numericParam";

const Widget = part<{ w: number; label?: string }, { text: Color }>("bof-widget", {
  size: p => v(p.w, WIDGET_SLOT_H),
  arrange: (_p, r, kids) => kids.length === 2
    ? [rect(r.x,r.y,r.w,COMPACT_SLOT_H),rect(r.x,r.y+36,r.w,30)]
    : [rect(r.x,r.y+36,r.w,30)],
  style: t => ({text:t.text}),
  render: (n,p,s) => { if (n.props.label) p.label(n.props.label,v(n.rect.x,n.rect.y+16),s.text,{align:"left",size:14}); },
});

const set = (nodeId: string, name: string, value: string): CanvasIntent => ({kind:"setParam",nodeId,name,value});

export function sliderSlot(nodeId: string, param: CanvasParam, w: number, field: Element): Element {
  const limits = numericLimits(param.kind,param.control);
  const lo = limits.min ?? 0, hi = limits.max ?? 1;
  return Widget(param.name,{w},[field, Slider("slider",{
    width:w, value:(Number(param.value)-lo)/(hi-lo),
    set: fraction => set(nodeId,param.name,numericValue(param.kind,String(lo+fraction*(hi-lo)),param.control) ?? param.value),
  })]);
}

export function rangeSlot(nodeId: string, param: CanvasParam, w: number): Element {
  let value: unknown;
  try { value = JSON.parse(param.value); } catch { value = null; }
  const [min,max] = Array.isArray(value) && value.length === 2 && value.every(Number.isFinite) ? value : [0,1];
  const scale = displayScale(param.control);
  return Widget(param.name,{w,label:`${paramLabel(param.name,param.kind,param.control)}   ${Number((min*scale).toFixed(2))} – ${Number((max*scale).toFixed(2))}`},[
    Range("range",{width:w,min,max,lo:param.control?.min ?? 0,hi:param.control?.max ?? 1,step:param.control?.step ?? .01,
      fmt:value => `${Math.round(value*scale)}${scale === 100 ? "%" : ""}`,
      set:(low,high) => set(nodeId,param.name,JSON.stringify([Number(low.toFixed(8)),Number(high.toFixed(8))])),
    }),
  ]);
}
