import { axeViolations } from '../support/axe';
import { createFixtureSheet, requireModelBackend } from '../support/lyric-assist-fixtures';
import { runAssist } from '../support/lyric-assist-flow';
import { expect, test } from '../support/lyric-assist-test';
import { cellState, matrixThemes } from '../support/visual';

// Feature 023 (SC-008, quickstart Q9; WCAG 2.2 AA as in 004): axe on the lyrics editor with the
// assistant pane open and a finished check, in both themes at Expert (every pane control) and
// Beginner (fewest). Fake stack with the lyric scripts (`@lyric-assist`). The NVDA pass of Q9 is
// manual (docs/testing/023-lyric-assist.md section 6).
for (const theme of matrixThemes) {
  for (const level of ['beginner', 'expert'] as const) {
    test.describe(`lyric assistant / ${level} / ${theme}`, { tag: '@lyric-assist' }, () => {
      test.use({ storageState: cellState(level, theme) });

      test('the editor with the pane open and a finished check', async ({ lyricAssistApi, assistPane, page }) => {
        await requireModelBackend(lyricAssistApi, 'fake');
        const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
        await assistPane.goto(sheet.id);
        expect(await axeViolations(page), 'pane idle').toEqual([]);
        await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
        await expect(assistPane.list).toHaveAttribute('role', 'listbox');
        expect(await axeViolations(page), 'pane with suggestions').toEqual([]);
      });
    });
  }
}
