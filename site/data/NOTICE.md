# Notices for the sample buildings

The `.bos` and `.duckdb` files in `samples/public/` are converted from openly licensed IFC files. Ara 3D's code in this repository is MIT licensed (`LICENSE` at the root); the building data keeps its own licence, given below. The source IFC files are not in this repository: `fetch.mjs` downloads them from the commits named here.

What was changed, for every file: each source IFC file was read by this repository's IFC loader and written out as BIM Open Schema tables (entities, parameters, relations, and tessellated geometry, as Parquet in a zip), then loaded into a DuckDB database with text views. Nothing was added to or removed from the building's data on purpose; IFC content the converter does not map (for example 2D annotation) is absent, geometry is triangulated, and repeated meshes are stored once. `digitalhub-federated` and `duplex-federated` are unions of four models each (DigitalHub, Duplex) into one geometry-free document set, each source kept as its own document.

## Schependomlaan

Files: `schependomlaan.bos`, `schependomlaan.duckdb`.

- Source: `Design model IFC/IFC Schependomlaan.ifc` in https://github.com/openBIMstandards/Archive-DataSetSchependomlaan at commit `e6971d897b60f04ea28b4e3f2acf80d05f269197` (an archived repository; the dataset's current home is buildingSMART's sample files).
- Licence: Creative Commons Attribution 4.0 International, https://creativecommons.org/licenses/by/4.0/

Attribution:

> Schependomlaan dataset, (C) original owners, licensed CC BY 4.0, https://creativecommons.org/licenses/by/4.0/; converted to BIM Open Schema tables by Ara 3D.

## DigitalHub

Files: `digitalhub-arc.bos`, `digitalhub-arc.duckdb`, `digitalhub-hzg.bos`, `digitalhub-hzg.duckdb`, `digitalhub-federated.bos`, `digitalhub-federated.duckdb`.

- Source: `Version_2/DigitalHub_FM-ARC_v2.ifc`, `DigitalHub_FM-HZG_v2.ifc`, `DigitalHub_FM-LFT_v2.ifc`, and `DigitalHub_FM-SAN_v2.ifc` in https://github.com/RWTH-E3D/DigitalHub at commit `36565d529b4dadeca625de2b793d7e16700171e9`.
- Licence: MIT. Converted to BIM Open Schema tables by Ara 3D. The licence's copyright and permission notice, in full:

```
MIT License

Copyright (c) 2020 RWTH Aachen University - E3D Institute of Energy Efficiency and Sustainable Building

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Duplex Apartment

Files: `duplex.bos`, `duplex.duckdb`, `duplex-mep.bos`, `duplex-mep.duckdb`, `duplex-electrical.bos`, `duplex-electrical.duckdb`, `duplex-rooms.bos`, `duplex-rooms.duckdb`, `duplex-federated.bos`, and `duplex-federated.duckdb` here, and `samples/nrc/duplex-base.ifc` and `samples/nrc/duplex-enriched.ifc`.

- Source: `IFC 2.3.0.1 (IFC 2x3)/Duplex Apartment/Duplex_A_20110907.ifc` (architecture), `Duplex_MEP_20110907.ifc`, `Duplex_Electrical_20121207.ifc`, and `Duplex_M_20111024_ROOMS_AND_SPACES.ifc` in https://github.com/buildingsmart-community/Community-Sample-Test-Files at commit `7ddf57a201f88a0c213d5322b02ed15e94a60a40`.
- Licence: Creative Commons Attribution 4.0 International, https://creativecommons.org/licenses/by/4.0/ ("(C) original authors").

Attribution:

> BSI (2020) Duplex Apartment Test Files, buildingSMART International. (C) original authors, licensed CC BY 4.0, https://creativecommons.org/licenses/by/4.0/; converted to BIM Open Schema tables by Ara 3D.

`samples/nrc/duplex-base.ifc` is that file unchanged (the same SHA-256, `b347a2c8…606ed`). `samples/nrc/duplex-enriched.ifc` is that file with analytics property sets added for the NRC (National Research Council Canada) paper.
