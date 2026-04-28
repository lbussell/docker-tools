# Image Info Merge Repro Summary

This is a handoff for the next coding agent about the local reproduction of an Azure Pipelines `mergeImageInfo` run.

## Pipeline run

- URL: <https://dev.azure.com/dnceng/internal/_build/results?buildId=2957279&view=results>
- Build ID: `2957279`
- Build number: `20260421.2`
- Definition: `dotnet-docker-nightly-official`
- Repository: `dotnet-dotnet-docker`
- Source branch: `refs/heads/nightly`
- Source version: `9e2517879c2d9912ee023efcd3caba010bd89176`

## Local files

Downloaded and staged files are under the repo-root `artifacts/` directory, which is gitignored:

```text
artifacts/build-2957279-image-info/
```

Important subdirectories:

```text
artifacts/build-2957279-image-info/artifacts/          # downloaded Azure artifacts
artifacts/build-2957279-image-info/post-build-input/   # staged per-leg image-info merge inputs
artifacts/build-2957279-image-info/dotnet-docker-src/  # sparse checkout of dotnet/dotnet-docker at 9e251787...
artifacts/build-2957279-image-info/local-merge/        # local merge output
```

The staged post-build merge inputs are the six per-leg image-info artifacts:

```text
linuxamd64src-runtime-deps-8.0-bookworm-slim-graph-image-info-1/linuxamd64src-runtime-deps-8.0-bookworm-slim-graph-image-info.json
linuxamd64src-runtime-deps-9.0-bookworm-slim-graph-image-info-1/linuxamd64src-runtime-deps-9.0-bookworm-slim-graph-image-info.json
linuxarm32src-runtime-deps-8.0-bookworm-slim-arm32v7-graph-image-info-1/linuxarm32src-runtime-deps-8.0-bookworm-slim-arm32v7-graph-image-info.json
linuxarm32src-runtime-deps-9.0-bookworm-slim-arm32v7-graph-image-info-1/linuxarm32src-runtime-deps-9.0-bookworm-slim-arm32v7-graph-image-info.json
linuxarm64src-runtime-deps-8.0-bookworm-slim-arm64v8-graph-image-info-1/linuxarm64src-runtime-deps-8.0-bookworm-slim-arm64v8-graph-image-info.json
linuxarm64src-runtime-deps-9.0-bookworm-slim-arm64v8-graph-image-info-1/linuxarm64src-runtime-deps-9.0-bookworm-slim-arm64v8-graph-image-info.json
```

Also downloaded:

```text
artifacts/build-2957279-image-info/artifacts/image-info/image-info.json
artifacts/build-2957279-image-info/artifacts/image-info-final-1/image-info.json
```

## Local merge command

Use the checked-out `dotnet/dotnet-docker` manifest, not the copied standalone manifest. `mergeImageInfo` validates manifest includes, readmes, MCR metadata templates, Dockerfiles, and Dockerfile templates, so a standalone `manifest.json` download is not sufficient.

```bash
dotnet run --project src/ImageBuilder -- mergeImageInfo \
  --manifest artifacts/build-2957279-image-info/dotnet-docker-src/manifest.json \
  artifacts/build-2957279-image-info/post-build-input \
  artifacts/build-2957279-image-info/local-merge/image-info.json
```

The local merge output summary was:

```json
{
  "repos": 4,
  "images": 8,
  "platforms": 24
}
```

## Comparison result

The local `mergeImageInfo` output is not byte-for-byte identical to the downloaded `image-info/image-info.json` artifact because the pipeline runs `createManifestList` after `mergeImageInfo` and before publishing the `image-info` artifact. That later step adds `manifest.digest` and `manifest.created` to each image manifest entry.

After normalizing away those fields, the local output matches the downloaded post-build artifact:

```bash
diff -u \
  <(jq -S '(.repos[].images[].manifest) |= del(.digest, .created)' \
    artifacts/build-2957279-image-info/artifacts/image-info/image-info.json) \
  <(jq -S '(.repos[].images[].manifest) |= del(.digest, .created)' \
    artifacts/build-2957279-image-info/local-merge/image-info.json)
```

The normalized comparison result was:

```text
MATCH after removing manifest digest/created fields
```

## Relevance for future image-info merge tests

This run is a realistic post-build merge fixture. It demonstrates that actual pipeline `mergeImageInfo` inputs are per-leg fragment files grouped under artifact directories, then merged into one current-run image-info file. The published post-build artifact may include post-merge manifest-list metadata, so tests that target only `mergeImageInfo` should compare against the raw local merge output or normalize away `manifest.digest` and `manifest.created`.
