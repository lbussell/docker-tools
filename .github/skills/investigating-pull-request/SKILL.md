---
name: investigating-pull-request
description: >-
  Shows the Azure Pipelines build status for a single GitHub pull request. Displays PR metadata
  (title, author, fork, branch) and renders build timeline trees for each pipeline run. Use when a
  user provides a PR number or URL and wants to check its CI status or diagnose failures.
---

## Usage

```shell
# By PR number (auto-detects repo from current git remote)
dotnet scripts/GetPullRequestStatus.cs 2016

# By PR number with explicit repo
dotnet scripts/GetPullRequestStatus.cs 2016 --repo dotnet/docker-tools

# Show all tasks, not just failing ones
dotnet scripts/GetPullRequestStatus.cs 2016 --show-all
```

The script outputs PR metadata followed by a build timeline tree for each Azure Pipelines run. Non-Azure checks (e.g., CLA bots, GitHub Actions) are listed separately at the end.

To dig deeper into a specific failing build, use the `investigating-pipeline` skill with the build ID shown in the output.
