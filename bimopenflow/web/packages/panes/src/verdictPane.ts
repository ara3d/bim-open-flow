import type { TableSlice } from "@bimopenflow/contracts";
import type { Pane } from "./pane";
import { definePane } from "./base";
import { idColumnIndex, idOf } from "./columns";
import { groupVerdicts, VERDICTS, type CheckGroup } from "./verdictGroups";

/**
 * Verdict pane: renders a verdict table (compliance convention columns
 * verdict/checkId/checkTitle/citation) as one block per check, with
 * pass/fail/needsReview/infoNotAvailable count chips and severity coloring.
 *
 * Inputs: "table" (a verdict table) renders the check list; "selection"
 * highlights checks containing any selected row id. Clicking a check emits a
 * "selection" event with the distinct ids of that check's rows (id column
 * heuristic: globalId, else entityId, else the first column).
 */
export const createVerdictPane = (): Pane =>
  definePane((root, _ctx, emit) => {
    let groups: CheckGroup[] = [];
    let groupIds: string[][] = [];
    let selected: ReadonlySet<string> = new Set();

    const highlight = (): void => {
      root.querySelectorAll(".bof-panes-check").forEach((el, i) => {
        el.classList.toggle(
          "bof-panes-check--selected",
          groupIds[i]?.some((id) => selected.has(id)) ?? false,
        );
      });
    };

    const render = (): void => {
      const doc = root.ownerDocument;
      root.textContent = "";
      groups.forEach((group, i) => {
        const block = doc.createElement("div");
        block.className = `bof-panes-check bof-panes-check--${group.worst}`;
        block.dataset.checkId = group.checkId;

        const title = doc.createElement("div");
        title.className = "bof-panes-check-title";
        title.textContent = `${group.checkId} — ${group.checkTitle}`;
        block.appendChild(title);

        const citation = doc.createElement("div");
        citation.className = "bof-panes-check-citation";
        citation.textContent = group.citation;
        block.appendChild(citation);

        for (const verdict of VERDICTS) {
          const count = group.counts[verdict];
          if (count === 0) continue;
          const chip = doc.createElement("span");
          chip.className = `bof-panes-chip bof-panes-chip--${verdict}`;
          chip.textContent = `${verdict} ${count}`;
          block.appendChild(chip);
        }

        block.addEventListener("click", () =>
          emit({
            kind: "selection",
            event: { source: "verdict", ids: groupIds[i] },
          }),
        );
        root.appendChild(block);
      });
      highlight();
    };

    return {
      update(input) {
        if (input.kind === "table") {
          groups = groupVerdicts(input.data);
          const idIdx = idColumnIndex(input.data.columns);
          groupIds = groups.map((g) => [
            ...new Set(g.rowIndices.map((r) => idOf(input.data.rows[r][idIdx]))),
          ]);
          render();
        } else if (input.kind === "selection") {
          selected = new Set(input.ids);
          highlight();
        }
      },
    };
  });
