# Copilot Instructions: Team Coding Preferences

## General Principles
- Always follow coding conventions and formatting rules defined in the `.editorconfig` file.
- Prioritize Blazor components and patterns over Razor Pages or ASP.NET Core MVC.
- Use modern C# features (currently C# 13.0) and target the latest .NET (currently .NET 9).
- Organize code by Domain-Driven Design (DDD) and Clean Architecture: separate Domain, Application, Infrastructure, Persistenceand and Presentation layers.

### Rules:
- **All production code** lives under the `src` folder.
  - `Domain` → pure domain logic with no external dependencies.
  - `Application` → CQRS commands, queries, handlers, validators.
  - `Persistence` → EF Core context, configurations, migrations, repositories.
  - `Infrastructure` → external services, messaging, background processing.
  - `Presentation` → Blazor UI and any public API endpoints.
- **All test projects** live under the `tests` folder.
  - Names follow `[Project].UnitTests` or `[Project].IntegrationTests`.
  - Folder structure **mirrors the `src` structure**.
- The `.github` folder is only for GitHub config files like `copilot-instructions.md`.
- `Solution Items` contains shared solution-level files.

## Architecture Patterns
- Follow **CQRS**:  
  - **Commands** mutate state and **Queries** are strictly read-only.  
  - Handlers must remain **thin**, delegating logic to domain services or aggregates.
- Use a **Rich Domain Model**:  
  - Business rules and invariants live **inside domain entities**, not in services.
  - Entities expose **behavioral methods** rather than being data containers.
- **Domain Entity Rules**:
  - No public constructors → only **private constructors**.
  - Provide **public static factory methods** (e.g., `Order.Create(...)`) to control object creation.
  - Validate state inside the factory method to prevent invalid entities.
- **Value Objects**:
  - Immutable and validated at construction.
  - Must override `Equals` and `GetHashCode`.
- **Unit of Work Pattern**:
  - All database operations must be coordinated through a Unit of Work.
  - Repositories are scoped to the Unit of Work instance.
  - Ensure atomic operations by calling `CommitAsync()` or `SaveChangesAsync()` only through the Unit of Work.
- **Repository Pattern**:
  - The application layer communicates with persistence only through **repository interfaces**.
  - Repository interfaces are defined in the `Domain` layer.
  - Implementations are provided in the `Persistence` layer.
  - Repositories must be scoped to the active **Unit of Work** instance and never used standalone.
  - No direct `DbContext` calls are allowed outside repository implementations.

## Naming Conventions
- **PascalCase** for classes, methods, and properties.
- **camelCase** for local variables and parameters.
- Pluralize table names in EF Core (e.g., `"Users"`).
- Prefix private/internal EF configuration classes with `internal sealed`.

## Entity Framework Core
- Use **Fluent API** for all entity configurations.
- Always configure:
  - `.IsRequired()` for mandatory fields.
  - `.HasMaxLength()` for strings.
  - `.IsUnicode()` for strings when appropriate (e.g., email).
- Explicitly define relationships using `.HasOne()`, `.WithMany()`, `.HasForeignKey()`.
- Always set delete behaviors explicitly (e.g., `DeleteBehavior.Restrict`).
- Use `.HasConversion()` for value objects and enums.

## Blazor / Presentation Layer
- Use **partial classes** for `.razor.cs` code-behind files.
- Group Blazor components **by feature**, not by type.
- Prefer **dependency injection** for services and repositories.

## C# Coding Preferences (Personal)
- Always use **explicit types** instead of `var`,  
  except when the type is **obvious** (e.g., `var user = new User();` or `.ToList()` calls).  
- Prefer **primary constructors** over private fields.
- Always use **braces `{}` for all `if` conditions**, even single-line.
- Prefer **collection initialization** `[item1, item2, ...]` instead of `new [] { ... }`.
- Use **file-scoped namespaces** instead of block-scoped namespaces.
- Place **using directives outside** of namespaces.

## Formatting & Style
- Consistent indentation: **4 spaces**.
- Avoid regions unless necessary for very large files.
- Group related properties and methods logically.

## Testing & Validation
- Use validators for commands (`AddOrEditUserCommandValidator`).
- Always use **async/await** for asynchronous operations.
- Unit test domain logic directly at the **aggregate root or entity level**.

## Documentation
- Use XML comments for public APIs and base classes.
- Keep documentation concise and relevant.

---

**Note:**  
These conventions apply globally unless explicitly overridden for a specific feature or solution.
