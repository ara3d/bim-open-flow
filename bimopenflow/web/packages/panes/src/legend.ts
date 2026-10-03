// One entry of a colour legend, shared by the chart pane and the 3D pane
// (@bimopenflow/pane-3d), which hands its viewer's category legend over in
// this shape. The colour is linear RGB in 0..1, the viewer's Vec3.

export type Vec3 = readonly [number, number, number];

export type LegendEntry = { name: string; color: Vec3; count: number };
