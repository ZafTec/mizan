# DTO Contracts

## Source of truth

- Backend request and response DTOs are the canonical API contract.
- Swagger/OpenAPI is generated from backend DTOs and controllers.
- Frontend API types come from `frontend/types/api.generated.ts`.
- Controller actions should return `ActionResult<T>` when they expose a typed JSON response. Returning plain `IActionResult` hides the schema from OpenAPI and forces frontend fallbacks.

## Rules

- Do not hand-write API response interfaces in pages or components when the contract already exists in `frontend/types/api.generated.ts`.
- If the UI needs a different shape, map from generated DTOs into a local view model in one place.
- After any backend contract change, regenerate frontend types with `bun run codegen` from `frontend/`.
- If MCP reuses an existing backend contract, prefer the generated DTO alias over a duplicate handwritten type.

## Current wrapper files

- `frontend/types/api-contracts.ts` exposes stable aliases for commonly used generated DTOs.
- `frontend/types/mcp.ts` should stay thin and only define request shapes that are not generated yet.

## Workflow

1. Change backend DTO/controller contract.
2. Run backend locally so Swagger is available.
3. Run `bun run codegen` in `frontend/`.
4. Update frontend code to use regenerated types.
5. Run build and targeted tests.

## Anti-patterns

- Duplicating backend DTOs inside React components.
- Returning one shape from the backend and assuming a different shape in the frontend.
- Editing `frontend/types/api.generated.ts` manually unless you are unblocking a local emergency and will immediately regenerate it afterward.
