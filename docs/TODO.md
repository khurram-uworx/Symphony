# Plan: T8C/T8D — sample compose + container docs (#16, #17)

## Problem

- #16: no ready-to-run compose file using the published GHCR image with PostgreSQL.
- #17: no container usage docs/env table; README/AGENTS still describe legacy console poller.

## Decisions (human-confirmed)

- Branch: `khurram/16-17`.
- Sample env file: `samples/.env` (not `.env.example`) for a seamless first `docker compose up`.
- README: remove legacy polling mention entirely from product docs.

## Proposed changes

1. `samples/docker-compose.yml` — postgres:16 + `ghcr.io/khurram-uworx/symphony:latest`, env from `.env`, health checks, volumes `pgdata` + `dataprotection`, port `8080:8080`.
2. `samples/.env` — sample-only default credentials and connection string (must be changed).
3. `CONTAINER-README.md` — image usage, env table, first-admin output, persistence, sample commands, secret handling.
4. `README.md` — replace Getting Started/Running/How It Works with web host + published image; remove console poller.
5. `AGENTS.md` — align primary docs/run notes with container story.

## Blast radius

Docs/compose/env only. No C# or workflow changes. No test code changes expected.

## Verification

- `docker compose -f samples/docker-compose.yml config`
- Pull + run, check admin creds in logs, login + greeting.
- Grep README/AGENTS for console-poller product wording.

## Planned commits

1. docs: plan T8C/T8D in TODO.md
2. feat: add samples/docker-compose.yml and samples/.env
3. docs: add CONTAINER-README.md
4. docs: rewrite README for web host and published image
5. docs: align AGENTS.md with container story
6. docs: remove TODO.md — plan executed

## GitHub issues log

(none yet)
" path="docs/TODO.md