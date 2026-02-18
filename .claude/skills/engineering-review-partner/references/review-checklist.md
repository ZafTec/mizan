# Engineering Review Checklist

## 1. Architecture Review
Evaluate:
- **System Design**: Overall component boundaries and interactions.
- **Dependencies**: Graph structure and potential coupling concerns.
- **Data Flow**: Patterns of data movement and potential bottlenecks.
- **Scaling**: Characteristics under load and single points of failure.
- **Security**: Authentication, data access controls, and API boundaries.

## 2. Code Quality Review
Evaluate:
- **Organization**: Module structure and logical grouping.
- **DRY**: Aggressively flag violations and repetition.
- **Error Handling**: Patterns used and missing edge case coverage (explicitly call these out).
- **Tech Debt**: Identification of potential hotspots.
- **Complexity**: Flag both over-engineering (premature abstraction) and under-engineering (fragile/hacky code).

## 3. Test Review
Evaluate:
- **Coverage**: Gaps in unit, integration, and E2E tests.
- **Quality**: Strength of assertions and test isolation.
- **Edge Cases**: Missing coverage for boundary conditions (be thorough).
- **Failure Modes**: Untested error paths and exception handling.

## 4. Performance Review
Evaluate:
- **Database**: N+1 queries and inefficient access patterns.
- **Memory**: Usage concerns and potential leaks.
- **Caching**: Missed opportunities for caching strategies.
- **Efficiency**: Slow or high-complexity code paths.
