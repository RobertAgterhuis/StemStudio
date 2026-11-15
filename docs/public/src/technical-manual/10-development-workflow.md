# Development workflow

StemStudio is built by a team of specialised AI agents, orchestrated by a CTO agent, with
GitHub Spec Kit as the process.

## Spec Kit phases

Each feature is a folder `specs/NNN-name/` and goes through:

1. **Specify** (`/speckit-specify`): `spec.md` with user stories, requirements and success
   criteria.
2. **Clarify** (`/speckit-clarify`): open questions resolved and recorded.
3. **Plan** (`/speckit-plan`): `plan.md`, `research.md`, `data-model.md`, `quickstart.md`,
   contracts and an ADR, with a Constitution Check.
4. **Tasks** (`/speckit-tasks`): `tasks.md`, dependency-ordered and assigned to owners.
5. **Analyze** (`/speckit-analyze`): cross-artifact consistency check.
6. **Implement** (`/speckit-implement`): a parallel agent wave, then an integration wave.

Build order after the MVP: 003 → 004 → {005, 007} → {006, 008} → {009 → 010} → 011.
Features in braces are built in parallel; their contract edits are additive.

## Agents and lanes

| Agent | Owns |
|---|---|
| architect | Plans, ADRs, contracts, data model, repository skeleton |
| backend | ASP.NET Core API, EF Core, storage, worker client |
| frontend | Blazor web app and its bUnit tests |
| audio-engine | TypeScript Web Audio engine |
| audio-ingest, stem-separation, beat-analysis | Worker ingest, separation and analysis |
| generative, ai-agent | Generator services (009, 010) and the agent library (011) |
| qa | Test strategy, fixtures, Playwright suites, audits |
| quality-gate | SIG checks, lint configuration, Sonar |
| devops | Dockerfiles, compose, scripts, CI |

Rules from `docs/AGENT-PROTOCOL.md`: stay in your lane (write only owned paths), contracts are
fixed during a wave, test-first, SIG limits, ≥ 90 % coverage, documented public APIs, no
secrets, open source only, Conventional Commits. Each agent works in its own git worktree.

## Hand-offs

Every task ends with `docs/handoffs/<agent>-<task>.md`: what was built, files changed, tests
and coverage (real output), quality checks, dependencies added (name, version, licence, URL),
open issues and the next owner. The CTO reviews each hand-off, merges, and records results in
`docs/STATUS.md`.

## Integration wave

After all lanes merge, the integration wave runs every suite on an isolated compose project,
fixes integration findings, updates the visual baselines, consolidates licences and records
the results. **Before the wave closes, the CTO updates the public manuals and regenerates the
PDFs** (task "Update docs/public manuals and regenerate PDFs" in every feature's `tasks.md`).

## Constitution amendments

The constitution changes only through `/speckit-constitution` with a semantic version bump
(MAJOR for removed or redefined principles, MINOR for new principles or sections, PATCH for
wording) and an ADR when the technical direction changes.
