# HttpJsonRpc agent instructions

## Code conventions

Read and follow [CODE_CONVENTIONS.md](CODE_CONVENTIONS.md) before adding or changing
code. Record newly agreed conventions there.

## Scope and collaboration

- Inspect the existing implementation and nearby patterns before proposing changes.
- For requests to discuss, review, or propose a design, do not implement until approved.
- Preserve existing working-tree changes and keep edits scoped to the requested work.
- Keep library APIs, examples, documentation, tests, and commit messages generic and
  independent of consuming applications.
- Do not commit or push without explicit authorization.

## Verification

Build the solution and run the tests appropriate to the change. The library targets
.NET Standard 2.0; tests cover .NET Framework 4.8 and .NET 10.

```powershell
dotnet build src/HttpJsonRpc.sln -c Release -p:GeneratePackageOnBuild=false
dotnet test src/HttpJsonRpc.Tests/HttpJsonRpc.Tests.csproj -c Release --no-build
```

Require successful exit codes and report any relevant warnings or testing limits.
