# Copilot Instructions: Team Coding Preferences

## General Principles
- Always follow coding conventions and formatting rules defined in the `.editorconfig` file.
- Prioritize Blazor components and patterns over Razor Pages or ASP.NET Core MVC.
- Use modern C# features (currently C# 13.0) and target the latest .NET (currently .NET 9).
- Organize code by Domain-Driven Design (DDD) and Clean Architecture: separate Domain, Application, Infrastructure, Persistence, and Presentation layers.

## Project Configuration Management

- **Central Package Management:** Use `Directory.Packages.props` at the repository root to centrally manage NuGet package versions for all projects. Avoid specifying package versions in individual `.csproj` files—always update dependencies in the central file.
- **Centralized MSBuild Configuration:** Use `Directory.Build.props` and/or `Directory.Build.targets` at the solution or repository root to define and share common MSBuild properties, tooling, and targets across all projects. Avoid duplicating build settings in individual project files.

## Coding Style Rules

- **Braces:** Always use brackets `{}` for if conditions and all control flow statements (if, else, for, foreach, while, etc.), even for single-line statements.
  ```csharp
  // Correct
  if (condition)
  {
      DoSomething();
  }

  // Incorrect
  if (condition)
      DoSomething();
  ```

- **Type Declarations:** Prefer explicit types (`int`, `string`, etc.) over `var`, unless the type is obvious (such as a `.ToList()` or `new()` instantiation) or required for anonymous types.
  ```csharp
  // Correct
  int count = 5;
  var list = items.ToList();
  var anon = new { Name = "Test", Value = 1 };

  // Incorrect
  var count = 5;
  ```

- **File-scoped namespaces:** Use file-scoped namespaces in all classes.
  ```csharp
  namespace Dualis.Domain;

  public class Example { }
  ```

- **Collection Initialization:** Prefer collection initialization using `[item1, item2, ...]` instead of `new[]`.
  ```csharp
  // Preferred
  var items = [item1, item2];

  // Not preferred
  var items = new[] { item1, item2 };
  ```

- **Primary Constructors:** Prefer primary constructors over private fields when possible.

- **Remove unused usings:** Always remove unused `using` directives from the top of files.

- **Expression-bodied members:** Use expression body syntax for single-line methods and properties.
  ```csharp
  // Preferred
  public int GetValue() => 42;

  // Not preferred
  public int GetValue()
  {
      return 42;
  }
  ```

- **Indentation:** Use 4 spaces for indentation. Do not use tabs.

- **Visibility:** Omit redundant access modifiers (e.g., `private` is default for class members).

- **Trailing Commas:** In multi-line initializers and parameter lists, include trailing commas.

- **Attribute Loop Simplification:**  
  When searching for specific attributes, prefer LINQ (`Select`, `Any`) over manual `foreach` loops for clarity and conciseness.
  ```csharp
  // Not preferred
  foreach (AttributeData attr in comp.Assembly.GetAttributes())
  {
      string? name = attr.AttributeClass?.Name;
      if (name == "EnableDualisGenerationAttribute" || name == "EnableDualisGeneration")
      {
          return true;
      }
  }
  return false;

  // Preferred
  return comp.Assembly.GetAttributes()
      .Select(attr => attr.AttributeClass)
      .Any(cls => cls?.Name == "EnableDualisGenerationAttribute" || 
           cls?.Name == "EnableDualisGeneration");
  ```

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