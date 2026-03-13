---
name: triage-pipelines
description: Get Azure Pipelines build information, failures, and logs.
---

## How to investigate pipeline failures

All scripts default to the `internal` project. Use `--project public` (or `-p public`) to query the `public` project instead.

```shell
# List all pipelines in this repo where the latest build is failing
dotnet scripts/GetFailingPipelines.cs dotnet/docker-tools

# Show all failing tasks in a pipeline
dotnet scripts/GetBuildTimeline.cs 2999999 --failing

# Show all stages, jobs, and tasks in a pipeline
dotnet scripts/GetBuildTimeline.cs 2999999 -d 3

# Get the log for a specific task in a pipeline
dotnet scripts/GetTaskLog.cs 2999999 42
```
