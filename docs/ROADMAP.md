# EasyAppDev.Blazor.Store Roadmap

> The path from solid foundation to killer library

## Current State Assessment

**Version:** 3.0.0
**Status:** Production-ready, actively maintained
**Rating:** 9/10 - Feature-complete, production-ready with killer features

### What We Have
- Zustand-inspired simplicity for Blazor
- Immutable state via C# records
- Thread-safe async-first updates
- Middleware pipeline (DevTools, Persistence, Logging)
- Selector-based granular subscriptions
- Async action state machine (AsyncData<T>)
- Debounce/Throttle/LazyCache utilities

### What's Been Added (Phase 4 & 5)
- Optimistic updates with rollback
- Undo/redo with history
- Type-safe actions
- Source generators
- TanStack Query-style data fetching
- Immer-style mutable syntax
- Enhanced DevTools with time-travel
- Plugin ecosystem
- Server-side state sync

---

## Vision

**Goal:** Become the go-to state management library for Blazor - the one developers *want* to use, not the one they're forced to use.

**Philosophy:** Simple, Type-Safe, Pleasant

**Differentiators:**
1. Simplicity of Zustand
2. Type-safety of C#
3. Developer experience that doesn't make you hate your life

---

## Phase Overview

| Phase | Version | Focus | Status |
|-------|---------|-------|--------|
| [Phase 1](phases/PHASE_1_BUG_FIXES.md) | 1.1.x | Bug Fixes & Polish | 🟢 Complete |
| [Phase 2](phases/PHASE_2_CLEANUP.md) | 1.2.x | Cleanup & Simplification | 🟢 Complete |
| [Phase 3](phases/PHASE_3_CORE_ENHANCEMENTS.md) | 2.0.0 | Core Enhancements | 🟢 Complete |
| [Phase 4](phases/PHASE_4_ADVANCED_FEATURES.md) | 2.x | Advanced Features | 🟢 Complete |
| [Phase 5](phases/PHASE_5_KILLER_FEATURES.md) | 3.0.0 | Killer Features | 🟢 Complete |

---

## Quick Links

### Planning Documents
- [Phase 1: Bug Fixes & Polish](phases/PHASE_1_BUG_FIXES.md)
- [Phase 2: Cleanup & Simplification](phases/PHASE_2_CLEANUP.md)
- [Phase 3: Core Enhancements](phases/PHASE_3_CORE_ENHANCEMENTS.md)
- [Phase 4: Advanced Features](phases/PHASE_4_ADVANCED_FEATURES.md)
- [Phase 5: Killer Features](phases/PHASE_5_KILLER_FEATURES.md)

### Reference Documents
- [Architecture](ARCHITECTURE.md)
- [Design Principles](DESIGN_PRINCIPLES.md)
- [Coding Standards](CODING_STANDARDS.md)
- [API Design Guidelines](API_DESIGN_GUIDELINES.md)
- [Testing Strategy](TESTING_STRATEGY.md)

---

## Phase 1: Bug Fixes & Polish (v1.1.x)

**Goal:** Fix known issues without breaking changes

### Key Deliverables
- [x] Convert `AsyncData<T>` from class to record
- [x] Add thread-safety to `MemoizedSelector<T>`
- [x] Replace swallowed exceptions with proper logging
- [x] Replace `Console.WriteLine` with `ILogger`
- [x] Add XML documentation to all public APIs

**Breaking Changes:** None
**Risk Level:** Low

[Full Details →](phases/PHASE_1_BUG_FIXES.md)

---

## Phase 2: Cleanup & Simplification (v1.2.x)

**Goal:** Reduce complexity, improve maintainability

### Key Deliverables
- [x] Remove deprecated `Update()` method from Store, IStateWriter, and StoreComponent
- [x] Extract diagnostics to separate package (deferred - using #if DEBUG conditional compilation)
- [x] Consolidate DevTools overloads to single IServiceProvider method
- [x] Slim down `StoreComponent<T>` - created `StoreComponentWithUtilities<T>`
- [x] Add `UpdateWithAsync` extension method for simplified async patterns

**Breaking Changes:** Minor (deprecated APIs removed)
**Risk Level:** Low-Medium

[Full Details →](phases/PHASE_2_CLEANUP.md)

---

## Phase 3: Core Enhancements (v2.0.0)

**Goal:** Add foundational features that enable advanced patterns

### Key Deliverables
- [x] ISelector subscription on IStore - Subscribe using memoized selectors
- [x] Functional middleware syntax - Use/UseWhen/UseForAction inline middleware
- [x] MiddlewareContext - Rich context for middleware with phase info and services
- [x] Improved PersistenceOptions API - Callbacks, transforms, and fine-grained control
- [x] Structured error boundaries - StoreError record with ErrorLocation enum
- [x] OnError handler in StoreBuilder - Centralized error management

**Breaking Changes:** Yes (major version bump)
**Risk Level:** Medium

[Full Details →](phases/PHASE_3_CORE_ENHANCEMENTS.md)

---

## Phase 4: Advanced Features (v2.1.0)

**Goal:** Add powerful features for complex applications

### Key Deliverables
- [x] Optimistic updates with rollback - `UpdateOptimistic`, `UpdateOptimisticWithConfirm`
- [x] Built-in undo/redo - `IStoreHistory<T>` with `UndoAsync`/`RedoAsync`
- [x] Type-safe actions/events - `IAction`, `IActionDispatcher<T>`, reducer pattern
- [x] Cross-tab state sync - `WithTabSync` middleware using BroadcastChannel API
- [x] Source generators for boilerplate - `[Store]` attribute generates setters/actions

**Breaking Changes:** Additive only
**Risk Level:** Medium

[Full Details →](phases/PHASE_4_ADVANCED_FEATURES.md)

---

## Phase 5: Killer Features (v3.0.0)

**Goal:** Become the undisputed best Blazor state library

### Key Deliverables
- [x] TanStack Query-style data fetching - UseQuery, UseMutation, QueryClient
- [x] Immer-style mutable syntax - Produce() with Draft pattern
- [x] Full DevTools time-travel debugging - Enhanced middleware with action replay
- [x] Plugin ecosystem - IStorePlugin, PluginHost, built-in plugins
- [x] Server-side state sync - SignalR-based real-time sync with presence
- [ ] Visual Studio / Rider tooling (Future enhancement)

**Breaking Changes:** None (additive)
**Risk Level:** Medium (well-tested)

[Full Details →](phases/PHASE_5_KILLER_FEATURES.md)

---

## Success Metrics

### Adoption
- GitHub stars growth
- NuGet download trends
- Community contributions
- Stack Overflow questions

### Quality
- Test coverage > 90%
- Zero critical bugs in production
- Documentation completeness
- API stability

### Developer Experience
- Time to first working app < 5 minutes
- Learning curve feedback
- Migration ease from other libraries

---

## Principles Guiding Development

1. **Simplicity over features** - Don't add complexity unless it solves real problems
2. **Immutability is non-negotiable** - Every API must preserve immutability
3. **Type-safety first** - Leverage C# compiler, no magic strings
4. **Async by default** - Synchronous is the exception
5. **Fail gracefully** - Never crash the app for optional features
6. **Test everything** - If it's not tested, it's broken

---

## Contributing

See each phase document for specific contribution opportunities. Priority areas:
- Bug fixes (Phase 1)
- Documentation improvements
- Test coverage expansion
- Sample applications

---

## Changelog

| Date | Update |
|------|--------|
| 2025-12-01 | Phase 5 completed - v3.0.0 released (Query system, Immer syntax, Enhanced DevTools, Plugins, Server sync) |
| 2025-12-01 | Phase 4 completed - v2.1.0 released (Optimistic updates, Undo/Redo, Actions, TabSync, Source Generators) |
| 2025-12-01 | Phase 3 completed - v2.0.0 released |
| 2025-12-01 | Phase 2 completed - v1.2.0 released |
| 2025-12-01 | Phase 1 completed - v1.1.0 released |
| 2024-12-01 | Initial roadmap created |

---

*This roadmap is a living document. It will evolve based on community feedback, real-world usage patterns, and the Blazor ecosystem evolution.*
