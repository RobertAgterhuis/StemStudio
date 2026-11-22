# Tipoca — project additions

This file is imported from the project's `CLAUDE.md` (`@.claude/tipoca/TIPOCA.md`). The Tipoca core
rules (TIER0) are injected by the plugin at session start and are always the current version; this
file only holds what is specific to this project. Edit it freely — Tipoca never overwrites it.

## Mode

The mode lives in `.claude/tipoca/mode.json`, never in this file:

| Mode | Set by | Meaning |
|---|---|---|
| `factory` | `/tipoca:init` | New project. Agents may build features via `/tipoca:build`. |
| `audit` | `/tipoca:adopt` | Running project. Writes are allowed only under `.claude/**` and `docs/refactor/**`; existing code is read, never changed. Findings become proposals in `docs/refactor/`. |

Leaving audit mode is a human decision: edit `mode.json` to `{"mode":"factory"}` yourself.
`/tipoca:init` never downgrades an existing audit mode on its own.

## Precedence

TIER0 §1 holds the full list (top wins): the user's explicit instruction in the session, then the
**Tipoca guardrails** (the hooks, W7 secrets, W8 outward changes, W10 no self-approval), then the
**project norm** (`.claude/tipoca/norm.json`, below), then **Tipoca practice** (the rest of TIER0 and the
Tipoca skills, sharpened by the project's own instructions: `CLAUDE.md`, `AGENTS.md`, this file, the
Spec Kit constitution), then general defaults. This file sits at the practice level: it may sharpen
TIER0, never relax it. No instruction switches off a hook guardrail (audit mode, no push to `main`,
no force-push, no recursive deletes outside `.claude/worktrees/` and `.claude/scratchpad/`).

## Project norm

A project with its own layered rules (core rules such as a "Tier-0" set, approved exceptions,
folder-scoped rules, conventions) describes them in `.claude/tipoca/norm.json`
(`schemas/norm.schema.json` in the plugin). Tipoca never replaces that norm: it reads the map once,
enforces its order and shows it at session start and in `/tipoca:status`.

- **Layers** keep the project's own labels ("Tier-0" …) in the project's order: `core`,
  `exceptions`, `scoped`, `conventions`. Their paths are project-relative files, folders or globs.
- **Shadows** name a Tipoca skill (`release`), a stack-rules reference (`stack-rules/bicep`) or a
  practice rule (`W3`) and the project file that replaces it. Active only when the norm and the
  shadow are approved; a W-rule also needs the owner's approval through an inbox decision. The
  guardrails are never shadowed.
- **Changing it**: propose an entry with `"status": "proposed"` and an inbox decision (approve /
  changes / stop); only the owner approves. A core rule changes only through a superseding decision
  record; in factory mode a write to a core file asks for confirmation (`tipoca:project-norm`).
- Check it with `node "<plugin>/scripts/norm.mjs" check`; `summary` and `resolve <name>` show what applies.

## Where things live

| What | Path |
|---|---|
| Project additions (this file) | `.claude/tipoca/TIPOCA.md` |
| Mode marker | `.claude/tipoca/mode.json` |
| Project norm map (committed; layers, shadows) | `.claude/tipoca/norm.json` |
| Design snapshot (from the plugin) | `.claude/tipoca/design/default.md` + `tokens.json` |
| Design override (yours, wins fully) | `.claude/design/override.md` |
| Hook warning log (git-ignored) | `.claude/tipoca/logs/hooks.log` |
| Agent worktrees (git-ignored; share the main checkout's state and site) | `.claude/worktrees/<branch>/` |
| Agent scratch files (git-ignored) | `.claude/scratchpad/<branch>/` |
| Spec Kit | `.specify/` and `specs/<NNN-feature>/` |
| Decision records (the record home) | `records.paths` in `.claude/tipoca/norm.json`, else `.claude/tipoca/decisions/` |
| Local project site: port, title, audience (committed; the launcher's config) | `.claude/tipoca/projectsite/site.json` |
| Local project site runtime, per user (logs, locks, update markers, build output; never in the project) | `${CLAUDE_PLUGIN_DATA}/projectsite/<hash>/` (`<hash>` names the main checkout) |
| Update-check cache, per user | `${CLAUDE_PLUGIN_DATA}/update-check.json` |
| Per-user settings (autostart, icon pack, provider, update check; set with `site.mjs config`) | `${CLAUDE_PLUGIN_DATA}/settings.json` |
| Refactor proposals (running projects) | `docs/refactor/NNN-slug.md` + `docs/refactor/README.md` |
| Project site, GitHub Pages (public repos only) | `.github/workflows/tipoca-pages.yml` + `docs/site/` |
| Project site, Azure Static Web Apps | `.github/workflows/tipoca-site.yml` + `docs/site/` + `.claude/tipoca/infra/site/` |
| Stack detection (read by skills, never re-detected) | `.claude/tipoca/state/stack.json` |
| Pinned .NET tools (.NET monorepos) | `.config/dotnet-tools.json` |
| Affected builds: always-build list | `.claude/tipoca/affected/always-build.txt` |
| Affected builds in CI (new .NET monorepos, `/tipoca:init`) | `.github/workflows/tipoca-affected.yml` |
| Instruction mirrors: the Copilot bridges (`/tipoca:init --bridges`) and every `mirrors` entry in `.claude/tipoca/norm.json` | `.github/copilot-instructions.md`, `.github/instructions/`, `.github/prompts/`, `.github/pull_request_template.md`, `.vscode/extensions.json`, `.vscode/settings.json`, plus each declared target. Checked by `norm.mjs mirrors check` (gate `mirrors`), written only by `norm.mjs mirrors sync --write` in factory mode, never by a hook |

Other AI tools (Codex, Copilot) follow the mirrored instructions but run without Tipoca's guardrails: the hooks are Claude Code only.

**State root.** All state lives in the main checkout's `.claude/tipoca/state/`. Worktrees under `.claude/worktrees/` (where Tipoca creates them) share the main checkout's state and project site. A `git worktree add` outside that folder is not supported for this: outside the main checkout it fails closed and keeps its own state and site, and its specs are not synced.

Outside `.claude/`, Tipoca only writes `.specify/`, `specs/`, `docs/refactor/`, the site workflow and `docs/site/`,
`.config/dotnet-tools.json` (pinned .NET tools such as dotnet-affected) and the affected-build workflow, one import line in `CLAUDE.md` and `.gitignore`
entries (TIER0 §5).

## Design

If `.claude/design/override.md` exists it replaces the Tipoca default design completely — no
merging with `.claude/tipoca/design/`. `/tipoca:design-sync` refreshes the snapshot and never touches
the override. Its Superdesign source is a setting: `superdesign.team` and `superdesign.project`
(optional `superdesign.presets.{foundry,enterprise,cinematic}` node ids) in the git-ignored
`.claude/tipoca/config.local.json`, else `.claude/tipoca/config.json`, else the environment variables
`TIPOCA_SUPERDESIGN_TEAM` and `TIPOCA_SUPERDESIGN_PROJECT`. Without them the sync stops and explains.

### Design override tokens

The project site, the public site (`docs/site/`), the release-notes PDF and the visuals read the override's
tokens from `.claude/design/override.tokens.json`, or else from the first ```` ```json ```` block of
`.claude/design/override.md`. Only one file is used: `override.tokens.json` wins, and `override.md` is
used only when `override.tokens.json` is missing or not usable (keep one of them; the notice says so when
both exist). The contract is the plugin's `schemas/design-override.schema.json`; two formats are accepted.

**1. Simple format** (recommended for a project's own brand): `light` and `dark` maps of hex colours
(`#rgb`, `#rgba`, `#rrggbb`, `#rrggbbaa`), optional `fonts` and `radius`.

- Required in each theme: `canvas`, `surface`, `text`, `muted`, `line`, `primary`, `onPrimary`.
- Optional: `surfaceSoft`, `danger`, `success`, `warning`, `info`, `accent`, `onAccent`. Missing ones are
  derived (mixes of your colours, black or white for `on*`, neutral status colours picked by how light the
  theme's `canvas` is).
- Any other camelCase key is an extra accent (`--c-extra-<name>` on the project site, `--tp-extra-<name>` on
  the public site); `onX` is checked for contrast against `x`.
- `fonts.display` / `body` / `mono`: lists of font names, used as given. Fonts the site does not self-host
  show only where installed; install or host them yourself. Tipoca never loads fonts from a CDN.
- `radius`: one length (`12px`, `0.75rem`) or a map of `control`, `card`, `window`, `hero`.

```json
{
  "fonts": { "display": ["Example Display", "system-ui", "sans-serif"], "body": ["system-ui", "sans-serif"] },
  "radius": "12px",
  "light": { "canvas": "#f5f7fa", "surface": "#ffffff", "text": "#1d2733", "muted": "#5a6675", "line": "#d6dde5", "primary": "#22508a", "onPrimary": "#ffffff" },
  "dark":  { "canvas": "#0b1119", "surface": "#141c27", "text": "#e9eef4", "muted": "#a3afbd", "line": "#2c3a4c", "primary": "#8cb4e6", "onPrimary": "#0b1119" }
}
```

**2. Preset format**: W3C design tokens with `$value`, as in the Tipoca presets
(`templates/design/presets/<name>/tokens.json`):

```json
{ "color": { "dark": { "bg": { "canvas": { "$value": "#0b1119" } }, "accent": { "default": { "$value": "#8cb4e6" } } },
             "light": { "bg": { "canvas": { "$value": "#f5f7fa" } }, "accent": { "default": { "$value": "#22508a" } } } } }
```

Only plain values are accepted: anything else (named colours, `url(`, `expression(`, `;` or braces,
unbalanced quotes, line breaks, very long values, a block over 64 KB) is left out and named in the warning.
An override that cannot be read at all (for example a key that appears twice in one map) never breaks the
site: it falls back to the Tipoca design with a notice. Low contrast (below 4.5:1) is
reported but the values are used as given. When a key is missing or a value was left out, the project
site's **Team** view and `/tipoca:site status` say which; the stakeholder view never shows it.
`node "<plugin>/scripts/design-check.mjs"` (also run by `/tipoca:status`) reports whether the override is
usable by the site, the PDF and the visuals, and which keys are missing.

## Plugin updates

A SessionStart notice says when a newer Tipoca release is published (off for you only with
`site.mjs config set updates.check false`; per-user settings live in `${CLAUDE_PLUGIN_DATA}/settings.json`, never in the
project). Marketplace auto-update is opt-in: `/plugin` → Marketplaces → tipoca →
Enable auto-update turns it on for you only. `/tipoca:init --auto-update` or `/tipoca:adopt --auto-update` writes
`"autoUpdate": true` into this project's `.claude/settings.json` instead; that applies to everyone who trusts the folder and
overrides their own `/plugin` toggle for this marketplace.

## Keep worktrees out of builds and tooling

Each worktree under `.claude/worktrees/` is a full checkout. Tools that glob the repository would
otherwise see every project, test and config twice. Add only the snippets that match your stack.

**.NET** — in `Directory.Build.props` (or each `.csproj` that sits at the repo root):

```xml
<Project>
  <PropertyGroup>
    <DefaultItemExcludes>$(DefaultItemExcludes);.claude/**</DefaultItemExcludes>
  </PropertyGroup>
</Project>
```

Build solutions (`.sln`/`.slnx`), not folder globs such as `dotnet test **/*.Tests.csproj`. MSBuild
walks up from a worktree to find `Directory.Build.props`; commit it so every branch has its own copy
and a worktree build never imports the main checkout's file. SonarQube: `sonar.exclusions=.claude/**`.

**TypeScript** — `tsconfig.json` (setting `exclude` replaces the defaults, so keep them):

```json
{ "exclude": ["node_modules", "dist", ".claude"] }
```

**ESLint** (flat config) and Prettier:

```js
import { globalIgnores } from "eslint/config";
export default [globalIgnores([".claude/"]), /* ... */];
```

`.prettierignore`: `.claude/`

**Vitest / Jest**:

```js
// vitest.config.ts
import { configDefaults, defineConfig } from "vitest/config";
export default defineConfig({ test: { exclude: [...configDefaults.exclude, ".claude/**"] } });

// jest.config.js — modulePathIgnorePatterns also avoids duplicate package.json name collisions
module.exports = {
  testPathIgnorePatterns: ["/node_modules/", "<rootDir>/.claude/"],
  modulePathIgnorePatterns: ["<rootDir>/.claude/"],
};
```

**pytest** — the default `norecursedirs` already skips dot-folders. If you override it, keep `.*`:

```toml
[tool.pytest.ini_options]
norecursedirs = [".*", "node_modules", "build", "dist", "venv"]
```

**Godot** — an empty `.claude/.gdignore` makes the editor skip the whole folder (`/tipoca:init`
and `/tipoca:adopt` create it when `project.godot` exists at the root).

**Editors** — VS Code `settings.json`: `"search.exclude": { ".claude/worktrees": true }` and the
same key under `files.watcherExclude`.

## Project-specific rules

<!-- Add rules for this project below. Keep them short; link to docs instead of copying them. -->
