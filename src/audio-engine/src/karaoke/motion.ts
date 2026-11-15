/**
 * Reduced-motion preference of the banner: the explicit `KaraokeOptions.reducedMotion`, else
 * `prefers-reduced-motion: reduce`, whose runtime changes are followed (FR-011).
 *
 * @packageDocumentation
 */

/** Media query of the operating-system preference. */
export const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';

/** Current preference plus change notifications from the media query. */
export class MotionPreference {
  private readonly query: MediaQueryList | null;
  private value: boolean;

  /**
   * @param explicit - `KaraokeOptions.reducedMotion`; when defined, the media query is ignored.
   * @param onChange - Called with the new value when the media query changes.
   */
  public constructor(
    explicit: boolean | undefined,
    private readonly onChange: (reduced: boolean) => void,
  ) {
    this.query = explicit === undefined ? mediaQuery() : null;
    this.value = explicit ?? this.matches();
    this.query?.addEventListener('change', this.listener);
  }

  /** Whether motion is reduced (no ball). */
  public get reduced(): boolean {
    return this.value;
  }

  /** Stops following the media query. */
  public dispose(): void {
    this.query?.removeEventListener('change', this.listener);
  }

  private matches(): boolean {
    return this.query?.matches ?? false;
  }

  private readonly listener =(event: MediaQueryListEvent): void => {
    this.value = event.matches;
    this.onChange(event.matches);
  };
}

function mediaQuery(): MediaQueryList | null {
  const match = (globalThis as { matchMedia?: (query: string) => MediaQueryList }).matchMedia;
  return match === undefined ? null : match(REDUCED_MOTION_QUERY);
}
