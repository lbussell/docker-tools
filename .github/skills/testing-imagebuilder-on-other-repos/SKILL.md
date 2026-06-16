---
name: testing-imagebuilder-on-other-repos
description: >-
  End-to-end workflow for testing a local ImageBuilder build against a consuming
  repo (e.g. dotnet-docker) via the dev registry and an unofficial pipeline.
  Builds ImageBuilder, pushes it to dotnetdockerdev.azurecr.io, points the
  consumer's docker-images.yml at the dev tag, and queues a pipeline run. Use
  when a user wants to validate ImageBuilder changes in another repo's pipeline,
  test an ImageBuilder dev/prerelease build, or push ImageBuilder to the dev ACR
  for a downstream build.
---

# Testing ImageBuilder on other repos

Validate local ImageBuilder changes by running a consuming repo's pipeline
against a dev build of ImageBuilder. Run the steps in order.

## 1. Build ImageBuilder and push to the dev registry

Tagging scheme on `dotnetdockerdev.azurecr.io/dotnet-buildtools/image-builder`
is `<branch>` (mutable) + `<branch>-<UTC-timestamp>` (immutable). Check existing
tags first: `az acr repository show-tags --name dotnetdockerdev --repository
dotnet-buildtools/image-builder --orderby time_desc --top 20 -o table`.

From the **docker-tools repo root** (requires `az login` and a running Docker):

```bash
TAG=<branch>            # e.g. issue-2141
TS=$(date -u +%Y%m%d%H%M%S)
IMAGE=dotnetdockerdev.azurecr.io/dotnet-buildtools/image-builder

az acr login --name dotnetdockerdev
docker build --platform linux/amd64,linux/arm64 \
  -t "$IMAGE:$TAG" -t "$IMAGE:$TAG-$TS" \
  -f src/Dockerfile.linux --push src
```

This pushes a multi-arch **Linux** image (amd64+arm64; no Windows on macOS/arm64,
matching existing dev tags). Pin the immutable `$IMAGE:$TAG-$TS` tag downstream.
Multi-platform `--push` needs the containerd image store (default in recent
Docker Desktop).

## 2. Point the consumer repo at the dev tag, push the branch

In the consumer repo (e.g. dotnet-docker), edit
`eng/docker-tools/templates/variables/docker-images.yml`:

```yaml
imageNames.imageBuilderName: dotnetdockerdev.azurecr.io/dotnet-buildtools/image-builder:<branch>-<timestamp>
```

Commit and push the branch to the internal AzDO remote the pipeline builds from
(e.g. `git push dnceng <branch>`).

## 3. Enable anonymous pull on the dev ACR

Pipeline agents can't auth to the dev registry, so enable anonymous pull
**before** queuing, and **disable it after** the run completes (see step 5):

```bash
az acr update --name dotnetdockerdev --anonymous-pull-enabled true
```

## 4. (Linux-only) Filter out Windows images

The dev tag has no Windows ImageBuilder image, so restrict the consumer's build
to Linux. **Do not** use `--os-type linux` — matrix generation already passes
`--os-type '*'` and System.CommandLine rejects the duplicate. Instead set
`imageBuilder.pathArgs` with `--path` include globs covering every Linux distro
family in the manifest (verify against `manifest.json` that only
`nanoserver`/`windowsservercore` paths are excluded):

```yaml
- name: "imageBuilder.pathArgs"
  value: "--path *alpine* --path *azurelinux* --path *bookworm* --path *jammy* --path *noble* --path *resolute* --path *ubuntu* --path *aspire-dashboard*"
```

Add this in the pipeline yaml (e.g. `dotnet-docker-nightly-unofficial.yml`),
commit, and push. See [REFERENCE.md](REFERENCE.md) for how to derive and verify
the path set.

## 5. Queue the pipeline and clean up

```bash
az pipelines run --org https://dev.azure.com/dnceng --project internal \
  --id <pipelineId> --branch <branch> --parameters noCache=true \
  --query "{id:id,url:url}" -o json
```

Verify branch + params: `az devops invoke --area pipelines --resource runs ...`.
Builds take hours — poll status, and once `status == completed`, **disable
anonymous pull**:

```bash
az acr update --name dotnetdockerdev --anonymous-pull-enabled false
```

A recurring scheduled check is a good way to auto-disable on completion.

## Verifying results & gotchas

For inspecting the pipeline timeline/logs use the `investigating-pipeline`
skill. Common gotchas (`--os-type` duplicate, ACR **throttling/429** during
publish on Standard SKU) are in [REFERENCE.md](REFERENCE.md).
