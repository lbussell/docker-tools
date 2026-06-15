# Microsoft.DotNet.DockerTools.Infrastructure

This project carries a copy of the repository's `eng/docker-tools/` infrastructure (Azure
Pipelines templates, PowerShell scripts, and docs) and embeds it into the assembly as
resources. ImageBuilder references this project so it can ship these files inside its build
and write them back out to disk via the `update` command.

## Why

ImageBuilder is both the producer and a consumer of pipeline templates. Bundling the
templates with ImageBuilder lets source-code changes and pipeline-template changes live in
the same commit, rather than being split across the two-phase update process (ship a new
ImageBuilder, then a follow-up PR to change the pipelines).

See [issue #2130](https://github.com/dotnet/docker-tools/issues/2130) for background.

## Layout

- `content/` — a copy of `eng/docker-tools/`. These files are embedded as resources, with
  each resource's `LogicalName` preserving its path relative to `content/`.
- `InfrastructureContent.cs` — API to enumerate the embedded files and read their bytes.

The copy must live under `src/` because the ImageBuilder Docker build context is `src/`,
which means `eng/docker-tools/` is not reachable from the container build.

## Keeping the copy in sync

`content/` and `eng/docker-tools/` must be kept identical. Either:

- Edit both locations together, or
- Edit `content/` and regenerate `eng/docker-tools/` by running the `update` command from the
  repo root:

  ```pwsh
  dotnet run --project src/ImageBuilder -- update --no-version-logging
  ```

  `update` must be run from the root of a git repository and always writes to `eng/docker-tools`.
  It performs a full mirror — files that ImageBuilder no longer ships are deleted and empty
  directories are pruned. If `eng/docker-tools` does not yet exist, pass `--init` to create it
  (used when onboarding a repo).
