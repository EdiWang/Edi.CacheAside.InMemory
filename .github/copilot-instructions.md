# Copilot Instructions

This repository is a small .NET class library that implements the cache-aside pattern on top of `IMemoryCache`. Keep changes focused, minimal, and compatible with the existing public API unless the user explicitly asks for a breaking change.

## Project Shape

- The solution lives under `src/` and currently targets `net10.0`.
- The library project is `src/Edi.CacheAside.InMemory/Edi.CacheAside.InMemory.csproj`.
- Unit tests live in `src/Edi.CacheAside.InMemory.Tests/` and use xUnit v3 plus Moq.
- The package is built as a NuGet package with metadata in the library `.csproj`; avoid changing package metadata unless the task requires it.
- Build and CI commands run from `src/`.

## Core Design Rules

- `MemoryCacheAside` is the main implementation of `ICacheAside`; preserve the cache-aside behavior of first checking cache, then executing the factory, then storing the value.
- Preserve partitioned cache management. Entries are tracked by partition so callers can remove one key, one partition, or everything.
- Preserve the cache key format `partition::key` unless intentionally making a coordinated breaking change with tests and documentation.
- Keep operations thread-safe. Use concurrent collections and per-key locking patterns consistently with the existing implementation.
- Keep stampede protection intact: concurrent `GetOrCreate` or `GetOrCreateAsync` calls for the same partition/key should execute the factory only once.
- Keep disposal behavior predictable: public operations should throw `ObjectDisposedException` after disposal, and `Dispose` should be idempotent.
- This package is for in-memory, non-distributed cache scenarios. Do not introduce distributed cache dependencies or background infrastructure without an explicit requirement.

## Coding Style

- Use file-scoped namespaces, nullable-aware C#, implicit usings, and the concise style already present in the repository.
- Prefer `ArgumentException.ThrowIfNullOrWhiteSpace` and `ArgumentNullException.ThrowIfNull` for guard clauses.
- Keep public APIs small and simple. Add overloads only when they remove real friction for callers.
- Avoid unnecessary abstractions; this library should stay easy to read and package.
- Avoid broad formatting-only churn in files unrelated to the requested change.

## Testing Guidance

- Add or update tests for any behavioral change in `MemoryCacheAside`, `ICacheAside`, options, or DI registration.
- Follow the existing test naming pattern: `Method_Condition_ExpectedResult`.
- Use Moq when verifying interactions with `IMemoryCache`; use a real `MemoryCache` for concurrency, expiration, or eviction behavior.
- Cover sync and async paths when changing shared `GetOrCreate` behavior.
- Include tests for invalid arguments, cache hits, partition removal, key removal, clear behavior, disposal, expiration, and concurrency when relevant.

## Commands

Run commands from `src/` unless noted otherwise:

```powershell
dotnet build --configuration Release
dotnet run --project ./Edi.CacheAside.InMemory.Tests/Edi.CacheAside.InMemory.Tests.csproj --configuration Release --no-build
dotnet pack --configuration Release -o nupkg
```

Use the test command above because the test project is configured as an executable xUnit v3 test project, matching the GitHub Actions workflow.

## Documentation

- Update `README.md` when changing public usage, options, DI registration, supported target framework, or cache behavior.
- Keep README examples aligned with the actual `ICacheAside` API, which accepts factory delegates and an optional `TimeSpan` expiration.