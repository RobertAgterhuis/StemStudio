# Hand-off: devops / 002 polish (T087)

## What was built
Validation of the 002 compose, CI and e2e-real changes from T008 (commit 3730219,
`docs/handoffs/devops-002-foundational.md`). No further file changes were needed.

## Files changed
- `specs/002-detail-separation/tasks.md` (T087 ticked)
- this note

## Tests and coverage
No unit-tested code owned by devops; the coverage gate does not apply. Validation output:

```
docker compose config -q                                                  -> exit 0
docker compose -f docker-compose.yml -f docker-compose.gpu.yml config -q  -> exit 0
STEMSTUDIO_MODEL_CACHE_DIR=/tmp/mc docker compose -f docker-compose.yml -f docker/compose.model-cache-bind.yml config -q -> exit 0
docker compose build worker -> "Image stemstudio/worker:local Built", exit 0
image check: onnxruntime 1.32.0 (CPUExecutionProvider), rotary-embedding-torch 0.9.2, torch 2.15.0+cpu,
             STEMSTUDIO_MODEL_DIR=/models
npx @redocly/cli@2.78.0 lint specs/002-detail-separation/contracts/* -> valid, 3 warnings, exit 0
```

## Quality checks
```
actionlint 1.7.12 -verbose .github/workflows/ci.yml .github/workflows/e2e-real.yml -> "Found 0 errors in 2 files", exit 0
shellcheck v0.11.0 scripts/dev.sh scripts/test-all.sh -> exit 0
PSScriptAnalyzer 1.25.0 scripts/ -> 0 warnings, 0 errors (6 pre-existing Information findings)
```

## Dependencies added
None.

## Open issues / requests
- Not verified here (needs the wave's code): the e2e-real run itself (T010 fixture script and
  T050 detail integration test must exist; see the foundational note, items 1-3). The detail
  step fails by design (`pytest` exit 4) while `worker/tests/detail` is missing.
- `scripts/dev.{ps1,sh} --smoke-test` was not run: ports 8080/8081 are held by the running
  compose stack, which was left untouched as instructed. The change is two env exports.

## Next owner
CTO (integration wave: run e2e-real via workflow_dispatch once T010/T050 are merged).
