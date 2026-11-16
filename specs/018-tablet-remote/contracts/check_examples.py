"""Validate the remote-protocol example messages against remote-messages.schema.json.

Usage (repo root):
    uv run --no-project --with "jsonschema[format]==4.25.1" python -I \
        specs/018-tablet-remote/contracts/check_examples.py \
        specs/018-tablet-remote/contracts/remote-messages.schema.json \
        specs/018-tablet-remote/contracts/remote-examples

Every file in ``valid/`` must validate and every file in ``invalid/`` must fail (exit code 1 otherwise).
"""

import json
import pathlib
import sys

import jsonschema

schema_path, examples = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
schema = json.loads(schema_path.read_text(encoding="utf-8"))
jsonschema.Draft202012Validator.check_schema(schema)
validator = jsonschema.Draft202012Validator(schema, format_checker=jsonschema.FormatChecker())
failures = 0
for path in sorted((examples / "valid").glob("*.json")):
    errors = list(validator.iter_errors(json.loads(path.read_text(encoding="utf-8"))))
    if errors:
        failures += 1
        print("VALID FAILED", path.name, errors[0].message[:200])
for path in sorted((examples / "invalid").glob("*.json")):
    if validator.is_valid(json.loads(path.read_text(encoding="utf-8"))):
        failures += 1
        print("INVALID PASSED", path.name)
print("failures", failures)
sys.exit(1 if failures else 0)
