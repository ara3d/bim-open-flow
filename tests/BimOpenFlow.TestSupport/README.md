# BimOpenFlow.TestSupport

Test helpers for the projects that move to `bim-open-flow` in the repository split
(phase 5): `RepoPaths` (the checkout root and its `samples/`, `data/`, and `artifacts/`
folders, located from the source file) and `MiniIfc` (a fourteen-line IFC4 wall and a
`Document(...)` builder for hand-written STEP fixtures).

It is a copy of `tests/BimOpenToolkit.TestSupport` with one difference: the root is
the folder holding any `*.sln`, not `BimOpenToolkit.sln`, so the same file works in both
repositories. The layering tests (`BimSeamTests`) fail if a project that moves
references the toolkit's copy instead.
