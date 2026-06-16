# Ship docker-tools infrastructure in-box with ImageBuilder

To couple ImageBuilder source-code changes with the pipeline-template changes they require, ImageBuilder now carries its own copy of the entire `eng/docker-tools/` directory and can write it back to disk via the `update` command. This lets both kinds of change land in a single commit instead of being split across the two-phase process (ship a new ImageBuilder, then a follow-up PR to change the pipelines). See [issue #2130](https://github.com/dotnet/docker-tools/issues/2130).

## Consequences

- The repository now carries **two copies** of the infrastructure: the canonical `eng/docker-tools/` and an embedded copy under `src/Infrastructure/content/`. They must be kept in sync; there is no automated test enforcing this (running `update` regenerates `eng/docker-tools/` from the embedded copy).
- The copy lives under `src/` — not next to `eng/docker-tools/` — because the ImageBuilder Docker build context is `src/`, so `eng/docker-tools/` is not reachable from the container build. This placement is otherwise surprising and exists solely to satisfy the build context.
- Pipeline wiring (having the automated ImageBuilder-tag-bump PR run `imagebuilder update`) is a deliberate follow-up, not part of this change.
