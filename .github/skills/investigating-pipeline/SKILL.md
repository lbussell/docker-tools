---
name: investigating-pipeline
description: >-
  Diagnoses a single Azure Pipelines build. Shows the build timeline tree with stages, jobs, and
  task results, and retrieves task logs for debugging failures. Use when a user provides a build ID
  or Azure DevOps build URL and wants to understand what failed and why.
---

## Workflow

All scripts default to the `internal` Azure DevOps project. Use `--project public` (or `-p public`) to query `dnceng-public` instead.

### Step 1: View the build timeline

```shell
dotnet scripts/GetBuildTimeline.cs <buildId>
```

This prints a tree of stages, jobs, and tasks. By default only failing tasks are shown. Use `--show-all` to see everything.

Each node includes a log ID (e.g., `Task #42`). Note the log IDs of failing tasks for the next step.

### Step 2: Read a failing task's log

```shell
dotnet scripts/GetTaskLog.cs <buildId> <logId>
```

This prints the full log for a specific task. Use this to understand the root cause of a failure.
