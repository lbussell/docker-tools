# Embed infrastructure content as resources, not generated code

The bundled `eng/docker-tools/` content is shipped as embedded resources and read at runtime via the `Assembly` manifest-resource APIs, rather than being compiled into the assembly as string/byte literals by a source generator. These APIs are part of the Native AOT / trim-safe subset of reflection (embedded resources are preserved by the trimmer), so the approach is already AOT compatible while staying far simpler than build-time code generation.

## Considered Options

- **Source generator** — a `netstandard2.0` incremental generator emitting compiled-in data. Fully reflection-free, but adds an analyzer project plus a Roslyn package dependency for no AOT benefit here.
- **MSBuild `RoslynCodeTaskFactory` codegen** — reflection-free with no new project, but embeds hard-to-test C# in the csproj.

## Consequences

- `src/Infrastructure/` sets `IsAotCompatible=true`, which enables the trim/AOT/single-file analyzers and **fails the build** if genuinely AOT-unsafe reflection is introduced later.
- The remaining reflection (`GetManifestResourceNames`/`GetManifestResourceStream`) is intentional and AOT-safe. A future reader aiming for AOT should not "fix" it by switching to a source generator without a concrete reason.
