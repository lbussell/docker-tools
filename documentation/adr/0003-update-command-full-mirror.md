# `update` command mirrors to a fixed, git-rooted output

The `update` command must be run from the root of a git repository and always targets `eng/docker-tools` relative to that root (there is no `--output` option). It performs a full mirror — deleting files the current ImageBuilder no longer ships and pruning empty directories — so the output exactly matches the embedded content; this guarantees consuming repos converge on the shipped state rather than accumulating stale templates.

## Consequences

- `update` **deletes files** under `eng/docker-tools`. The fixed output path plus the git-root requirement are the guardrails against destructive deletion in an unexpected location.
- If `eng/docker-tools` does not already exist the command fails unless `--init` is passed, so the destructive mirror cannot silently scaffold a directory in a non-onboarded repo while still supporting intentional onboarding.
