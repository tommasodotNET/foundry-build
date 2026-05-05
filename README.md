# Foundry Build Samples

This repository contains two versions of the same trip readiness hosted-agent sample:

- `python-sample/` uses a TypeScript Aspire AppHost and a Python hosted agent.
- `dotnet-sample/` uses a C# Aspire AppHost and a .NET hosted agent.

Both samples use the same Vite/React frontend and expose the hosted agent through the Foundry Responses-compatible `/responses` endpoint.

## Python Sample

```bash
cd python-sample
npm install
npm run aspire:build
aspire run
```

The Python hosted agent lives in `python-sample/trip-agent-python/` and the import package is `trip_agent_python`.

## .NET Sample

```bash
cd dotnet-sample
dotnet build apphost.cs
npm --prefix frontend install
aspire run
```

The .NET hosted agent lives in `dotnet-sample/trip-agent-dotnet/` and is published from `dotnet-sample/apphost.cs` with `PublishAsHostedAgent`.