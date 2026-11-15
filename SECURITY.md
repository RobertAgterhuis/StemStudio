# Security policy

StemStudio is a local, single-user tool. Do not expose its ports (8080, 8081) to the internet.

## Reporting a vulnerability

Please report vulnerabilities privately through GitHub's
[private vulnerability reporting](https://github.com/RobertAgterhuis/StemStudio/security/advisories/new).
Do not open a public issue for security problems. You can expect an acknowledgement within a week.

## Supported versions

Only the latest `main` is supported.

## Secrets

StemStudio never stores secrets in files. The optional Claude backend reads its key only from the
`ANTHROPIC_API_KEY` environment variable; CI uses repository secrets only (for example `SONAR_TOKEN`).
Secret scanning with push protection is enabled on this repository.
