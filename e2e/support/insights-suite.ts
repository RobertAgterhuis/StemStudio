/**
 * Shared set-up and expectations of the 020 specs: the per-test API context and client (skipped while the stack has no
 * 020 backend), and the texts and tip codes the fake analyzer must produce (data-model 2.6, 4.6 and 5.3). Every
 * expectation below is derived from the contract texts; readings are listed in `docs/testing/TEST-STRATEGY.md` 23.
 *
 * @packageDocumentation
 */
import { test, type APIRequestContext } from '@playwright/test';
import { apiContextOptions } from './env';
import { InsightsApi, type QualityRating, type TipCode } from './insights-api';
import { required } from './instrument-views';

/** Accessors of the per-test context and client registered by {@link useInsightsApi}. */
export interface InsightsSuite {
  readonly insights: () => InsightsApi;
  readonly context: () => APIRequestContext;
}

/** Registers the hooks of a 020 spec: timeout, API context, client and the feature skip. */
export function useInsightsApi(timeoutMs: number): InsightsSuite {
  let context: APIRequestContext | undefined;
  let insights: InsightsApi | undefined;
  test.beforeEach(async ({ playwright }) => {
    test.setTimeout(timeoutMs);
    context = await playwright.request.newContext(apiContextOptions);
    insights = new InsightsApi(context);
    await insights.requireFeature();
  });
  test.afterEach(async () => {
    await context?.dispose();
  });
  return { insights: () => required(insights, 'insights client'), context: () => required(context, 'API context') };
}

/** The six standard stems with the fake analyzer's rating (data-model 2.6). */
export const fakeRatings: Readonly<Record<string, QualityRating>> = {
  vocals: 'fair',
  drums: 'good',
  bass: 'good',
  guitar: 'poor',
  piano: 'not-present',
  other: 'fair',
};

/** Reason texts of the fake ratings (data-model 4.6; `{source}` is the source stem in lower case). */
export const fakeReasons: Readonly<Record<string, string>> = {
  vocals: 'Some sound of drums is in this stem.',
  drums: 'Little bleed and few artefacts.',
  bass: 'Little bleed and few artefacts.',
  guitar: 'The stem has separation artefacts (a watery or choppy sound).',
  piano: 'This instrument is probably not in this song; what you hear is spill from other instruments.',
  other: 'Some sound of guitar is in this stem.',
};

/** Fixed tip texts (data-model 5.3); `use-detail-stem` and `hard-to-isolate` carry a name. */
export const tipTexts: Readonly<Record<TipCode, RegExp>> = {
  'not-in-song': /^Mute this stem; the instrument is probably not in this song\.$/u,
  'use-detail-stem': /^Use the .+ detail stem instead\.$/u,
  'try-detail-separation': /^Split further \(detail separation\) may pull apart the instruments mixed into this stem\.$/u,
  'listen-to-rest': /^Part of this sound went to the 'rest' stem; listen to it too\.$/u,
  'hard-to-isolate': /^.+ is hard to isolate with today's models; this is close to the best StemStudio can do\.$/u,
  clean: /^Clean enough to practise with\.$/u,
};

/** The guitar tip after the split (US2 scenario 2, exact). */
export const guitarHardToIsolate = "Guitar is hard to isolate with today's models; this is close to the best StemStudio can do.";

/** Tip codes per standard stem before any detail split (rules 1, 4, 7; rule 3 is G2). */
export const tipsBeforeSplit: Readonly<Record<string, readonly TipCode[]>> = {
  vocals: ['try-detail-separation'],
  drums: ['clean'],
  bass: ['clean'],
  guitar: ['try-detail-separation'],
  piano: ['not-in-song'],
  other: ['try-detail-separation'],
};

/**
 * Tip codes per standard stem after the fake detail split (vocals, drums, other split; guitar nothing further; piano
 * not reliable; bass not supported): rule 2 for a split parent whose children rate better, rule 6 for guitar.
 */
export const tipsAfterSplit: Readonly<Record<string, readonly TipCode[]>> = {
  vocals: ['use-detail-stem'],
  drums: ['clean'],
  bass: ['clean'],
  guitar: ['hard-to-isolate'],
  piano: ['not-in-song'],
  other: ['use-detail-stem'],
};
