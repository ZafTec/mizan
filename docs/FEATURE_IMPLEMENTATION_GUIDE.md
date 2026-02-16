# Feature Implementation Guide

This document provides comprehensive implementation plans for 5 major features requested for MacroChef.

## Table of Contents
1. [User Goal History & Pre-fill](#1-user-goal-history--pre-fill)
2. [Complete Meal Plan + Shopping Integration](#2-complete-meal-plan--shopping-integration)
3. [Food Diary Chart Date Highlighting](#3-food-diary-chart-date-highlighting)
4. [Comprehensive Backend Test Coverage](#4-comprehensive-backend-test-coverage)
5. [PWA Implementation Guide](#5-pwa-implementation-guide)

---

## 1. User Goal History & Pre-fill

### Problem
Currently, creating a new goal deletes old goals. Goals should be preserved as history.

### Solution Overview
- Modify backend to deactivate old goals instead of deleting
- Store goal history with timestamps
- Pre-fill form with latest active goal
- Allow viewing goal history

### Implementation Checklist

#### Backend
- [ ] Modify `CreateUserGoalCommandHandler` to set `IsActive = false` instead of deleting
- [ ] Create `GetUserGoalHistoryQuery` for retrieving all goals
- [ ] Add `GET /api/Goals/history` endpoint in `GoalsController`
- [ ] Run backend tests to verify behavior

#### Frontend
- [ ] Convert `/goal/page.tsx` to server component with data fetching
- [ ] Create `GoalFormClient.tsx` component with form logic
- [ ] Pre-fill form with `initialGoal` if exists
- [ ] Create `/goal/history/page.tsx` for viewing past goals
- [ ] Run `bun run codegen` to update TypeScript types

#### Testing
- [ ] Backend: Test goal history is preserved
- [ ] Backend: Test only one goal is active
- [ ] Frontend E2E: Create goal → verify pre-fill on revisit
- [ ] Frontend E2E: Create multiple goals → verify history

### Files to Modify
- Backend: `backend/Mizan.Application/Commands/CreateUserGoalCommand.cs`
- Backend: `backend/Mizan.Application/Queries/GetUserGoalHistoryQuery.cs` (NEW)
- Backend: `backend/Mizan.Api/Controllers/GoalsController.cs`
- Frontend: `frontend/app/(dashboard)/goal/page.tsx`
- Frontend: `frontend/app/(dashboard)/goal/history/page.tsx` (NEW)

---

## 2. Complete Meal Plan + Shopping Integration

### Problem
Meal planning exists but lacks calendar view and shopping list integration.

### Solution Overview
- Add calendar-based meal plan editor
- Generate shopping lists from meal plans
- Link shopping lists to meal plans via foreign key

### Implementation Checklist

#### Backend - Database
- [ ] Add migration: `AddMealPlanIdToShoppingList`
- [ ] Update `ShoppingList` entity with `MealPlanId` property
- [ ] Configure foreign key in `MizanDbContext`
- [ ] Run migration: `dotnet ef database update`

#### Backend - Business Logic
- [ ] Create `GenerateShoppingListFromMealPlanCommand`
- [ ] Implement ingredient aggregation logic
- [ ] Add endpoint: `POST /api/MealPlans/{id}/generate-shopping-list`
- [ ] Enhance `GetMealPlanByIdQuery` to include recipes + linked shopping lists

#### Frontend
- [ ] Create `MealPlanCalendar.tsx` component (calendar grid)
- [ ] Create meal plan detail page: `/meal-plan/[id]/page.tsx`
- [ ] Implement recipe selection modal
- [ ] Add "Generate Shopping List" button with date range picker
- [ ] Enhance shopping list detail to show meal plan link
- [ ] Run `bun run codegen` to update types

#### Testing
- [ ] Backend: Test shopping list generation includes all ingredients
- [ ] Backend: Test ingredient aggregation (same ingredient from multiple recipes)
- [ ] Backend: Test authorization (can't access other user's meal plans)
- [ ] Frontend E2E: Create meal plan → add recipes → generate shopping list

### Files to Create/Modify
- Backend Migration: `backend/Mizan.Infrastructure/Migrations/{timestamp}_AddMealPlanIdToShoppingList.cs`
- Backend Entity: `backend/Mizan.Domain/Entities/ShoppingList.cs`
- Backend Command: `backend/Mizan.Application/Commands/GenerateShoppingListFromMealPlanCommand.cs` (NEW)
- Backend Controller: `backend/Mizan.Api/Controllers/MealPlansController.cs`
- Frontend Component: `frontend/components/MealPlanCalendar.tsx` (NEW)
- Frontend Page: `frontend/app/(dashboard)/meal-plan/[id]/page.tsx` (NEW)

---

## 3. Food Diary Chart Date Highlighting

### Problem
Charts don't show which date is currently selected.

### Solution Overview
- Add date selection state
- Use Recharts `ReferenceLine` component to highlight selected date
- Synchronize selection across all charts

### Implementation Checklist

- [ ] Add `selectedDate` state to `/goal/dashboard/page.tsx`
- [ ] Add `onClick` handler to LineChart (calorie trend)
- [ ] Add `ReferenceLine` component to highlight selected date
- [ ] Add `onClick` handler to BarChart (macro breakdown)
- [ ] Highlight selected entry in "Recent Entries" section
- [ ] Add date picker UI above charts
- [ ] Add "Clear Selection" button
- [ ] Test on mobile (increase touch targets)

### Files to Modify
- Frontend: `frontend/app/(dashboard)/goal/dashboard/page.tsx`

### Code Example
```tsx
// Add to chart component
{selectedDate && (
  <ReferenceLine
    x={selectedDate}
    stroke="#3b82f6"
    strokeWidth={2}
    strokeDasharray="3 3"
    label={{ value: "Selected", position: "top" }}
  />
)}
```

---

## 4. Comprehensive Backend Test Coverage

### Problem
Many controllers lack integration tests (MealPlans, Workouts, Trainers, etc.).

### Solution Overview
- Create integration test files for all controllers
- Follow existing pattern (xUnit + FluentAssertions + Testcontainers)
- Test CRUD operations, authorization, pagination, cascades

### Implementation Checklist

#### Test Files to Create
- [ ] `MealPlansControllerTests.cs` (8 tests minimum)
- [ ] `WorkoutsControllerTests.cs` (6 tests minimum)
- [ ] `ExercisesControllerTests.cs` (7 tests minimum)
- [ ] `BodyMeasurementsControllerTests.cs` (5 tests minimum)
- [ ] `ShoppingListsControllerTests.cs` (6 tests minimum)
- [ ] `TrainersControllerTests.cs` (7 tests minimum)
- [ ] `AchievementsControllerTests.cs` (3 tests minimum)
- [ ] `ChatControllerTests.cs` (4 tests minimum)
- [ ] `UsersControllerTests.cs` (4 tests minimum)
- [ ] `AuditLogsControllerTests.cs` (4 tests minimum)
- [ ] `AdminOperationsTests.cs` (5 admin-specific tests)

#### Test Execution
```bash
# Run all tests
docker-compose --profile test up test

# Run specific test class
dotnet test --filter "FullyQualifiedName~MealPlansControllerTests"
```

### Test Patterns to Follow

**Pattern 1: Authorization Test**
```csharp
var user1 = await _fixture.SeedUserAsync(...);
var user2 = await _fixture.SeedUserAsync(...);
var entity = await _fixture.SeedEntityAsync(user1.Id, ...);

using var user2Client = _fixture.CreateAuthenticatedClient(user2.Id, user2.Email);
var response = await user2Client.GetAsync($"/api/Entity/{entity.Id}");
response.StatusCode.Should().Be(HttpStatusCode.NotFound);
```

**Pattern 2: Cascade Delete Test**
```csharp
var parent = await _fixture.SeedParentAsync(...);
var child = await _fixture.SeedChildAsync(parent.Id, ...);
await client.DeleteAsync($"/api/Parents/{parent.Id}");
var children = await _fixture.GetChildrenByParentId(parent.Id);
children.Should().BeEmpty();
```

**Pattern 3: Pagination Test**
```csharp
for (int i = 0; i < 25; i++) {
    await _fixture.SeedEntityAsync(...);
}
var response = await client.GetAsync("/api/Entities?page=1&pageSize=10");
var page1 = await response.Content.ReadFromJsonAsync<PagedResult<EntityDto>>();
page1.Items.Should().HaveCount(10);
page1.TotalPages.Should().Be(3);
```

### Files to Create
All in `/Users/nttyy/Documents/_Projects/macro_chef/backend/Mizan.Tests/Integration/`

---

## 5. PWA Implementation Guide

### Overview
Convert MacroChef into a Progressive Web App with:
- Installation prompts (Android + iOS)
- Offline support
- App shortcuts
- Update notifications

### Implementation Checklist

#### 1. Web App Manifest
- [ ] Create `frontend/app/manifest.ts` using Next.js built-in support
- [ ] Add app metadata (name, description, icons, screenshots)
- [ ] Define app shortcuts (Log Food, View Meal Plan)
- [ ] Update `frontend/app/layout.tsx` metadata

#### 2. App Icons
- [ ] Generate icons (72x72 to 512x512) using `pwa-asset-generator`
- [ ] Place in `frontend/public/icons/`
- [ ] Add Apple-specific icons (180x180)
- [ ] Create maskable icons with safe zone

#### 3. Service Worker with Serwist
- [ ] Install: `bun add @serwist/next @serwist/window serwist`
- [ ] Create `frontend/serwist.config.js`
- [ ] Create `frontend/app/sw.ts` (service worker source)
- [ ] Update `frontend/next.config.ts` with `withSerwist`
- [ ] Configure caching strategies (network-first for API, cache-first for assets)

#### 4. Offline Support
- [ ] Create `frontend/app/offline/page.tsx` (offline fallback)
- [ ] Configure service worker fallback routing
- [ ] Test offline behavior

#### 5. Installation Prompts
- [ ] Create `frontend/components/InstallPwaPrompt.tsx`
- [ ] Handle `beforeinstallprompt` event (Android)
- [ ] Show manual instructions for iOS
- [ ] Add to root layout

#### 6. Update Notifications
- [ ] Create `frontend/components/PwaUpdatePrompt.tsx`
- [ ] Listen for service worker `waiting` event
- [ ] Implement `skipWaiting` + reload flow
- [ ] Add to root layout

#### 7. Build & Deploy
- [ ] Run `bun run build` to generate service worker
- [ ] Verify `public/sw.js` exists
- [ ] Configure nginx to serve `/sw.js` with correct cache headers
- [ ] Deploy to production

#### 8. Testing
- [ ] Chrome DevTools → Application → Manifest (verify fields)
- [ ] Chrome DevTools → Application → Service Workers (verify registered)
- [ ] Lighthouse PWA audit (aim for 100 score)
- [ ] Test "Add to Home Screen" on mobile (Android + iOS)
- [ ] Test offline mode
- [ ] Test update flow

### Key Files to Create

```
frontend/
├── app/
│   ├── manifest.ts                         # NEW: Web app manifest
│   ├── sw.ts                               # NEW: Service worker source
│   ├── offline/page.tsx                    # NEW: Offline fallback page
│   └── register-sw.tsx                     # NEW: SW registration component
├── components/
│   ├── InstallPwaPrompt.tsx               # NEW: Installation prompt
│   └── PwaUpdatePrompt.tsx                # NEW: Update notification
├── public/
│   └── icons/                             # NEW: App icons (72-512px)
├── serwist.config.js                       # NEW: Serwist configuration
└── next.config.ts                          # MODIFY: Add withSerwist
```

### Installation Commands

```bash
cd frontend

# Install Serwist
bun add @serwist/next @serwist/window
bun add -D serwist

# Generate app icons (requires source logo)
npx pwa-asset-generator public/logo.png public/icons \
  --icon-only \
  --background "#ffffff" \
  --maskable-padding 0.1

# Build (generates service worker)
bun run build

# Test locally
bun run start
```

### Nginx Configuration

Add to nginx config:
```nginx
location /sw.js {
    add_header Cache-Control "public, max-age=0, must-revalidate";
    add_header Service-Worker-Allowed "/";
}
```

### Troubleshooting

**Issue**: Service worker not updating
**Solution**: Ensure `skipWaiting: true` in `sw.ts` and implement update prompt

**Issue**: iOS doesn't show install prompt
**Solution**: iOS requires manual "Add to Home Screen" - show instructions in prompt

**Issue**: API responses cached when they shouldn't be
**Solution**: Use `NetworkFirst` strategy for `/api/*` routes:
```typescript
runtimeCaching: [
  {
    urlPattern: /^https:\/\/api\.mizan\.euaell\.me\/.*/i,
    handler: 'NetworkFirst',
    options: {
      cacheName: 'api-cache',
      networkTimeoutSeconds: 10
    }
  }
]
```

---

## Priority Order

1. **High Priority** (Core functionality):
   - User Goal History & Pre-fill
   - Complete Meal Plan + Shopping Integration

2. **Medium Priority** (UX improvements):
   - Food Diary Chart Date Highlighting
   - PWA Implementation

3. **Lower Priority** (Quality assurance):
   - Comprehensive Backend Test Coverage

---

## Development Workflow

For each feature:
1. Create feature branch: `git checkout -b feature/goal-history`
2. Implement backend changes first
3. Run backend tests: `docker-compose --profile test up test`
4. Run `bun run codegen` to update frontend types
5. Implement frontend changes
6. Run frontend tests: `bun run test` and `bun run test:e2e`
7. Test manually in browser
8. Create pull request with description
9. Deploy to staging for QA

---

## Additional Resources

- **Next.js PWA Guide**: https://nextjs.org/docs/app/guides/progressive-web-apps
- **Serwist Documentation**: https://serwist.pages.dev/
- **Recharts Reference Lines**: https://recharts.org/en-US/api/ReferenceLine
- **xUnit Best Practices**: https://xunit.net/docs/comparisons
- **FluentAssertions Docs**: https://fluentassertions.com/introduction

---

**Document Version**: 1.0
**Last Updated**: 2026-02-16
**Author**: Claude (AI Assistant)
