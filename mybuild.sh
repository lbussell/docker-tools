#!/usr/bin/env bash

# repo="dotnetdockerdev.azurecr.io/dotnet-buildtools/image-builder/mec"
repo="dotnetdockerdev.azurecr.io/dotnet-buildtools/image-builder/no-static-token"
tag=$(date +"%Y%m%d-%H%M")

docker buildx build --push --platform linux/amd64,linux/arm64 -t $repo:$tag -t $repo:latest -f ./src/Dockerfile.linux src/
