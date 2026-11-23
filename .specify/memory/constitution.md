<!--
Sync Impact Report
- Version change: unversioned scaffold -> 1.0.0
- Modified principles: none; this replaces placeholder principles
- Added sections: Security and Authorization; Offline-First and Abstraction;
  Layered Architecture; Testability and Validation; Training-Context Simplicity;
  Technical Constraints; Development Workflow
- Removed sections: placeholder principles and placeholder sections
- Follow-up TODOs: RATIFICATION_DATE is unknown and marked TODO below
-->

# ContosoDashboard Constitution

## Core Principles

### I. Security and Authorization

Every protected feature MUST enforce authentication and authorization at both the page and
service boundaries. All data access MUST verify the acting user's role and ownership or
membership; features MUST NOT rely only on hidden UI elements or route attributes.
The application MAY use mock authentication only for the documented training environment.
Production-oriented work MUST require a real identity provider and MUST NOT reuse mock
credentials or passwordless login as an acceptable production control.

### II. Offline-First and Infrastructure Abstraction

The application MUST remain runnable without external subscriptions or network services.
Infrastructure dependencies, including persistence and file storage, MUST be behind interfaces
or equivalent abstractions. Training implementations MAY use LocalDB, in-memory data, local
filesystem storage, and mock authentication, while business logic MUST remain independent of
the selected infrastructure implementation.

### III. Layered Architecture and Separation of Concerns

Models MUST represent domain data, data contexts MUST own EF Core mappings and persistence
configuration, services MUST implement reusable application behavior, and Razor components or
pages MUST coordinate presentation and user interaction. UI code MUST NOT contain repository
queries or bypass service-level authorization. Dependencies MUST point inward toward stable
domain and application behavior rather than toward UI or infrastructure details.

### IV. Testability and Validation

Behavioral changes MUST include appropriate automated coverage or a documented reason why
coverage is impractical. Tests MUST cover authorization boundaries, service behavior, validation,
and user-visible state transitions when those behaviors change. Feature inputs MUST be validated
before use, and failures MUST produce clear, actionable feedback without exposing sensitive
details or leaving partial data or files behind.

### V. Training-Context Simplicity

Solutions MUST favor the smallest implementation that satisfies the accepted requirement.
Complex abstractions, external services, and speculative extensibility MUST be justified against
the training objective. Documentation MUST distinguish mock or simplified behavior from
production behavior and MUST identify known limitations instead of presenting training code as
production guidance.

## Technical Constraints

- The implementation SHOULD target the framework version declared in the project file and
  preserve the existing ASP.NET Core Blazor Server, EF Core, SQL Server LocalDB, and
  Bootstrap-based architecture unless a specification explicitly changes that stack.
- Sensitive paths outside the web root MUST use authorization-aware endpoints; user-supplied
  names MUST NOT control storage paths. File uploads MUST validate type and size and use
  non-user-controlled identifiers.
- Configuration MUST remain local-development friendly. Secrets MUST NOT be committed to
  source control, and environment-specific values MUST use configuration rather than
  application hard-coding.
- Database and storage operations MUST preserve referential integrity and use deterministic
  migration or seeding behavior. Application changes MUST avoid orphaned records or files.
- Security-sensitive changes MUST include a threat or misuse review covering authentication,
  authorization, input handling, direct object references, and accidental data exposure.

## Development Workflow

- Feature work MUST begin with a written specification that defines user value, scope,
  acceptance scenarios, constraints, and measurable success criteria.
- Implementation MUST follow an approved plan and dependency-ordered tasks. Deviations from
  the plan MUST update the relevant Spec Kit artifact before or alongside the implementation.
- Changes MUST pass the narrowest relevant build or test command, then any broader validation
  needed for affected layers. Existing warnings or failures MUST be reported rather than
  silently suppressed.
- Reviewers MUST verify constitution compliance, readability, security boundaries, and
  preservation of existing behavior. Unavoidable complexity MUST be documented and limited.
- Documentation and runtime behavior MUST remain synchronized. Public interfaces, setup steps,
  authorization behavior, and known limitations MUST be updated with the code that changes them.

## Governance

This constitution supersedes ad hoc implementation practices for the ContosoDashboard
repository. All specification, plan, task, implementation, and review work MUST demonstrate
compliance or record an explicit, narrowly scoped exception.

Amendments require an updated constitution file, a rationale, and an incremented semantic
version. A MAJOR increment is required for incompatible governance or principle removals or
redefinitions. A MINOR increment is required for a new principle or section or for materially
expanded guidance. A PATCH increment is required for clarifications, wording corrections, or
non-semantic refinements. Each amendment MUST include a Sync Impact Report identifying changed
principles and sections and MUST update the ratification or last-amended date as applicable.

Compliance reviews MUST occur at specification, implementation planning, task completion, and
pull-request review. Complexity or exceptions MUST identify the requirement that necessitates
the additional behavior and the safer alternatives that were rejected.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): confirm original adoption date | **Last Amended**: 2026-11-23
