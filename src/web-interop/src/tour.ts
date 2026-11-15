/**
 * Guided tours on driver.js 1.10.2 (research.md R-07, web-interop.md 2-3): skips missing or hidden targets,
 * labels the popover for tests and assistive technology, traps focus in it, maps the arrow keys, Enter and
 * Escape, honours reduced motion, restores focus at the end and reports how the tour ended to .NET.
 *
 * @packageDocumentation
 */
import { driver, type Config, type DriveStep, type PopoverDOM } from 'driver.js';
import { captureFocus } from './focus';
import type { Disposable, DotNetRef, TourRuntime, TourStatus } from './types';
import { once } from './validation';

/** The part of a driver.js instance the wrapper uses. */
export interface TourDriver {
  /** Starts at a step. */
  drive(stepIndex?: number): void;
  /** Removes the overlay and popover. */
  destroy(): void;
  /** Shows the next step. */
  moveNext(): void;
  /** Shows the previous step. */
  movePrevious(): void;
  /** Index of the shown step. */
  getActiveIndex(): number | undefined;
  /** True on the last step. */
  isLastStep(): boolean;
}

/** Creates a driver for a configuration (replaced by a fake in unit tests). */
export type DriverFactory = (config: Config) => TourDriver;

/** The real driver.js factory. */
export const defaultDriverFactory: DriverFactory = (config) => driver(config);

let factory: DriverFactory = defaultDriverFactory;

/**
 * Replaces the driver factory (unit tests).
 *
 * @param next - The factory to use from now on.
 */
export function useDriverFactory(next: DriverFactory): void {
  factory = next;
}

interface Session {
  readonly dotnet: DotNetRef;
  readonly tour: TourRuntime;
  readonly focus: Disposable;
  readonly onKey: (event: KeyboardEvent) => void;
  driver: TourDriver;
  ended: boolean;
  targetAria?: AriaSnapshot;
}

/** ARIA attributes driver.js puts on the highlighted element (a "popup" button), saved and put back. */
const POPUP_ARIA = ['aria-haspopup', 'aria-expanded', 'aria-controls'] as const;

type AriaSnapshot = readonly (readonly [string, string | null])[];

function ariaOf(element: Element | undefined): AriaSnapshot {
  return element === undefined ? [] : POPUP_ARIA.map((name) => [name, element.getAttribute(name)] as const);
}

function setOrRemove(element: Element, name: string, value: string | null): void {
  if (value === null) {
    element.removeAttribute(name);
  } else {
    element.setAttribute(name, value);
  }
}

/** Restores the target's own values: popup attributes are not allowed on every role (axe aria-allowed-attr). */
function restoreAria(element: Element | undefined, snapshot: AriaSnapshot = []): void {
  if (element === undefined) {
    return;
  }

  for (const [name, value] of snapshot) {
    setOrRemove(element, name, value);
  }
}

/** Highlight hooks that keep the target's own ARIA semantics while it is highlighted. */
function ariaHooks(session: Session): Pick<Config, 'onHighlightStarted' | 'onHighlighted'> {
  return {
    onHighlightStarted: (element) => {
      session.targetAria = ariaOf(element);
    },
    onHighlighted: (element) => {
      restoreAria(element, session.targetAria);
    },
  };
}

/** Offer chips; .NET removes a chip once its tour is recorded, so focus must not return into one. */
const OFFER_CHIP = '[data-testid^="tour-offer-"]';

function focusHeading(heading: HTMLElement): void {
  if (!heading.hasAttribute('tabindex')) {
    heading.tabIndex = -1;
  }

  heading.focus();
}

/**
 * Remembers where focus returns when the tour ends: the opener, or the page heading when the opener sits in an
 * offer chip that disappears with the recorded tour (WCAG 2.4.3; focus would otherwise fall back to the body).
 */
function captureTourFocus(): Disposable {
  const inChip = document.activeElement?.closest(OFFER_CHIP) != null;
  const heading = document.querySelector<HTMLElement>('h1');
  const restore = captureFocus();
  return once(() => {
    if (inChip && heading !== null) {
      focusHeading(heading);
    } else {
      restore.dispose();
    }
  });
}

/** The driver is assigned right after its config is built; driver.js calls no hook before `drive()`. */
const PENDING = null as unknown as TourDriver;

function isVisible(element: Element): boolean {
  if (!(element instanceof HTMLElement) || element.closest('[hidden]') !== null) {
    return false;
  }

  const style = getComputedStyle(element);
  return style.display !== 'none' && style.visibility !== 'hidden';
}

function resolveSteps(tour: TourRuntime): DriveStep[] {
  return tour.steps.flatMap((step) => {
    const element = document.querySelector(`[data-help-id="${step.target}"]`);
    return element !== null && isVisible(element) ? [{ element, popover: { title: step.title, description: step.text } }] : [];
  });
}

function activeIndex(session: Session): number {
  return session.driver.getActiveIndex() ?? 0;
}

function report(session: Session, status: TourStatus | null): void {
  if (status !== null) {
    void session.dotnet.invokeMethodAsync('OnTourEnded', session.tour.id, status, activeIndex(session));
  }
}

function finish(session: Session, status: TourStatus | null, destroy: boolean): void {
  if (session.ended) {
    return;
  }

  session.ended = true;
  document.removeEventListener('keydown', session.onKey, true);
  report(session, status);
  if (destroy) {
    session.driver.destroy();
  }

  session.focus.dispose();
}

function next(session: Session): void {
  if (session.driver.isLastStep()) {
    finish(session, 'completed', true);
    return;
  }

  session.driver.moveNext();
}

function previous(session: Session): void {
  if (activeIndex(session) > 0) {
    session.driver.movePrevious();
  }
}

function popoverButtons(): HTMLElement[] {
  return [...(document.querySelector('[data-testid="tour-popover"]')?.querySelectorAll<HTMLElement>('button') ?? [])];
}

function trapTab(event: KeyboardEvent): void {
  const buttons = popoverButtons();
  const ring = event.shiftKey ? [...buttons].reverse() : buttons;
  if (ring.length > 0 && document.activeElement === ring.at(-1)) {
    event.preventDefault();
    ring.slice(0, 1).forEach((button) => {
      button.focus();
    });
  }
}

const KEYS: Readonly<Record<string, (session: Session, event: KeyboardEvent) => void>> = {
  ArrowRight: (session) => {
    next(session);
  },
  Enter: (session) => {
    next(session);
  },
  ArrowLeft: (session) => {
    previous(session);
  },
  Escape: (session) => {
    finish(session, 'dismissed', true);
  },
};

function onKey(session: Session, event: KeyboardEvent): void {
  if (event.key === 'Tab') {
    trapTab(event);
    return;
  }

  const handler = KEYS[event.key];
  if (handler !== undefined) {
    event.preventDefault();
    handler(session, event);
  }
}

function decorate(popover: PopoverDOM, session: Session, total: number, index: number): void {
  const wrapper = popover.wrapper;
  Object.assign(wrapper.dataset, { testid: 'tour-popover', tourId: session.tour.id, step: String(index + 1), steps: String(total) });
  wrapper.setAttribute('role', 'dialog');
  wrapper.setAttribute('aria-modal', 'true');
  popover.title.id = 'tour-popover-title';
  wrapper.setAttribute('aria-labelledby', popover.title.id);
  popover.nextButton.dataset.testid = 'tour-next';
  popover.previousButton.dataset.testid = 'tour-prev';
  popover.closeButton.dataset.testid = 'tour-close';
  popover.nextButton.focus();
}

function configFor(session: Session, steps: DriveStep[]): Config {
  const { labels, reducedMotion } = session.tour;
  return {
    steps,
    animate: !reducedMotion,
    allowKeyboardControl: false,
    showProgress: true,
    progressText: labels.progress,
    nextBtnText: labels.next,
    prevBtnText: labels.previous,
    doneBtnText: labels.done,
    closeBtnLabel: labels.close,
    popoverClass: 'ss-tour',
    ...ariaHooks(session),
    ...navigationHooks(session),
    onPopoverRender: (popover, opts) => {
      decorate(popover, session, steps.length, Number(opts.state.activeIndex));
    },
  };
}

function navigationHooks(session: Session): Pick<Config, 'onNextClick' | 'onPrevClick' | 'onCloseClick' | 'onDestroyed'> {
  return {
    onNextClick: () => {
      next(session);
    },
    onPrevClick: () => {
      previous(session);
    },
    onCloseClick: () => {
      finish(session, 'dismissed', true);
    },
    onDestroyed: () => {
      finish(session, 'dismissed', false);
    },
  };
}

/**
 * Starts a tour. Steps whose target `[data-help-id=...]` is missing or hidden are skipped; without any visible
 * step the tour ends at once as dismissed with step 0. Calls
 * `dotnet.invokeMethodAsync("OnTourEnded", tourId, "completed" | "dismissed", lastStep)` once.
 *
 * @param dotnet - Receiver of the end callback.
 * @param tour - Tour with resolved texts.
 * @returns Handle that destroys the running tour without a callback (navigation).
 */
export function startTour(dotnet: DotNetRef, tour: TourRuntime): Disposable {
  const steps = resolveSteps(tour);
  if (steps.length === 0) {
    void dotnet.invokeMethodAsync('OnTourEnded', tour.id, 'dismissed', 0);
    return once(() => undefined);
  }

  const session: Session = { dotnet, tour, focus: captureTourFocus(), ended: false, driver: PENDING, onKey: (event) => { onKey(session, event); } };
  document.addEventListener('keydown', session.onKey, true);
  session.driver = factory(configFor(session, steps));
  session.driver.drive(0);
  return once(() => {
    finish(session, null, true);
  });
}
