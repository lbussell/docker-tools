---
name: investigating-pull-request
description: >-
  Shows the PR validation status for a single GitHub pull request.
  Displays PR comments and reviews and fetches Azure Pipelines build logs.
  Use when a user provides a PR number or URL and wants to check its CI status or diagnose failures.
---

## Usage

View pull request status:

```shell
gh pr view -c 1234
```

If a pull request has failing checks, run the `GetPullRequestStatus.cs` script from the `investigating-pull-request` skill:

```shell
dotnet .github/skills/investigating-pull-request/scripts/GetPullRequestStatus.cs <number>
```

Prioritize pull requests where pipeline runs show `FAIL` results.
To get failing logs from Azure Pipelines checks, run the `GetTaskLog.cs` script using the `investigating-pipeline` skill.
Investigate the failure and diagnose the root cause.

```shell
# By PR number (auto-detects repo from current git remote)
dotnet scripts/GetPullRequestStatus.cs 2016

# By PR number with explicit repo
dotnet scripts/GetPullRequestStatus.cs 2016 --repo dotnet/docker-tools

# Show all tasks, not just failing ones
dotnet scripts/GetPullRequestStatus.cs 2016 --show-all
```

The script outputs PR metadata followed by a build timeline tree for each Azure Pipelines run. Non-Azure checks (GitHub Actions, etc.) are listed separately at the end.

To dig deeper into a specific failing build, use the `investigating-pipeline` skill with the build ID shown in the output.

