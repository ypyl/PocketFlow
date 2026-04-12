# PocketFlow Agent Guide

## Project Structure

```
pocketflow/           # Python source (100 lines - source of truth)
pocketflow.net/       # .NET port (Async-only)
pocketflow.net.tests/ # .NET tests (xUnit)
cookbook/             # Python examples (40+ apps)
cookbook.net/         # .NET migrated examples (file-based apps)
```

## Build & Test

**Python:** (no explicit test command documented - check individual cookbook dirs)

**.NET:**
```powershell
cd pocketflow.net.tests && dotnet test
```

## Python → .NET Key Differences

| Aspect | Python | .NET |
|--------|--------|------|
| Sync/Async | Both exist | **Async-only** |
| Transitions | `>>` operator, `- "action"` | `.On("action").To(node)` |
| Null return | `None` = default | `null` = default |
| Node cloning | `copy.copy()` | `ShallowClone()` |

## Core Types (pocketflow.net/PocketFlow.cs)

- **`Node<TShared, TPrep, TExec>`** - 3-phase: `Prep` → `Exec` → `Post`
- **`Flow<TShared>`** - orchestrates nodes via action transitions
- **`BatchNode<TShared, TItem, TExec>`** - processes items with retry
- **`BatchFlow<TShared>`** - iterates param sets, runs sub-flow per item

## Critical Behaviors

- `Post()` returning `null` → uses `"default"` transition
- Nodes are **cloned** between orchestration iterations (resets state)
- `ExecFallback` returns `TExecReturn` (not void) - default re-throws
- `BatchFlow.OrchestrateOnce` calls `base.DoOrchestrate`
- `Flow` implements `IOrchestrated<TShared>` → flows can nest

## .NET Cookbook Examples (cookbook.net/)

- File-based apps using `#:project`, `#:package`, `#:property` directives
- Output goes to `./cookbook.net/artifacts`
- Reference: `pocketflow.net.csproj`

## Reference Sources

- `pocketflow/__init__.py` - Python source (100 lines, source of truth)
- `pocketflow.net/PocketFlow.cs` - .NET implementation
- `pocketflow.net.tests/*.cs` - xUnit tests with naming `Name_likes_sentence`
