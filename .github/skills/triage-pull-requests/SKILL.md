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
gh pr list --state open --json number,title,author,headRefName
```

### Step 2: Check each PR's pipeline status

For each open PR, run the `GetPullRequestStatus.cs` script from the `investigating-pull-request` skill:

```shell
dotnet .github/skills/investigating-pull-request/scripts/GetPullRequestStatus.cs <number>
```

### Step 3: Focus on failures

Prioritize PRs where pipeline runs show `FAIL` results. For each failing build, use the `investigating-pipeline` skill to read task logs and diagnose the root cause.
