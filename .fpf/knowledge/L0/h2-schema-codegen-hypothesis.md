---
id: h2-schema-codegen
type: hypothesis
created: 2025-12-19T00:35:00Z
problem: "Architecture review: validation, security, schema duplication, Redis, UI bugs"
status: L0
formality: 6
novelty: Novel
complexity: Medium
author: Claude (generated), Human (to review)
scope:
  applies_to: "Projects requiring type-safe schema sync between frontend and backend"
  not_valid_for: "Teams unwilling to add build-time code generation"
  scale: "Solo developer with willingness to invest upfront for long-term maintainability"
---

# Hypothesis: Schema-Driven Code Generation (Innovative)

## 1. The Method (Design-Time)

### Proposed Approach
Establish a **single source of truth** for the database schema using a neutral format (TypeScript schema file or JSON schema), then generate both Drizzle and EF Core models from it. Use TypeScript as the schema language since it's already used for Drizzle, and generate C# EF Core entities via a custom generator.

### Rationale
- Eliminates manual schema synchronization (biggest maintenance risk)
- TypeScript schema can generate both TypeScript types (for Drizzle) and C# classes (for EF Core)
- Validation schemas (Zod + FluentValidation) can also be generated from the same source
- One-time upfront investment in generator pays off over time
- TypeScript → C# code generation is well-understood problem (many tools exist)

### Implementation Steps

1. **Create Schema DSL** (Day 1-2):
   - Define canonical schema in `shared/schema.ts` using a builder pattern
   - Example:
     ```typescript
     const Recipe = schema('Recipe', {
       id: field.uuid().primaryKey(),
       title: field.string().maxLength(200).required(),
       servings: field.int().min(1).required(),
       // ... etc
     })
     ```
   - This DSL captures both DB structure AND validation rules

2. **Build Code Generators** (Day 3-5):
   - **Generator 1:** TypeScript schema → Drizzle schema (`drizzle-orm/pg-core`)
   - **Generator 2:** TypeScript schema → EF Core entities (C# classes with fluent API)
   - **Generator 3:** TypeScript schema → Zod schemas (client-side validation)
   - **Generator 4:** TypeScript schema → FluentValidation (backend validation)
   - Implement as Node scripts in `scripts/codegen/`

3. **Integrate into Build Pipeline** (Day 6):
   - Add pre-build hook: `npm run codegen` (runs all generators)
   - Backend build depends on generated C# files
   - Frontend build uses generated Drizzle/Zod schemas
   - Git-ignore generated files (regenerate on every build) OR commit them (review changes)

4. **Fix Immediate Bugs** (Day 7):
   - Same fixes as H1 (proxy routes, dropdown overflow)
   - But now schemas are guaranteed to match

5. **Redis + Security** (Day 8-9):
   - Same Redis queue implementation as H1
   - Security hardening (rate limits, CSRF, cookie flags)

6. **Migration Path** (Day 10):
   - Convert existing schemas to schema DSL format
   - Generate code and verify output matches current schemas
   - Run tests to ensure no breaking changes
   - Cut over to generated schemas

### Expected Capability
- **Zero schema drift**: Frontend and backend schemas always match
- **DRY validation**: Zod and FluentValidation generated from same rules
- **Type safety**: DTOs match database entities exactly (PascalCase → camelCase handled in generator)
- **Maintainability**: Schema changes require editing one file, regenerate everything
- **Auditability**: Generated code can be reviewed in PRs

## 2. The Validation (Run-Time)

### Plausibility Assessment

| Filter | Score | Justification |
|--------|-------|---------------|
| **Simplicity** | Low | Adds build-time complexity, requires maintaining generators |
| **Explanatory Power** | High | Eliminates root cause of schema drift entirely |
| **Consistency** | High | Aligns with code-first database design principles |
| **Falsifiability** | High | Easy to compare generated code to hand-written schemas |

**Plausibility Verdict:** PLAUSIBLE (higher upfront cost, better long-term ROI)

### Assumptions to Verify
- [ ] TypeScript can express all EF Core fluent API configurations
  - **Performer:** AI Agent (research EF Core capabilities)
- [ ] C# code generator can handle complex relationships (many-to-many, cascades)
  - **Performer:** Developer (prototype generator)
- [ ] Generated code is readable and maintainable (not obfuscated)
  - **Performer:** Code review
- [ ] Build pipeline doesn't slow down significantly with codegen step
  - **Performer:** Benchmark (measure build time before/after)

### Required Evidence
- [ ] **Internal Test:** Prototype generator can convert 3 sample entities
  - **Performer:** Developer
- [ ] **Internal Test:** Generated schemas pass all existing tests
  - **Performer:** CI pipeline
- [ ] **Research:** Existing tools like Prisma, TypeORM schema generators
  - **Performer:** AI Agent
- [ ] **Research:** C# code generation best practices (Roslyn vs T4 templates)
  - **Performer:** AI Agent

## Falsification Criteria
- If generator cannot express EF Core edge cases (inheritance, complex indexes) → schema DSL insufficient
- If generated code requires manual tweaking → generator not robust enough
- If build time increases by >30 seconds → codegen too slow
- If schema DSL becomes harder to read than direct Drizzle/EF code → abstraction wrong

## Estimated Effort
**10 days** (solo developer, full-time)
- Schema DSL design: 2 days
- Generator implementation: 3 days
- Build integration: 1 day
- Bug fixes: 1 day
- Redis + security: 2 days
- Migration + testing: 1 day

## Weakest Link
**Generator complexity** - Building a robust generator that handles all EF Core and Drizzle features is non-trivial. May require ongoing maintenance as ORMs evolve. Risk of the generator becoming a maintenance burden itself.

**Mitigation**: Start with subset of schema features, expand generator incrementally. Use existing tools (Prisma schema parser, Roslyn) instead of building from scratch.
