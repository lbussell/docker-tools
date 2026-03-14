---
name: triage-pull-requests
description: >-
  Reviews the CI status of all open pull requests in the repository. Lists open PRs via the GitHub
  CLI, then checks each one's pipeline status. Use for daily PR triage to identify PRs with failing
  CI that need attention.
---

## Workflow

### Step 1: List open pull requests

```shell
gh pr list
```

### Step 2: Check each pull request's status

```shell
gh pr view -c 1234
```

If a pull request has failing checks, run the `GetPullRequestStatus.cs` script from the `investigating-pull-request` skill:

```shell
dotnet .github/skills/investigating-pull-request/scripts/GetPullRequestStatus.cs <number>
```

Prioritize pull requests where pipeline runs show `FAIL` results. To get failing logs from Azure Pipelines checks, run the `GetTaskLog.cs` script using the `investigating-pipeline` skill. Investigate the failure and diagnose the root cause.

### Step 3: Summary

For each pull request, determine:

- Is this pull request ready to merge? If not, why?
- Is this pull request stale? (1+ week old)
- If there are failures, what is the root cause?
- What comments still need to be addressed? Provide links to specific comments.

