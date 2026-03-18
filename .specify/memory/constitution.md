<!--
SYNC IMPACT REPORT
==================
Version Change: N/A → 1.0.0 (New Constitution)
Status: Initial ratification - all placeholders replaced with concrete values
Modified Principles: None (initial creation)
Added Sections: All 5 Core Principles + Technology Stack + Development Workflow
Removed Sections: None
Templates Requiring Updates:
  ✅ .specify/templates/plan-template.md - Constitution Check section aligned
  ✅ .specify/templates/spec-template.md - Testing requirements aligned
  ✅ .specify/templates/tasks-template.md - Task categories aligned
  ⚠ .specify/templates/commands/ - Directory does not exist (no action needed)
  ✅ AGENTS.md - Serves as runtime guidance, referenced in governance
Deferred Items: None
-->

# MyMusic Constitution

## Core Principles

### I. Multi-Platform Architecture

Every feature must be designed to work across all supported platforms: Server (ASP.NET Core Web API), CLI (command-line interface), Web Client (React/TypeScript SPA), and Mobile Client (React Native).

**Non-negotiables:**
- New features must include API endpoints in MyMusic.Server that can be consumed by all clients
- Business logic belongs in MyMusic.Common (shared library), not duplicated across platforms
- CLI and Mobile clients must use the same API contracts as the Web Client
- Database migrations must consider all client synchronization scenarios

**Rationale**: Ensures consistent user experience and eliminates code duplication across platform boundaries.

### II. Test-First Development (NON-NEGOTIABLE)

All code changes must follow the Red-Green-Refactor cycle. Tests are written before implementation and must fail before the code is written.

**Non-negotiables:**
- xUnit with NSubstitute for mocking and Shouldly for assertions
- Test naming convention: `<MethodName>_<Scenario>_<ExpectedOutcome>`
- Use Scenario class for test setup (in-memory SQLite + MockFileSystem)
- Contract tests required for new API endpoints
- Integration tests for inter-service communication and shared schemas

**Rationale**: Guarantees code correctness, prevents regressions, and documents expected behavior through executable specifications.

### III. API-First Design with Contract Generation

All server-side functionality must expose OpenAPI contracts that auto-generate TypeScript clients.

**Non-negotiables:**
- Server must maintain accurate OpenAPI specification
- Orval generates TypeScript client code from OpenAPI - never manually edit files in `src/client/` or `src/model/`
- Custom mutations requiring query invalidation must be added to `mutationInvalidates` in `orval.config.cjs`
- DTOs follow strict organization: Request DTOs, Response DTOs, and Shared data DTOs
- Use AgileMapper for simple mappings, manual mapping for complex relationships

**Rationale**: Maintains type safety across the server-client boundary and eliminates manual synchronization of API contracts.

### IV. Code Quality Standards

All code must adhere to established patterns for readability, maintainability, and consistency.

**Non-negotiables:**
- .NET 9.0 with `ImplicitUsings` and `Nullable` enabled
- File-scoped namespaces: `namespace MyMusic.Common.Services;`
- Primary constructors for controller and service classes
- XML documentation comments (`/// <summary>`) for all public APIs
- PascalCase for classes/methods/properties, prefix interfaces with `I`
- Import order: System → Microsoft → Third-party → MyMusic

**Rationale**: Consistent code style reduces cognitive load, improves code review efficiency, and maintains professional standards.

### V. State Management and Reactivity Patterns

Client-side state management must follow established patterns to prevent performance issues.

**Non-negotiables:**
- Zustand for state management with `useShallow` wrapper for all selectors returning objects/arrays
- Player Context: Use `usePlayerActions()` for actions, `usePlayerContext(state => ...)` with selectors for reading
- Never call `usePlayerContext()` without a selector - causes excessive re-renders
- Debounce with `useDebouncedValue` from @mantine/hooks, never manual setTimeout
- Mantine styles imported centrally in `src/components/styles.ts`

**Rationale**: Prevents common React performance pitfalls (infinite re-renders, memory leaks) and ensures consistent UI behavior.

## Technology Stack Requirements

**Backend:**
- .NET 9.0 with EF Core and PostgreSQL
- Npgsql.EntityFrameworkCore.PostgreSQL for database access
- EFCore.NamingConventions for snake_case naming
- Refit for HTTP client generation
- TagLib# for audio metadata extraction
- System.IO.Abstractions for testable file I/O

**Frontend:**
- React with TypeScript
- Vite for build tooling
- Mantine UI component library
- TanStack Query for server state management
- Zustand for client state management
- Zod for runtime validation (Mobile)

**Testing:**
- xUnit for .NET testing
- NSubstitute for mocking
- Shouldly for assertions
- Scenario class for integration test setup

## Development Workflow

### Database Changes

1. Create entity class in `MyMusic.Common/Entities/`
2. Add to `MusicDbContext`
3. Run migration: `dotnet ef migrations add <MigrationName> --project MyMusic.Common`
4. Server applies migrations automatically on startup

### API Changes

1. Create DTOs in appropriate `MyMusic.Server/DTO/<Resource>/` folder
2. Add method to service interface/implementation
3. Add controller action with proper HTTP attribute
4. Restart server to update OpenAPI
5. Run `devbox run orval` to regenerate client
6. Add tests if applicable

### Code Review Gates

All pull requests must verify:
- Constitution compliance (no violations without documented justification)
- Test coverage for new functionality
- DTO patterns followed correctly
- No secrets or credentials in code
- Lint and typecheck commands pass

## Governance

**Supremacy**: This constitution supersedes all other practices and guidelines. When conflicts arise, constitution principles take precedence.

**Amendment Process**:
1. Proposed changes require documentation of rationale and impact
2. Must include migration plan for existing code if breaking
3. Requires approval from project maintainers
4. Version must be incremented per semantic versioning:
   - MAJOR: Backward incompatible governance/principle removals or redefinitions
   - MINOR: New principle/section added or materially expanded guidance
   - PATCH: Clarifications, wording, typo fixes, non-semantic refinements

**Compliance Verification**:
- All PRs/reviews must verify compliance with relevant principles
- Complexity must be justified with reference to specific principles
- Use AGENTS.md for runtime development guidance
- Quarterly review of constitution effectiveness

**Versioning Policy**:
- Version format: MAJOR.MINOR.PATCH
- Breaking changes to principles require MAJOR version bump
- Additions or expansions require MINOR version bump
- Clarifications and fixes require PATCH version bump

**Version**: 1.0.0 | **Ratified**: 2026-03-17 | **Last Amended**: 2026-03-17
