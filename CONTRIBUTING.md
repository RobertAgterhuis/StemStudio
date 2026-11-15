# Contributing

StemStudio is built with GitHub Spec Kit and a strict quality bar
(`.specify/memory/constitution.md`). In short:

1. **Spec first**: every feature has `specs/NNN-*/` (spec, plan, contracts, tasks).
2. **Contracts first**: change the versioned contract before the code.
3. **Test first**: commit a failing test before the implementation (red -> green).
4. **Gates**: >= 90 % line and branch coverage per component, SIG maintainability limits,
   analyzers as errors, linters and type checkers clean. Run everything with
   `./scripts/test-all.ps1` (or `scripts/test-all.sh`).
5. **Docs**: user-visible or setup changes update `docs/public` and regenerate the PDFs
   (`node tools/docs/check.mjs` must pass).
6. **Licences**: open-source code and libraries only (see the constitution for the documented
   exceptions); record every new dependency in `docs/THIRD-PARTY-LICENSES.md`.
7. **Commits**: Conventional Commits; no secrets anywhere.

Pull requests run the full CI (build, tests, coverage gates, quality gate, contracts, E2E,
visual, accessibility and Lighthouse checks) and need a green run before merging.
