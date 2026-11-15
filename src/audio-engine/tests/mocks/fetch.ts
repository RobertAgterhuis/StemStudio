/** Scripted reply of the stubbed fetch for one URL. */
export interface FetchReply {
  readonly status?: number;
  /** Body size in bytes (the mock AudioContext maps byte length to a decoded duration). */
  readonly bytes?: number;
  /** When set, fetch rejects with this error (network failure). */
  readonly error?: Error;
}

/** Stubbed `fetch` with per-URL replies; records requested URLs. */
export class FetchStub {
  public readonly requested: string[] = [];
  private readonly replies = new Map<string, FetchReply>();

  /** Registers the reply for `url`. */
  public reply(url: string, reply: FetchReply): this {
    this.replies.set(url, reply);
    return this;
  }

  /** The `fetch` function to inject into the engine. */
  public readonly fetch = (input: RequestInfo | URL): Promise<Response> => {
    const url = input instanceof Request ? input.url : String(input);
    this.requested.push(url);
    const reply = this.replies.get(url) ?? { status: 404 };
    return reply.error === undefined ? Promise.resolve(makeResponse(reply)) : Promise.reject(reply.error);
  };
}

function makeResponse(reply: FetchReply): Response {
  const status = reply.status ?? 200;
  const bytes = reply.bytes ?? 16;
  const response = {
    ok: status >= 200 && status < 300,
    status,
    statusText: status === 200 ? 'OK' : 'Error',
    arrayBuffer: () => Promise.resolve(new ArrayBuffer(bytes)),
  };
  return response as unknown as Response;
}
