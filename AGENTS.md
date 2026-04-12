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
- **`BatchNode<TShared, TItem, TExecReturn>`** - inherits from `BaseNode`, processes items in batch with retry support
- **`BatchFlow<TShared>`** - iterates over param sets, runs sub-flow for each
- **`BatchExtensions.GetBatchParams(shared)`** - access batch params from child nodes

## BatchNode

- `Prep` returns collection of items to process
- `ExecItem` processes each item (retry-enabled per item)
- `ExecFallback` handles item-level failures after retries exhausted
- Constructor: `BatchNode(defaultParams, maxRetries, wait, enableParallel)`

## BatchFlow

- `Prep` returns collection of param dicts
- Each param dict triggers one sub-flow execution
- Child nodes access params via `BatchExtensions.GetBatchParams(shared)`
- Constructor: `BatchFlow(startNode, defaultParams, enableParallel)`

## Important Behaviors

- `Post()` returning `null` → use "default" transition
- `Flow` implements `IOrchestrated<TShared>` so flows can nest inside other flows
- Nodes are **cloned** between orchestration iterations to reset state
- `ExecFallback` called when all retries exhausted (default re-throws)
- `BatchFlow.OrchestrateOnce` calls `base.DoOrchestrate` to run sub-flow

## Missing Features (Opportunities)

- `ExecFallback` needs dedicated tests
- No parallel batch execution tests
- No nested BatchFlow tests (e.g., BatchFlow inside BatchFlow)

## Reference Sources

- `pocketflow/__init__.py` - Python implementation (source of truth)
- `docs/core_abstraction/*.md` - design documentation
- `tests/test_*.py` - Python tests (reference for .NET test scenarios)
