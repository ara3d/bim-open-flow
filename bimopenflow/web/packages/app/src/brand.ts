// The brand mark, shared by every chrome (docs/BRANDING.md). The path is
// docs/brand/mark.svg: three table rows whose wires gather into one node,
// "Rows to Flow", on a 24 by 24 viewBox, one fill. The fonts are loaded
// by each page's head (a Google Fonts link) and named in the stylesheets.

export const BRAND_MARK_PATH =
  "M2 3h5v4H2ZM2 10h5v4H2ZM2 17h5v4H2ZM8.5 4C12.5 4 11.5 11 15.5 11V13C11.5 13 12.5 6 8.5 6ZM8.5 11h7v2h-7ZM8.5 18C12.5 18 11.5 11 15.5 11V13C11.5 13 12.5 20 8.5 20ZM14.8 12a3.2 3.2 0 1 1 6.4 0a3.2 3.2 0 1 1-6.4 0Z";

/** An inline SVG of a 24-unit single-fill path in the current colour. */
export function svgIcon(doc: Document, path: string, className?: string): SVGSVGElement {
  const svg = doc.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("aria-hidden", "true");
  if (className) svg.setAttribute("class", className);
  const p = doc.createElementNS("http://www.w3.org/2000/svg", "path");
  p.setAttribute("d", path);
  svg.appendChild(p);
  return svg;
}

/** The mark as an inline SVG, sized and coloured by the stylesheet. */
export const brandMark = (doc: Document, className?: string): SVGSVGElement =>
  svgIcon(doc, BRAND_MARK_PATH, className);
