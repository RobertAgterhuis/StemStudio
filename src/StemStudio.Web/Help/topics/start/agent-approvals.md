---
id: agent-approvals
title: "Agent confirmations"
area: start
summary: "Before the agent deletes, overwrites or starts a long job it shows a card with the exact step to approve."
anchors: ["agent-approvals"]
keywords: ["approve", "reject", "confirm", "confirmation", "tainted", "undo"]
related: [agent-panel, agent-backends]
reviewed: "011"
---

The card shows what the step will do, the exact arguments, why it asks and whether it can be undone. Long jobs also
show the estimated minutes and whether they run on the graphics card or the processor.

- **Approve** runs this one step. **Approve all heavy steps of this plan** appears only when the plan has more of
  them.
- **Reject** skips the step; the agent continues or stops.
- "Proposed after reading external content" means the step was suggested after the agent read text from outside
  StemStudio (tags, lyrics, online facts). Check the arguments carefully.

**Undo** reverts the agent's last command: mixer settings return to their previous values, a new mix is removed and
a running job is cancelled.
