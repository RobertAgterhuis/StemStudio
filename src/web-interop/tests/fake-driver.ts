import type { Config, DriveStep, PopoverDOM } from 'driver.js';
import type { TourDriver } from '../src/tour';

/** A minimal driver.js stand-in: renders a popover for the active step and calls the config hooks. */
export class FakeDriver implements TourDriver {
  public index = -1;
  public destroyed = false;
  public popover: PopoverDOM | undefined;

  public constructor(public readonly config: Config) {}

  private get steps(): DriveStep[] {
    return this.config.steps ?? [];
  }

  public drive(stepIndex = 0): void {
    this.show(stepIndex);
  }

  public moveNext(): void {
    this.show(this.index + 1);
  }

  public movePrevious(): void {
    this.show(this.index - 1);
  }

  public getActiveIndex(): number | undefined {
    return this.index < 0 ? undefined : this.index;
  }

  public isLastStep(): boolean {
    return this.index === this.steps.length - 1;
  }

  public destroy(): void {
    this.destroyed = true;
    this.popover?.wrapper.remove();
    this.config.onDestroyed?.(undefined, this.steps[this.index] ?? {}, this.opts());
  }

  private opts(): Parameters<NonNullable<Config['onDestroyed']>>[2] {
    return { config: this.config, state: { activeIndex: this.index }, driver: undefined as never, index: this.index };
  }

  private show(index: number): void {
    this.index = index;
    this.popover?.wrapper.remove();
    this.popover = this.render(this.steps[index]?.popover ?? {});
    this.config.onPopoverRender?.(this.popover, this.opts());
  }

  private button(text: string, hook: Config['onNextClick']): HTMLButtonElement {
    const button = document.createElement('button');
    button.textContent = text;
    button.addEventListener('click', () => hook?.(undefined, this.steps[this.index] ?? {}, this.opts()));
    return button;
  }

  private render(popover: NonNullable<DriveStep['popover']>): PopoverDOM {
    const wrapper = document.createElement('div');
    const title = document.createElement('header');
    title.textContent = String(popover.title);
    const description = document.createElement('div');
    description.textContent = String(popover.description);
    const progress = document.createElement('span');
    const previousButton = this.button('prev', this.config.onPrevClick);
    const nextButton = this.button(this.isLastStep() ? 'done' : 'next', this.config.onNextClick);
    const closeButton = this.button('x', this.config.onCloseClick);
    const footer = document.createElement('footer');
    const footerButtons = document.createElement('span');
    footerButtons.append(previousButton, nextButton);
    footer.append(progress, footerButtons);
    wrapper.append(closeButton, title, description, footer);
    document.body.append(wrapper);
    return { wrapper, arrow: document.createElement('span'), title, description, footer, progress, previousButton, nextButton, closeButton, footerButtons };
  }
}
