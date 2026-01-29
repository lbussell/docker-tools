---
name: dotnet-inspect
description: Inspects .NET assemblies and NuGet packages. Use when understanding package contents, comparing API surfaces between versions, or auditing assemblies for SourceLink/determinism.
---

# dotnet-inspect

A CLI tool for inspecting .NET assemblies and NuGet packages. Useful for understanding package contents, comparing API surfaces between versions, and auditing assemblies for SourceLink/determinism.

## Installation

Install as a global tool:

```bash
dotnet tool install -g dotnet-inspect
```

Or run without installing:

```bash
dnx dotnet-inspect
```

## Quick Start

Inspect a NuGet package:

```bash
dotnet-inspect package System.Text.Json
```

View public API of a type:

```bash
dotnet-inspect api JsonSerializer --package System.Text.Json
```

Compare APIs between versions:

```bash
diff <(dotnet-inspect api JsonSerializer --package System.Text.Json@9.0.0) \
     <(dotnet-inspect api JsonSerializer --package System.Text.Json@10.0.2)
```

Audit assembly for SourceLink/determinism:

```bash
dotnet-inspect assembly --package System.Text.Json --tfm net8.0 --audit
```

Inspect a tool package (dotnet-inspect inspecting itself):

```bash
dotnet-inspect package dotnet-inspect --files --all
```

## Commands

### `package`

Inspect NuGet packages - view metadata, dependencies, and file structure.

```bash
# Package metadata
dotnet-inspect package System.Text.Json

# List DLLs
dotnet-inspect package System.CommandLine --files

# List available versions
dotnet-inspect package System.CommandLine --versions

# Inspect tool packages
dotnet-inspect package dotnet-inspect --files --all
```

### `assembly`

Inspect .NET assemblies - view assembly info and audit for SourceLink/determinism.

```bash
dotnet-inspect assembly MyLib.dll --audit
dotnet-inspect assembly --package System.Text.Json --tfm net8.0 --audit
dotnet-inspect assembly --package dotnet-inspect     # dotnet-inspect inspecting itself
```

### `api`

View public API surface of assemblies or specific types.

```bash
# List all types
dotnet-inspect api --package System.CommandLine

# Filter by glob pattern
dotnet-inspect api --package System.CommandLine --filter "Command*"

# Specific type
dotnet-inspect api JsonSerializer --package System.Text.Json

# Generic types (C#-style syntax)
dotnet-inspect api 'Option<T>' --package System.CommandLine

# Filter to member
dotnet-inspect api JsonSerializer --package System.Text.Json -m Deserialize

# Show constructors with details
dotnet-inspect api Command --package System.CommandLine --ctor

# Include hidden/obsolete
dotnet-inspect api JsonSerializer --package System.Text.Json --all

# Unsafe methods only
dotnet-inspect api Unsafe --package System.Runtime.CompilerServices.Unsafe --unsafe

# With documentation
dotnet-inspect api Command --package System.CommandLine --docs

# Source URL only, no members
dotnet-inspect api JsonSerializer --package Newtonsoft.Json --source-url --fields-only

# dotnet-inspect inspecting itself
dotnet-inspect api CommandLineBuilder --package dotnet-inspect
```

### `type`

View type shape with hierarchy, interfaces, and members in tree format.

```bash
# Inheritance, interfaces, members
dotnet-inspect type JsonSerializer --package System.Text.Json

# Shows base class and interfaces
dotnet-inspect type Command --package System.CommandLine

# JSON output
dotnet-inspect type JsonSerializer --package System.Text.Json --json
```

### `diff`

Compare API surfaces between package versions with semantic awareness.

```bash
# Compare type between versions
dotnet-inspect diff JsonSerializer --package System.Text.Json@9.0.0..10.0.0

# See what changed
dotnet-inspect diff Command --package System.CommandLine@2.0.1..2.0.2
```

## Key Features

- **Package inspection**: View metadata, dependencies, target frameworks, and file structure
- **API surface extraction**: List types and members with full signatures including parameter names
- **Generic type support**: Use C#-style syntax (`Option<T>`) or CLR backtick notation (`` Option`1 ``)
- **Constructor emphasis**: `--ctor` shows constructors with parameter details (required vs optional)
- **API diff**: Compare type APIs between package versions with semantic awareness
- **Type hierarchy**: View inheritance chains and implemented interfaces in tree format
- **Type filtering**: Filter types by glob pattern (e.g., `--filter "*Json*")
- **Smart defaults**: Excludes `[EditorBrowsable(Never)]` and `[Obsolete]` members by default
- **Unsafe code filtering**: Filter to methods with pointer signatures using `--unsafe`
- **Version comparison**: Compare APIs between package versions using diff
- **SourceLink support**: Fetch source URLs and documentation from Portable PDBs (embedded or .snupkg)
- **Fields-only mode**: Show only type info (source URL, docs) without member tables via `--fields-only`
- **Multiple output formats**: Markdown tables, tree view, signatures-only, or JSON
- **Smart TFM selection**: Auto-selects highest target framework when multiple exist
- **Caching**: Reads from NuGet cache and caches downloads for fast repeated access

## Output Formats

- **Markdown** (default): Human-readable tables, powered by Markout
- **Signatures only** (`--signatures-only`): Plain method signatures, minimal tokens
- **JSON** (`--json`): Machine-readable output
- **Compact JSON** (`--json --compact`): Minified, omits defaults

## Output Control

Output verbosity follows a **height × width** model for progressive disclosure:

- **Width (verbosity)** controls column density: `-v:q` (quiet) → `-v:d` (detailed)
- **Height (sections)** controls which sections appear: `-s:1,2` (include) or `-x:3` (exclude)

This lets you dial in exactly the information you need. Run a command once to see section numbers, then filter.

```bash
# Detailed: all sections, full tables
dotnet-inspect package System.Text.Json -v:d

# Section 1 only (metadata)
dotnet-inspect package System.Text.Json -s:1

# Detailed, but skip dependencies
dotnet-inspect package System.Text.Json -v:d -x:2
```

### Verbosity Levels

| Flag   | Level    | Description                  |
|--------|----------|------------------------------|
| `-v:q` | Quiet    | Summary only                 |
| `-v:m` | Minimal  | Summary + compact metadata   |
| `-v:n` | Normal   | Full sections (default)      |
| `-v:d` | Detailed | All sections with full tables|
