---
name: triage-pipelines
description: >-
  Lists all failing Azure Pipelines in a folder for daily triage. Identifies pipelines where the
  latest completed build has failed and provides links. Use for daily pipeline health checks across
  a repository's pipeline folder.
---

## Workflow

### Step 1: List all failing pipelines

```shell
dotnet scripts/GetFailingPipelines.cs dotnet/docker-tools
```

### Step 2: Investigate each failure

For each failing pipeline in the output, use the `investigating-pipeline` skill with the build ID to see the timeline and read task logs.
