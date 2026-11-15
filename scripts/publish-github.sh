#!/usr/bin/env bash
# Publish the local `main` to GitHub with author/committer e-mails mapped through the local,
# git-ignored `.publish.mailmap` (format: "Name <public@email> <local@email>").
# The rewrite is deterministic, so every publish is a fast-forward of the previous one.
# Requires: git, uv (runs git-filter-repo 2.47.0 without installing it).
set -euo pipefail
repo_root="$(git rev-parse --show-toplevel)"
mailmap="${repo_root}/.publish.mailmap"
remote="${PUBLISH_REMOTE:-https://github.com/RobertAgterhuis/StemStudio.git}"
[[ -f "${mailmap}" ]] || { echo "missing ${mailmap}" >&2; exit 2; }
work="$(mktemp -d)"
trap 'rm -rf "${work}"' EXIT
git clone --quiet --no-local --single-branch --branch main "${repo_root}" "${work}/publish"
cd "${work}/publish"
uvx --quiet git-filter-repo==2.47.0 --mailmap "${mailmap}" --force >/dev/null
if git log --format='%ae%n%ce' | grep -vqxf <(sed -E 's/.*<([^>]+)> <[^>]+>$/\1/' "${mailmap}"); then
  echo "unmapped e-mail address in history; refusing to publish" >&2; exit 3
fi
git push "${remote}" main
