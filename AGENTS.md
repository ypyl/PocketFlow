# PocketFlow .NET Agent Guide

## Project Overview

`.NET` port of [PocketFlow](https://github.com/The-Pocket/PocketFlow) - a minimalist LLM framework.
- Python source: `pocketflow/__init__.py` (100 lines)
- .NET implementation: `pocketflow.net/PocketFlow.cs`

## Build & Test

```powershell
cd pocketflow.net.tests
dotnet test
```

## .NET Conventions (differ from Python)

| Aspect | Python | .NET |
|--------|--------|------|
| Sync/Async | Both exist | **Async-only** |
| Transitions | `>>` operator, `- "action"` | `.On("action").To(node)` |
| Interface | Internal | `IOrchestrated<TShared>` is `internal` |
| Null return | `None` = default | `null` = default |
| Test naming | `test_underscore_name` | `Name_likes_sentence` |

## Core Types

- **`Node<TShared, TPrepReturn, TExecReturn>`** - 3-phase node (Prep → Exec → Post)
- **`Flow<TShared>`** - orchestrates nodes via action-based transitions
- **`BaseNode`** - base class with `Successors` dict and transition builder

## Important Behaviors

- `Post()` returning `null` → use "default" transition
- `Flow` implements `IOrchestrated<TShared>` so flows can nest inside other flows
- Nodes are **cloned** between orchestration iterations to reset state
- `ExecFallback` called when all retries exhausted (default re-throws)

## Missing Features (Opportunities)

- No `BatchNode` / `BatchFlow` (map-reduce pattern)
- `ExecFallback` needs dedicated tests
- No parallel execution variants

## Reference Sources

- `pocketflow/__init__.py` - Python implementation (source of truth)
- `docs/core_abstraction/*.md` - design documentation
- `tests/test_*.py` - Python tests (reference for .NET test scenarios)
