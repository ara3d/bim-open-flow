import type { Vec3 } from "@bim-open-toolkit/model";

export const categoryPaletteNames = ["classic", "vivid", "pastel", "earth", "grayscale"] as const;
export type CategoryPaletteName = typeof categoryPaletteNames[number];

const colors = (...hex: string[]): readonly Vec3[] => hex.map(color => {
  const channel = (at: number) => parseInt(color.slice(at,at+2),16)/255;
  return [channel(1),channel(3),channel(5)];
});

/** Stable categorical palettes; the unknown-category swatch remains neutral. */
export const categoryPalettes: Readonly<Record<CategoryPaletteName, readonly Vec3[]>> = {
  classic: [[.18,.55,.83],[.94,.55,.22],[.24,.7,.55],[.65,.4,.78],[.85,.36,.45],[.63,.63,.24]],
  vivid: colors("#0077bb","#ee7733","#009988","#cc3311","#aa4499","#33bbee","#eecc66","#4477aa"),
  pastel: colors("#8dd3c7","#ffffb3","#bebada","#fb8072","#80b1d3","#fdb462","#b3de69","#fccde5"),
  earth: colors("#8c510a","#bf812d","#c7a76c","#5e7b4b","#2e665a","#668c91","#806c7a","#b27962"),
  grayscale: colors("#333333","#555555","#777777","#999999","#bbbbbb","#dddddd"),
};
