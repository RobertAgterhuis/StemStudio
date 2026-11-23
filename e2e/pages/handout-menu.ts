import type { Locator } from '@playwright/test';
import type { HandoutFamily, IngredientCode } from '../support/insights-api';
import { TestIdPage } from './test-id-page';

/** Lesson handout menu and dialog of the track page and the setlist page (020 US3/US4, ui-test-ids 1.19.0). */
export class HandoutMenu extends TestIdPage {
  /** "Lesson handout" (`song`) or "Lesson handout per instrument" (`setlist`) menu button. */
  menu(kind: 'song' | 'setlist' = 'song'): Locator {
    return this.byId(kind === 'song' ? 'handout-menu' : 'setlist-handout-menu');
  }

  /** The menu item of a family (one click creates the handout with defaults). */
  family(family: HandoutFamily, kind: 'song' | 'setlist' = 'song'): Locator {
    return this.byId(kind === 'song' ? `handout-family-${family}` : `setlist-handout-family-${family}`);
  }

  /** "Customise..." (Advanced, Expert). */
  customise(kind: 'song' | 'setlist' = 'song'): Locator {
    return this.byId(kind === 'song' ? 'handout-customise' : 'setlist-handout-customise');
  }

  /** The handout dialog. */
  get dialog(): Locator {
    return this.byId('handout-dialog');
  }

  /** A checkbox of the dialog (`data-status`). */
  ingredient(code: IngredientCode): Locator {
    return this.byId(`handout-ingredient-${code}`);
  }

  /** Opens the menu and picks a family. */
  async oneClick(family: HandoutFamily, kind: 'song' | 'setlist' = 'song'): Promise<void> {
    await this.menu(kind).click();
    await this.family(family, kind).click();
  }
}
