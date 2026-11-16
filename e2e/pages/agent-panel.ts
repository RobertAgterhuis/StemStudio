import { expect, type Locator, type Page } from '@playwright/test';

/** Longest wait for a scripted run to finish (fake model turns, loopback calls, UI tools). */
export const agentRunTimeoutMs = 60_000;

/** Plan item and step statuses of the contract (data-model 4, ui-test-ids 1.10.0). */
export type AgentStepStatus =
  | 'planned'
  | 'awaiting_approval'
  | 'running'
  | 'awaiting_ui'
  | 'waiting_job'
  | 'succeeded'
  | 'failed'
  | 'rejected'
  | 'rejected_invalid'
  | 'skipped'
  | 'expired'
  | 'undone';

/**
 * The composer agent panel of the layout (ui-test-ids 1.10.0, "Additions in 1.10.0"): toggle,
 * conversation, messages, plan, step rows and approval cards. Step rows and messages are found by
 * attribute because their `<seq>` suffix is not predictable.
 */
export class AgentPanel {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Element with the given contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** The panel (complementary landmark). */
  get panel(): Locator {
    return this.byId('agent-panel');
  }

  /** The layout button that opens and closes the panel (`aria-expanded`). */
  get toggle(): Locator {
    return this.byId('agent-toggle');
  }

  /** Backend badge (`data-backend`, `data-model`, `data-fallback`). */
  get badge(): Locator {
    return this.byId('agent-backend-badge');
  }

  /** Unavailable notice with the reason and the fix (only while the backend is not ready). */
  get unavailable(): Locator {
    return this.byId('agent-unavailable');
  }

  /** Message input. */
  get input(): Locator {
    return this.byId('agent-input');
  }

  /** Send button. */
  get send(): Locator {
    return this.byId('agent-send');
  }

  /** Undo button (enabled when something can be undone). */
  get undo(): Locator {
    return this.byId('agent-undo');
  }

  /** The confirmation card (`data-tool`, `data-risk`, `data-reasons`). */
  get approvalCard(): Locator {
    return this.byId('agent-approval-card');
  }

  /** Opens the panel when it is closed. */
  async open(): Promise<void> {
    await expect(this.toggle).toBeVisible();
    if ((await this.toggle.getAttribute('aria-expanded')) !== 'true') {
      await this.toggle.click();
    }
    await expect(this.toggle).toHaveAttribute('aria-expanded', 'true');
    await expect(this.panel).toBeVisible();
  }

  /** Opens the panel and starts a new conversation, so earlier tests leave no messages in view. */
  async openNewConversation(): Promise<void> {
    await this.open();
    await this.byId('agent-new-conversation').click();
    await expect(this.messages()).toHaveCount(0);
  }

  /** Types `text` and presses Send (the page context of the current page is sent with it). */
  async sendCommand(text: string): Promise<void> {
    await expect(this.send).toBeVisible();
    await this.input.fill(text);
    await expect(this.send).toBeEnabled();
    await this.send.click();
    await expect(this.messages('user').last()).toContainText(text);
  }

  /** Messages in the open conversation, optionally of one role (`data-role`). */
  messages(role?: 'user' | 'assistant'): Locator {
    const roleFilter = role === undefined ? '' : `[data-role="${role}"]`;
    return this.panel.locator(`[data-testid^="agent-message-"]${roleFilter}`);
  }

  /** Waits until an assistant message contains `text` (the scripted final answer) and the run ended. */
  async expectAnswer(text: string): Promise<void> {
    await expect(this.messages('assistant').filter({ hasText: text })).toHaveCount(1, { timeout: agentRunTimeoutMs });
    await expect(this.byId('agent-cancel')).toHaveCount(0, { timeout: agentRunTimeoutMs });
    await expect(this.byId('agent-streaming')).toHaveCount(0);
  }

  /** Step rows of one tool (`data-tool`); progress and error elements carry no `data-tool`. */
  steps(tool: string): Locator {
    return this.panel.locator(`[data-testid^="agent-step-"][data-tool="${tool}"]`);
  }

  /** Asserts that exactly one step of `tool` exists and has `status`. */
  async expectStep(tool: string, status: AgentStepStatus): Promise<void> {
    await expect(this.steps(tool)).toHaveCount(1, { timeout: agentRunTimeoutMs });
    await expect(this.steps(tool)).toHaveAttribute('data-status', status, { timeout: agentRunTimeoutMs });
  }

  /** The error text of the single step of `tool` (`agent-step-error-<seq>`). */
  async stepError(tool: string): Promise<Locator> {
    const testId = await this.steps(tool).getAttribute('data-testid');
    const seq = (testId ?? '').replace('agent-step-', '');
    return this.byId(`agent-step-error-${seq}`);
  }

  /** Plan items in order (`agent-plan-item-<index>`, `data-status`). */
  planItems(): Locator {
    return this.byId('agent-plan').locator('[data-testid^="agent-plan-item-"]');
  }

  /** Asserts that the plan has `count` items, all with `status`. */
  async expectPlan(count: number, status: string): Promise<void> {
    await expect(this.planItems()).toHaveCount(count, { timeout: agentRunTimeoutMs });
    for (const item of await this.planItems().all()) {
      await expect(item).toHaveAttribute('data-status', status, { timeout: agentRunTimeoutMs });
    }
  }

  /** Waits for the confirmation card of `tool` with `risk` and returns it. */
  async expectApproval(tool: string, risk: string): Promise<Locator> {
    await expect(this.approvalCard).toBeVisible({ timeout: agentRunTimeoutMs });
    await expect(this.approvalCard).toHaveAttribute('data-tool', tool);
    await expect(this.approvalCard).toHaveAttribute('data-risk', risk);
    await expect(this.byId('agent-approve')).not.toBeFocused();
    return this.approvalCard;
  }

  /** The reason codes of the open card (`data-reasons`, comma-separated). */
  async approvalReasons(): Promise<string[]> {
    return ((await this.approvalCard.getAttribute('data-reasons')) ?? '').split(',').filter((reason) => reason !== '');
  }

  /** Presses Approve (once) and waits until the card is gone (`approval.resolved`). */
  async approve(): Promise<void> {
    await this.byId('agent-approve').click();
    await expect(this.approvalCard).toHaveCount(0, { timeout: agentRunTimeoutMs });
  }

  /** Presses Reject and waits until the card is gone. */
  async reject(): Promise<void> {
    await this.byId('agent-reject').click();
    await expect(this.approvalCard).toHaveCount(0, { timeout: agentRunTimeoutMs });
  }

  /** Entry of a conversation in the list (`agent-conversation-<id>`). */
  conversation(conversationId: string): Locator {
    return this.byId(`agent-conversation-${conversationId}`);
  }
}
