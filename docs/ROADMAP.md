# MacroChef Advanced Features Roadmap

**Last Updated:** December 2025
**Version:** 1.0
**Status:** Planning & Prioritization

---

## Executive Summary

MacroChef has established a solid foundation with core meal planning, nutrition tracking, and trainer-client collaboration. This roadmap outlines the strategic direction for advanced features that will differentiate the platform, improve user engagement, and establish new revenue opportunities.

**Strategic Priorities:**
1. **P0 (Critical)**: Trainer feature completion and optimization
2. **P1 (High)**: AI-powered insights and meal suggestions
3. **P2 (Medium)**: Content moderation and safety
4. **P3 (Lower)**: Advanced third-party integrations

**Estimated Timeline:** 18-24 months for full roadmap execution

---

## Priority Legend

| Priority | Definition | Timeline | Business Impact |
|----------|-----------|----------|-----------------|
| **P0** | Core to business model; blocks other features | Q1-Q2 2026 | Revenue generation |
| **P1** | High-value user feature; differentiator | Q2-Q3 2026 | User engagement, retention |
| **P2** | Risk mitigation; compliance | Q3-Q4 2026 | Legal, safety, brand |
| **P3** | Nice-to-have; extends ecosystem | Q4 2026+ | Market expansion |

---

## 1. Trainer Feature Completion (P0)

**Business Value:** Trainers are the primary revenue drivers. Complete feature set enables certification programs, B2B partnerships, and premium tiers.

**Current State:** Basic trainer-client relationships, real-time chat, limited dashboard.

**Target State:** Complete trainer platform with bulk operations, advanced analytics, automated workflows, and admin tooling.

### 1.1 Meal Plan Assignment & Management

**Purpose:** Enable trainers to create, customize, and assign meal plans to multiple clients simultaneously.

**Scope:**
- Template-based meal plan creation (default plans for common goals)
- Bulk assignment to client cohorts
- Plan versioning and rollback capability
- Client acceptance/rejection workflow
- Modification tracking and audit trail

**Technical Requirements:**

**Backend:**
```csharp
// New domain entities
public class MealPlanTemplate
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public MealPlanGoal Goal { get; set; } // Bulk, Cut, Maintain
    public int CalorieTarget { get; set; }
    public MacroRatio MacroRatio { get; set; }
    public IList<TemplateMeal> Meals { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ClientMealPlanAssignment
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public Guid ClientId { get; set; }
    public Guid MealPlanId { get; set; }
    public AssignmentStatus Status { get; set; } // Pending, Active, Completed, Rejected
    public DateTime AssignedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public int DurationWeeks { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public IList<MealPlanModification> Modifications { get; set; }
}

public class BulkMealPlanAssignment
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public List<Guid> ClientIds { get; set; }
    public Guid MealPlanTemplateId { get; set; }
    public int DurationWeeks { get; set; }
    public DateTime ScheduledStartDate { get; set; }
    public BulkAssignmentStatus Status { get; set; } // Pending, Processing, Completed, Failed
    public DateTime CreatedAt { get; set; }
}

// New handlers
public class CreateMealPlanTemplateCommand : IRequest<MealPlanTemplateDto>
{
    public string Name { get; set; }
    public MealPlanGoal Goal { get; set; }
    public MacroRatio MacroRatio { get; set; }
    public List<MealDto> Meals { get; set; }
    public bool IsPublic { get; set; }
}

public class BulkAssignMealPlansCommand : IRequest<BulkAssignmentResultDto>
{
    public Guid MealPlanTemplateId { get; set; }
    public List<Guid> ClientIds { get; set; }
    public int DurationWeeks { get; set; }
    public DateTime StartDate { get; set; }
}
```

**Frontend:**
- Template selection UI with preview
- Bulk client selector with cohort grouping
- Assignment calendar showing rollout timeline
- Client notification system (email + in-app)
- Trainer dashboard showing assignment status and acceptance rates

**Endpoints:**
- `POST /api/Trainers/meal-plan-templates` - Create template
- `PUT /api/Trainers/meal-plan-templates/{id}` - Update template
- `POST /api/Trainers/bulk-assign-meal-plans` - Bulk assign
- `GET /api/Trainers/assignments/status` - Track assignment progress
- `PUT /api/Trainers/assignments/{id}/modify` - Modify active assignment

**Complexity:** Medium
**Estimated Effort:** 4-5 weeks
**Dependencies:** Client-trainer relationship model (complete), notification system

**Testing Requirements:**
- Template CRUD operations
- Bulk assignment with 50+ clients
- Idempotency (re-assigning same plan should not duplicate)
- Permission validation (trainer can only assign to own clients)
- Integration with notification system
- Audit trail verification

### 1.2 Progress Monitoring Dashboard

**Purpose:** Real-time visibility into client progress across nutrition, workouts, and body composition.

**Scope:**
- Aggregated client overview (status, progress metrics)
- Adherence tracking (plan compliance percentage)
- Trend analysis (30/60/90-day views)
- Alert system for concerning trends (under/overeating, no logs)
- Cohort performance comparison (anonymized)

**Technical Requirements:**

**Backend:**
```csharp
public class ClientProgressSnapshot
{
    public Guid ClientId { get; set; }
    public DateTime SnapshotDate { get; set; }

    // Nutrition
    public double AverageCaloriesLogged { get; set; }
    public double CalorieAdherence { get; set; } // % of target
    public double ProteintAdherence { get; set; }
    public double CarbAdherence { get; set; }
    public double FatAdherence { get; set; }

    // Workouts
    public int WorkoutsCompleted { get; set; }
    public int WorkoutsAssigned { get; set; }
    public double WorkoutAdherence { get; set; }

    // Body Comp
    public double? WeightChange { get; set; }
    public double? BodyFatChange { get; set; }
    public DateTime? LastMeasurement { get; set; }

    // Status
    public ClientStatus Status { get; set; }
    public List<AlertDto> Alerts { get; set; }
}

public class TrainerDashboardQuery : IRequest<TrainerDashboardDto>
{
    public int DaysBack { get; set; } = 30;
}

public class TrainerDashboardDto
{
    public int TotalClients { get; set; }
    public int ActiveClients { get; set; }
    public List<ClientProgressDto> ClientProgress { get; set; }
    public CohortMetricsDto CohortMetrics { get; set; }
    public List<AlertDto> SystemAlerts { get; set; }
}
```

**Frontend:**
- Dashboard grid layout with key metrics
- Client cards with color-coded status (on-track, needs-attention, off-track)
- Trend mini-charts (sparklines for quick assessment)
- Alert notification center
- Drill-down to client detail view
- Export capabilities (PDF report)

**Endpoints:**
- `GET /api/Trainers/dashboard?daysBack=30` - Get dashboard data
- `GET /api/Trainers/clients/{id}/progress?daysBack=90` - Client progress detail
- `GET /api/Trainers/cohort-metrics` - Cohort comparison (anonymized)
- `GET /api/Trainers/alerts` - Active alerts

**Complexity:** Medium
**Estimated Effort:** 3-4 weeks
**Dependencies:** Aggregation pipeline (data warehouse or cached snapshots), alert system

**Technical Considerations:**
- **Performance:** Dashboard with 100+ clients cannot query individual data on every load
- **Solution:** Pre-calculated snapshots stored in cache or separate analytics table
- **Refresh Rate:** Snapshots calculated nightly + on-demand updates for real-time view
- **Privacy:** Cohort metrics use anonymized aggregations (no individual attribution)

**Testing Requirements:**
- Snapshot accuracy (manual calculation vs aggregated)
- Adherence calculations with edge cases (partial weeks, plan changes)
- Alert threshold testing (ensure alerts trigger at defined thresholds)
- Performance with 1000+ client dataset

### 1.3 Client Goal Setting & Tracking

**Purpose:** Enable trainers to set and monitor quantitative goals with automated progress tracking.

**Scope:**
- Goal templates (weight loss, muscle gain, performance metrics)
- Trainer sets baseline + target + deadline
- Automated weekly progress checks
- Goal achievement notifications
- Obstacle/blocker tracking

**Technical Requirements:**

**Backend:**
```csharp
public class Goal
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid TrainerId { get; set; }
    public string Name { get; set; }
    public GoalType Type { get; set; } // Weight, BodyFat, Strength, NutritionHabit
    public double BaselineValue { get; set; }
    public double TargetValue { get; set; }
    public double CurrentValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime TargetDate { get; set; }
    public GoalStatus Status { get; set; } // Active, Completed, Missed, Paused
    public string Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GoalProgressCheckCommand : IRequest<Unit>
{
    public Guid GoalId { get; set; }
    public double CurrentValue { get; set; }
    public string Notes { get; set; }
}

public class GoalProgressNotificationHandler : INotificationHandler<GoalAchievedNotification>
{
    // Send SignalR notification to trainer + email
}
```

**Frontend:**
- Goal creation form with progress tracking
- Goal card showing % toward target
- Linear progress visualization
- Weekly check-in prompts
- Historical progress chart

**Endpoints:**
- `POST /api/Trainers/clients/{id}/goals` - Create goal
- `PUT /api/Trainers/goals/{id}` - Update goal
- `POST /api/Trainers/goals/{id}/check-in` - Record progress
- `GET /api/Trainers/clients/{id}/goals` - List client goals

**Complexity:** Small-Medium
**Estimated Effort:** 2-3 weeks
**Dependencies:** Notification system, real-time updates (SignalR)

### 1.4 Bulk Operations & Workflow Automation

**Purpose:** Enable trainers to manage 50+ clients efficiently with batch operations.

**Scope:**
- Bulk meal plan assignment (covered in 1.1)
- Bulk workout assignment
- Batch messaging (broadcast to cohort)
- Scheduled bulk actions (e.g., "start all clients on plan X next Monday")
- Operation history and rollback

**Technical Requirements:**

**Backend (Background Jobs):**
```csharp
// Using Hangfire or similar job queue

public class ScheduledBulkMealPlanAssignmentJob
{
    [Queue("bulk-operations")]
    public async Task Execute(Guid bulkAssignmentId)
    {
        // 1. Validate trainer has permission for all clients
        // 2. Create individual assignments
        // 3. Send notifications
        // 4. Log audit trail
    }
}

public class BulkWorkoutAssignmentCommand : IRequest<BulkOperationResultDto>
{
    public List<Guid> ClientIds { get; set; }
    public Guid WorkoutTemplateId { get; set; }
    public DateTime StartDate { get; set; }
    public int WeeksToAssign { get; set; }
}

public class BulkClientMessageCommand : IRequest<Unit>
{
    public List<Guid> ClientIds { get; set; }
    public string Message { get; set; }
    public DateTime? ScheduledFor { get; set; }
}
```

**Frontend:**
- Bulk action selectors (multi-select clients)
- Scheduled action UI with date/time pickers
- Operation queue showing pending/executing/completed jobs
- Batch edit interface (similar to spreadsheet)

**Endpoints:**
- `POST /api/Trainers/bulk-workout-assign` - Assign workout
- `POST /api/Trainers/bulk-message` - Broadcast message
- `GET /api/Trainers/bulk-operations/{id}` - Track operation status

**Complexity:** Medium
**Estimated Effort:** 3-4 weeks
**Dependencies:** Background job system (Hangfire), audit logging

**Technical Decisions:**
- Use **Hangfire** (already in .NET ecosystem) for job queuing
- Store operation history for audit trails
- Implement idempotency keys to prevent duplicate operations

### 1.5 Enhanced Permission & Access Control

**Purpose:** Fine-grained control over what trainers can see/do with each client.

**Scope:**
- Current: Basic boolean flags (canViewNutrition, etc.)
- Enhanced: Resource-level permissions (specific meal plans, workouts)
- Delegation permissions (trainer can assign other trainers to client)
- Time-bound permissions (access expires after program ends)

**Technical Requirements:**

**Backend:**
```csharp
// Extend existing relationship model
public class TrainerClientRelationship
{
    // ... existing fields ...

    // Fine-grained permissions
    public List<ResourcePermission> ResourcePermissions { get; set; }
    public List<ActionPermission> ActionPermissions { get; set; }
    public DateTime? PermissionExpiresAt { get; set; }
    public bool AllowDelegation { get; set; }
}

public class ResourcePermission
{
    public ResourceType Type { get; set; } // MealPlan, Workout, BodyMetric
    public Guid ResourceId { get; set; }
    public PermissionLevel Level { get; set; } // Read, ReadWrite, Delegate
}

// Enhanced authorization handler
public class TrainerAuthorizationHandler : AuthorizationHandler<ViewClientDataRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ViewClientDataRequirement requirement)
    {
        // Check time-based permissions
        // Check resource-specific permissions
        // Check delegation chain
    }
}
```

**Frontend:**
- Permission editor UI (matrix-style: resources × actions)
- Time-based expiration picker
- Delegation toggles

**Complexity:** Medium
**Estimated Effort:** 2-3 weeks
**Dependencies:** Authorization middleware refactor

---

## 2. AI-Powered Insights & Meal Suggestions (P1)

**Business Value:** Differentiation via smart recommendations, improves engagement, reduces trainer workload.

**Strategic Rationale:** Users increasingly expect AI assistance. Proper implementation builds trust in the platform.

**Current State:** No AI integration.

**Target State:** Autonomous meal suggestions, nutrition insights, personalized coaching via AI.

### 2.1 Personalized Meal Recommendations

**Purpose:** Suggest meals based on user goals, preferences, dietary restrictions, and adherence patterns.

**Scope:**
- Analyze user's food history + preferences
- Generate 5 meal suggestions for next week
- Consider macronutrient targets, calorie needs, seasonality
- Support dietary filters (vegan, gluten-free, keto, etc.)
- Learn from feedback (user skips meal → downrank similar suggestions)

**Technical Requirements:**

**Architecture:**
```
┌─────────────────────────────────────┐
│   User Profile (goals, restrictions)│
└────────────────┬────────────────────┘
                 │
         ┌───────▼────────┐
         │  ML Feature    │
         │  Engineering   │
         └───────┬────────┘
                 │
    ┌────────────┴────────────┐
    │   OpenAI GPT-4 API     │
    │  + Embeddings API      │
    └────────────┬────────────┘
                 │
    ┌────────────▼────────────┐
    │  Recommendation Engine  │
    │  (scoring, filtering)   │
    └────────────┬────────────┘
                 │
         ┌───────▼────────┐
         │   User View    │
         │ (Accept/Skip)  │
         └────────────────┘
```

**Backend:**
```csharp
public class MealRecommendationCommand : IRequest<List<MealRecommendationDto>>
{
    public int NumberOfSuggestions { get; set; } = 5;
    public DateTime ForDate { get; set; }
}

public class MealRecommendationService
{
    private readonly IOpenAiService _openAi;
    private readonly IMizanDbContext _context;
    private readonly IRedisCache _cache;

    public async Task<List<MealRecommendationDto>> GenerateRecommendations(
        Guid userId, DateTime forDate)
    {
        // 1. Fetch user profile (goals, preferences, restrictions)
        var user = await _context.Users
            .Include(u => u.DietaryRestrictions)
            .Include(u => u.FitnessGoals)
            .FirstOrDefaultAsync(u => u.Id == userId);

        // 2. Fetch recent food history (last 30 days)
        var foodHistory = await _context.FoodDiaryEntries
            .Where(e => e.UserId == userId && e.Date >= DateTime.Now.AddDays(-30))
            .Select(e => e.Food)
            .ToListAsync();

        // 3. Calculate macro targets for the day
        var targets = CalculateMacroTargets(user, forDate);

        // 4. Build prompt for GPT-4
        var prompt = BuildMealPrompt(user, foodHistory, targets);

        // 5. Call OpenAI API
        var recommendations = await _openAi.GenerateMealRecommendations(prompt);

        // 6. Score and filter recommendations
        var scored = ScoreRecommendations(recommendations, user, foodHistory);

        // 7. Cache results (24 hours)
        await _cache.SetAsync($"meal-recommendations:{userId}:{forDate:yyyy-MM-dd}",
            scored, TimeSpan.FromHours(24));

        return scored;
    }

    private string BuildMealPrompt(User user, List<Food> history, MacroTargets targets)
    {
        return $"""
        User Profile:
        - Goal: {user.FitnessGoal}
        - Dietary Restrictions: {string.Join(", ", user.DietaryRestrictions)}
        - Calorie Target: {targets.Calories}
        - Protein: {targets.Protein}g, Carbs: {targets.Carbs}g, Fat: {targets.Fat}g

        Recent Food Preferences (past 30 days):
        {string.Join("\n", history.GroupBy(f => f.Name).OrderByDescending(g => g.Count()).Take(10).Select(g => $"- {g.Key} ({g.Count()}x)"))}

        Generate 5 meal ideas that:
        1. Match the macro targets
        2. Respect dietary restrictions
        3. Include variety (avoid same foods daily)
        4. Are realistic to prepare (max 30 minutes)
        5. Consider seasonal availability

        Format as JSON array with: name, calories, protein, carbs, fat, ingredients, instructions
        """;
    }

    private List<MealRecommendationDto> ScoreRecommendations(
        List<MealRecommendationDto> recommendations,
        User user,
        List<Food> history)
    {
        return recommendations
            .Select(r => new
            {
                Recommendation = r,
                Score = CalculateScore(r, user, history)
            })
            .OrderByDescending(x => x.Score)
            .Take(5)
            .Select(x => x.Recommendation)
            .ToList();
    }

    private double CalculateScore(MealRecommendationDto recommendation, User user, List<Food> history)
    {
        double score = 100;

        // Penalize if macro targets are off
        var calorieDeviation = Math.Abs(recommendation.Calories - user.CalorieTarget) / user.CalorieTarget;
        score -= calorieDeviation * 20;

        // Penalize if user rarely eats main ingredient
        var mainIngredient = recommendation.Ingredients.First();
        var frequency = history.Count(f => f.Name.Contains(mainIngredient)) / (double)history.Count;
        if (frequency < 0.05) score -= 10; // Uncommon, might dislike

        // Boost if user has eaten similar recently
        if (frequency > 0.1) score += 5;

        return score;
    }
}

public class FeedbackMealRecommendationCommand : IRequest<Unit>
{
    public Guid RecommendationId { get; set; }
    public FeedbackType Type { get; set; } // Accepted, Skipped, Disliked
}
```

**Frontend:**
```typescript
// Component for meal suggestions
export function MealRecommendations() {
  const { suggestions, isLoading, refetch } = useMealRecommendations(selectedDate);

  return (
    <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
      {suggestions.map((meal) => (
        <MealCard
          key={meal.id}
          meal={meal}
          onAccept={() => handleAccept(meal.id)}
          onSkip={() => handleSkip(meal.id)}
          onViewRecipe={() => showRecipeDetail(meal.id)}
        />
      ))}
    </div>
  );
}
```

**Endpoints:**
- `GET /api/Foods/meal-recommendations?date=2026-01-15&count=5` - Get recommendations
- `POST /api/Foods/meal-recommendations/{id}/feedback` - User feedback
- `GET /api/Foods/recommendation-history` - See past recommendations (analytics)

**API Keys & Infrastructure:**
```yaml
# .env
OPENAI_API_KEY: "sk-..."
OPENAI_MODEL: "gpt-4-turbo"
OPENAI_EMBEDDING_MODEL: "text-embedding-3-small"
MEAL_RECOMMENDATION_CACHE_TTL: 86400 # 24 hours
```

**Cost Estimate:**
- GPT-4 Turbo: ~$0.03-0.06 per recommendation (includes prompt + response)
- For 1000 daily active users = $30-60/day in API costs
- Mitigated by caching (same recommendation used for similar users)

**Complexity:** Medium-Large
**Estimated Effort:** 5-6 weeks
**Dependencies:** OpenAI API account, error handling for API failures, caching infrastructure

**Testing Requirements:**
- Validate prompt construction (different user profiles)
- Test scoring algorithm with known preferences
- Mock OpenAI API for unit tests
- Verify caching (same user gets cached recommendation)
- Cost tracking (alert if API spend exceeds threshold)

**Fallback Strategy:**
If OpenAI API fails, fall back to deterministic recommendations:
```csharp
// Rule-based recommendations when AI unavailable
public class DeterministicMealRecommendationService
{
    public List<MealRecommendationDto> GetFallbackRecommendations(User user)
    {
        // 1. Get user's favorite foods from history
        // 2. Combine with foods matching goal macros
        // 3. Filter by dietary restrictions
        // 4. Return top 5
    }
}
```

### 2.2 AI-Generated Meal Plans

**Purpose:** Create complete weekly meal plans using AI, tailored to goals and preferences.

**Scope:**
- User selects goal (bulk, cut, maintain) + diet style
- AI generates balanced 7-day plan
- Trainer can regenerate with different constraints
- Shopping list auto-generated from ingredients
- Plan can be saved as template for future reuse

**Technical Requirements:**

**Backend:**
```csharp
public class GenerateMealPlanCommand : IRequest<MealPlanDto>
{
    public MealPlanGoal Goal { get; set; }
    public DietStyle DietStyle { get; set; } // Balanced, HighProtein, Keto, Mediterranean
    public int CalorieTarget { get; set; }
    public List<string> ExcludedIngredients { get; set; }
    public int MealsPerDay { get; set; } = 3;
}

public class MealPlanGenerationService
{
    public async Task<MealPlanDto> GenerateMealPlan(GenerateMealPlanCommand command)
    {
        // 1. Build comprehensive prompt
        var prompt = BuildMealPlanPrompt(command);

        // 2. Call GPT-4
        var generatedPlan = await _openAi.GenerateMealPlan(prompt);

        // 3. Validate macro adherence
        var validation = ValidatePlanMacros(generatedPlan, command);
        if (!validation.IsValid)
        {
            // Regenerate with constraints
            return await RegenerateWithConstraints(generatedPlan, validation.Errors);
        }

        // 4. Fetch/create recipe entities
        var recipeIds = await CreateOrLinkRecipes(generatedPlan.Meals.SelectMany(m => m.Recipes));

        // 5. Build shopping list
        var shoppingList = GenerateShoppingList(generatedPlan.Meals);

        // 6. Return plan DTO
        return MapToMealPlanDto(generatedPlan, recipeIds, shoppingList);
    }

    private string BuildMealPlanPrompt(GenerateMealPlanCommand command)
    {
        return $"""
        Create a {command.DietStyle} {command.Goal} meal plan.

        Constraints:
        - Daily calorie target: {command.CalorieTarget}
        - Meals per day: {command.MealsPerDay}
        - Exclude ingredients: {string.Join(", ", command.ExcludedIngredients)}

        Macro guidelines by goal:
        {GetMacroGuidelines(command.Goal, command.CalorieTarget)}

        Requirements:
        1. 7-day plan (Monday-Sunday)
        2. Breakfast, Lunch, Dinner (+ snacks if {command.MealsPerDay} > 3)
        3. Variety: no same meal more than twice per week
        4. Realistic recipes (max 30 minutes prep)
        5. Seasonally appropriate (winter 2026)

        Format: JSON with structure
        {{
          "weekPlan": [
            {{
              "day": "Monday",
              "meals": [
                {{
                  "type": "breakfast",
                  "name": "...",
                  "calories": 500,
                  "protein": 25,
                  "carbs": 60,
                  "fat": 15,
                  "ingredients": ["..."],
                  "instructions": "..."
                }}
              ]
            }}
          ],
          "shopping_list": [
            {{"item": "chicken breast", "quantity": "2 lbs", "category": "protein"}}
          ]
        }}
        """;
    }

    private (bool IsValid, List<string> Errors) ValidatePlanMacros(
        GeneratedMealPlan plan, GenerateMealPlanCommand command)
    {
        var errors = new List<string>();

        foreach (var day in plan.Days)
        {
            var dayCalories = day.Meals.Sum(m => m.Calories);
            var dayProtein = day.Meals.Sum(m => m.Protein);

            if (Math.Abs(dayCalories - command.CalorieTarget) > command.CalorieTarget * 0.1)
                errors.Add($"{day.Day}: Calories off by {Math.Abs(dayCalories - command.CalorieTarget)}");

            if (dayProtein < command.CalorieTarget * 0.3 / 4) // Min 30% calories from protein
                errors.Add($"{day.Day}: Protein too low ({dayProtein}g)");
        }

        return (errors.Count == 0, errors);
    }
}
```

**Frontend:**
- Wizard UI: Goal → Diet Style → Constraints → Review → Confirm
- Plan preview showing daily macros + meals
- Shopping list view with ingredient grouping
- Save as template option
- Regenerate button (same settings or modify)

**Endpoints:**
- `POST /api/Foods/generate-meal-plan` - Generate plan
- `POST /api/Foods/meal-plans/{id}/save-as-template` - Save as template
- `GET /api/Foods/meal-plans/{id}/shopping-list` - Get shopping list

**Complexity:** Large
**Estimated Effort:** 6-7 weeks
**Dependencies:** Meal recommendation service (2.1), recipe database, shopping list generation

**Validation Strategy:**
- Always validate AI output before saving
- If validation fails, regenerate automatically (up to 3 attempts)
- If all attempts fail, return error with specific macro violations
- Log all regenerations for quality monitoring

### 2.3 Nutrition Insights & Trend Analysis

**Purpose:** Provide users with actionable insights into their nutrition patterns.

**Scope:**
- Weekly summary: adherence to macros, trends
- Nutrient tracking (vitamins, minerals, fiber)
- Pattern detection (binge eating, consistent under-eating)
- Recommendations based on trends
- Comparison to personalized targets

**Technical Requirements:**

**Backend:**
```csharp
public class NutritionInsightsQuery : IRequest<NutritionInsightsDto>
{
    public int DaysBack { get; set; } = 30;
}

public class NutritionInsightService
{
    public async Task<NutritionInsightsDto> AnalyzeNutrition(Guid userId, int daysBack)
    {
        var entries = await _context.FoodDiaryEntries
            .Where(e => e.UserId == userId && e.Date >= DateTime.Now.AddDays(-daysBack))
            .Include(e => e.Food)
            .ToListAsync();

        var insights = new NutritionInsightsDto
        {
            PeriodSummary = CalculateSummary(entries),
            MacroAdherence = CalculateMacroAdherence(entries, userId),
            NutrientAnalysis = AnalyzeNutrients(entries),
            Patterns = DetectPatterns(entries),
            Recommendations = GenerateRecommendations(entries, userId)
        };

        return insights;
    }

    private MacroAdherenceDto CalculateMacroAdherence(List<FoodDiaryEntry> entries, Guid userId)
    {
        var target = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.MacroTarget)
            .FirstOrDefaultAsync();

        var groupedByDay = entries.GroupBy(e => e.Date);

        var dailyAdherence = groupedByDay.Select(g => new
        {
            Date = g.Key,
            CalorieAdherence = (g.Sum(e => e.Food.Calories) / target.Calories) * 100,
            ProteinAdherence = (g.Sum(e => e.Food.Protein) / target.Protein) * 100,
            CarbAdherence = (g.Sum(e => e.Food.Carbs) / target.Carbs) * 100,
            FatAdherence = (g.Sum(e => e.Food.Fat) / target.Fat) * 100
        }).ToList();

        return new MacroAdherenceDto
        {
            AverageCalorieAdherence = dailyAdherence.Average(d => d.CalorieAdherence),
            AverageProteinAdherence = dailyAdherence.Average(d => d.ProteinAdherence),
            ConsistencyScore = CalculateConsistency(dailyAdherence),
            DaysWithCompleteLog = dailyAdherence.Count(d => d.CalorieAdherence > 0),
            DailyBreakdown = dailyAdherence.Select(d => new DailyAdherenceDto
            {
                Date = d.Date,
                CalorieAdherence = d.CalorieAdherence,
                MacroAdherence = (d.ProteinAdherence + d.CarbAdherence + d.FatAdherence) / 3
            }).ToList()
        };
    }

    private PatternsDto DetectPatterns(List<FoodDiaryEntry> entries)
    {
        var patterns = new PatternsDto();

        // Detect binge days (>120% of target)
        var target = entries.GroupBy(e => e.Date).Average(g => g.Sum(e => e.Food.Calories));
        patterns.BingeDays = entries
            .GroupBy(e => e.Date)
            .Where(g => g.Sum(e => e.Food.Calories) > target * 1.2)
            .Select(g => g.Key)
            .ToList();

        // Detect under-eating days (<80% of target)
        patterns.UndereatDays = entries
            .GroupBy(e => e.Date)
            .Where(g => g.Sum(e => e.Food.Calories) < target * 0.8 && g.Sum(e => e.Food.Calories) > 0)
            .Select(g => g.Key)
            .ToList();

        // Detect weekday vs weekend patterns
        patterns.WeekendVsWeekday = CalculateWeekendDifference(entries);

        // Detect common trigger foods (frequently eaten together)
        patterns.CommonCombinations = DetectFoodCombinations(entries);

        return patterns;
    }

    private List<string> GenerateRecommendations(List<FoodDiaryEntry> entries, Guid userId)
    {
        var recommendations = new List<string>();

        var adherence = CalculateMacroAdherence(entries, userId);
        if (adherence.AverageProteinAdherence < 90)
            recommendations.Add("You're consistently under-hitting protein targets. Consider adding Greek yogurt or lean meats.");

        var patterns = DetectPatterns(entries);
        if (patterns.BingeDays.Count > 3)
            recommendations.Add("Pattern detected: Higher intake on weekends. Plan ahead to maintain consistency.");

        if (!entries.Any(e => e.Date == DateTime.Today))
            recommendations.Add("No food logged today yet. Logging now helps keep you on track.");

        return recommendations;
    }
}

public class NutritionInsightsDto
{
    public PeriodSummaryDto PeriodSummary { get; set; }
    public MacroAdherenceDto MacroAdherence { get; set; }
    public NutrientAnalysisDto NutrientAnalysis { get; set; }
    public PatternsDto Patterns { get; set; }
    public List<string> Recommendations { get; set; }
}

public class NutrientAnalysisDto
{
    public Dictionary<string, NutrientDto> VitaminsAndMinerals { get; set; }
    public FiberAnalysisDto Fiber { get; set; }
    public HydrationAnalysisDto Hydration { get; set; }
}

public class NutrientDto
{
    public string Name { get; set; }
    public double AverageDaily { get; set; }
    public double RecommendedDaily { get; set; }
    public double PercentOfTarget { get; set; }
    public NutrientStatus Status { get; set; } // Adequate, Deficient, Excess
}
```

**Frontend:**
- Summary cards (adherence %, consistency score, trending)
- Macro breakdown chart (pie chart or stacked bar)
- Daily adherence sparkline (quick visual)
- Nutrient deep-dive (collapsible sections)
- Patterns section (bullet-point insights)
- Recommendations section (AI-generated advice)

**Endpoints:**
- `GET /api/Foods/nutrition-insights?daysBack=30` - Get insights
- `GET /api/Foods/nutrition-trends?daysBack=90&metric=protein` - Time-series data

**Complexity:** Medium
**Estimated Effort:** 4-5 weeks
**Dependencies:** Food/nutrition database with complete nutrient data

**Data Requirements:**
- Food database must include vitamins, minerals, fiber (not just macros)
- Consider integrating with USDA FoodData Central API for comprehensive nutrient data

### 2.4 Macronutrient Optimization Recommendations

**Purpose:** AI-generated suggestions to adjust macros based on goals and progress.

**Scope:**
- Analyze current macro distribution vs goal
- Detect under/over-consumption patterns
- Recommend adjustments with reasoning
- A/B test different macro ratios (trainer-initiated)
- Auto-adjust based on progress (optional)

**Technical Requirements:**

**Backend:**
```csharp
public class OptimizeMacrosCommand : IRequest<MacroOptimizationDto>
{
    public int DaysOfHistory { get; set; } = 30;
}

public class MacroOptimizationService
{
    public async Task<MacroOptimizationDto> OptimizeMacros(Guid userId, int daysOfHistory)
    {
        // 1. Get current adherence
        var adherence = await _nutritionService.AnalyzeNutrition(userId, daysOfHistory);

        // 2. Get progress metrics (weight, body comp)
        var progress = await _progressService.GetProgress(userId, daysOfHistory);

        // 3. Build optimization prompt
        var prompt = BuildOptimizationPrompt(adherence, progress, userId);

        // 4. Call GPT-4
        var recommendation = await _openAi.OptimizeMacros(prompt);

        // 5. Validate recommendation
        ValidateOptimization(recommendation);

        return MapToDto(recommendation);
    }

    private string BuildOptimizationPrompt(
        NutritionInsightsDto adherence,
        ProgressDto progress,
        Guid userId)
    {
        return $"""
        User is a {(progress.IsGaining ? "muscle builder" : "fat loser")}.

        Current macro split: {adherence.MacroAdherence.ProteinRatio}% P, {adherence.MacroAdherence.CarbRatio}% C, {adherence.MacroAdherence.FatRatio}% F
        Target: {progress.Target.ProteinRatio}% P, {progress.Target.CarbRatio}% C, {progress.Target.FatRatio}% F

        Progress over {progress.DaysTracked} days:
        - Weight change: {progress.WeightChange:+0.0;-0.0}lbs
        - Goal pace: {progress.TargetWeightChangePerWeek}/week
        - Actual pace: {progress.ActualWeightChangePerWeek}/week

        Adherence metrics:
        - Protein adherence: {adherence.MacroAdherence.AverageProteinAdherence}%
        - Carb adherence: {adherence.MacroAdherence.AverageCarbAdherence}%
        - Fat adherence: {adherence.MacroAdherence.AverageFatAdherence}%

        Patterns:
        - Consistency score: {adherence.MacroAdherence.ConsistencyScore}/100
        - Weekday vs weekend difference: {adherence.Patterns.WeekendDifference}%

        Recommendation:
        - Should macros be adjusted? Why/why not?
        - If yes, suggest new ratios and reasoning
        - Are there adherence issues preventing progress? (e.g., can't hit protein)
        - Provide 2-3 specific action items

        Format as JSON with: recommendation, reasoning, new_macros, actions
        """;
    }
}

public class MacroOptimizationDto
{
    public string Status { get; set; } // OnTrack, NeedsAdjustment, ReevaluateGoal
    public string Reasoning { get; set; }
    public MacroTargetDto RecommendedMacros { get; set; }
    public List<ActionItemDto> ActionItems { get; set; }
    public double ConfidenceLevel { get; set; } // 0-1
}
```

**Frontend:**
- Optimization results card showing current vs recommended macros
- Visual comparison (side-by-side bar chart)
- Reasoning explanation (highlighted key factors)
- Action items list with implementation guidance
- "Apply recommendation" button (saves new macro targets)

**Endpoints:**
- `POST /api/Foods/optimize-macros` - Generate optimization
- `PUT /api/Users/macro-targets` - Update targets
- `GET /api/Foods/optimization-history` - See past optimizations

**Complexity:** Medium
**Estimated Effort:** 3-4 weeks
**Dependencies:** Progress tracking system, nutrition insights service

---

## 3. AI Content Moderation (P2)

**Business Value:** Risk mitigation (liability, brand protection), ensures safe platform for all users.

**Regulatory Compliance:** COPPA, GDPR, terms of service enforcement.

**Current State:** No content moderation.

**Target State:** Automated flagging with human review queue.

### 3.1 Image Content Moderation

**Purpose:** Prevent harmful, inappropriate, or policy-violating images from being posted.

**Scope:**
- Scan images on upload (before storage)
- Detect: explicit content, hate symbols, violence, medical misinformation
- Flag for human review if confidence < threshold
- Admin review queue with approve/reject
- Appeal process for false positives

**Technical Requirements:**

**Architecture:**
```
User Upload → Next.js API → ContentModeration Service
                              ├─ Image Check (Microsoft Azure Content Safety)
                              ├─ Classification
                              ├─ Risk Score
                              └─ Decision (Auto-approve, Auto-reject, Review queue)
```

**Backend:**
```csharp
public class ModerationService
{
    private readonly IContentSafetyClient _client;
    private readonly IMizanDbContext _context;
    private readonly ILogger<ModerationService> _logger;

    // Microsoft Azure Content Safety API integration
    public async Task<ImageModerationResult> ModerateImage(
        byte[] imageData,
        string imageUrl = null)
    {
        var request = new AnalyzeImageRequest
        {
            Image = new ImageData { Url = new Uri(imageUrl) },
            Categories = AnalyzeImageCategories.Hate |
                        AnalyzeImageCategories.SelfHarm |
                        AnalyzeImageCategories.Sexual |
                        AnalyzeImageCategories.Violence
        };

        var response = await _client.AnalyzeImageAsync(request);

        var result = new ImageModerationResult
        {
            ImageId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            Categories = response.CategoriesAnalysis.ToDictionary(
                c => c.Category.ToString(),
                c => new CategoryScore
                {
                    Score = c.Severity,
                    IsFiltered = c.Severity >= 6 // Threshold: 6/10
                }),
            Status = DetermineModerationStatus(response),
            RequiresReview = response.CategoriesAnalysis.Any(c => c.Severity >= 4 && c.Severity < 6)
        };

        // Log for audit trail
        await _context.ModerationLogs.AddAsync(new ModerationLog
        {
            ImageId = result.ImageId,
            Category = string.Join(",", response.CategoriesAnalysis.Select(c => c.Category)),
            Scores = JsonConvert.SerializeObject(result.Categories),
            Status = result.Status.ToString(),
            ReviewedAt = null,
            ReviewedBy = null
        });

        await _context.SaveChangesAsync();

        return result;
    }

    private ModerationStatus DetermineModerationStatus(AnalyzeImageResponse response)
    {
        var hasCritical = response.CategoriesAnalysis.Any(c => c.Severity >= 8);
        if (hasCritical) return ModerationStatus.Rejected;

        var requiresReview = response.CategoriesAnalysis.Any(c => c.Severity >= 4 && c.Severity < 8);
        if (requiresReview) return ModerationStatus.PendingReview;

        return ModerationStatus.Approved;
    }
}

public class ImageModerationResult
{
    public Guid ImageId { get; set; }
    public DateTime UploadedAt { get; set; }
    public Dictionary<string, CategoryScore> Categories { get; set; }
    public ModerationStatus Status { get; set; }
    public bool RequiresReview { get; set; }
}

public enum ModerationStatus
{
    Approved,
    PendingReview,
    Rejected,
    ApprovedOnAppeal
}

// Admin review handler
public class ReviewModerationQueueCommand : IRequest<Unit>
{
    public Guid ImageId { get; set; }
    public ModerationDecision Decision { get; set; } // Approve, Reject, NeedMoreInfo
    public string Notes { get; set; }
}

public class ModerationQueueQuery : IRequest<List<ModerationQueueItemDto>>
{
    public int PageSize { get; set; } = 20;
    public int PageNumber { get; set; } = 1;
}
```

**Frontend (Upload):**
```typescript
async function uploadImage(file: File): Promise<ImageUploadResult> {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch("/api/images/upload", {
    method: "POST",
    body: formData,
    headers: { Authorization: `Bearer ${token}` },
  });

  const result: ImageUploadResult = await response.json();

  if (result.status === "approved") {
    // Image passes moderation, save immediately
    return result;
  } else if (result.status === "pending_review") {
    // Show message: "Your image is under review by our team"
    // Allow user to proceed but image not visible until approved
    return result;
  } else if (result.status === "rejected") {
    // Show specific reason for rejection
    throw new Error(`Image rejected: ${result.rejectionReason}`);
  }
}
```

**Frontend (Admin Panel):**
- Review queue showing thumbnails
- Category breakdown with confidence scores
- Approve/Reject buttons with notes field
- Appeal history for each image
- Bulk actions (approve 10, reject 10)

**Endpoints:**
- `POST /api/images/upload` - Upload with moderation
- `GET /api/admin/moderation/queue` - Review queue (admin only)
- `POST /api/admin/moderation/{imageId}/review` - Admin review decision
- `POST /api/images/{id}/appeal` - User appeal

**Configuration:**
```json
{
  "ContentModeration": {
    "Provider": "AzureContentSafety",
    "ApiKey": "***",
    "Endpoint": "https://[name].cognitiveservices.azure.com/",
    "AutoRejectThreshold": 8,
    "ReviewQueueThreshold": 4,
    "Enabled": true
  }
}
```

**Complexity:** Medium
**Estimated Effort:** 3-4 weeks
**Dependencies:** Azure Content Safety API account, image storage (AWS S3 or similar)

**Cost Estimate:**
- Azure Content Safety: ~$1 per 1000 images
- For 10k images/month = ~$10/month
- Minimal cost for risk mitigation

**Fallback Strategy:**
If moderation API fails:
```csharp
public class FailsafeModerationService
{
    // If Azure is down, queue for manual review
    // Allow upload with "pending_moderation" status
    // Admin reviews manually
    public async Task<ImageModerationResult> ModerateWithFailsafe(byte[] imageData)
    {
        try
        {
            return await _azureService.ModerateImage(imageData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure moderation failed, queuing for manual review");
            return new ImageModerationResult
            {
                Status = ModerationStatus.PendingReview,
                ManualReviewRequired = true
            };
        }
    }
}
```

### 3.2 Text Content Moderation

**Purpose:** Flag harmful comments, messages, or inappropriate user-generated text.

**Scope:**
- Monitor user comments, meal descriptions, post captions
- Detect: hate speech, harassment, misinformation, spam
- Automated removal of policy violations
- User appeals process

**Technical Requirements:**

**Backend:**
```csharp
public class TextModerationService
{
    private readonly IOpenAiModerationApi _openAi;

    public async Task<TextModerationResult> ModerateText(string text)
    {
        // OpenAI Moderation API
        var response = await _openAi.Moderation.CallModerationApi(text);

        var result = new TextModerationResult
        {
            Text = text,
            IsFlagged = response.Results[0].Flagged,
            Categories = response.Results[0].Categories.ToDictionary(
                c => c.Key,
                c => response.Results[0].CategoryScores[c.Key]),
            Status = response.Results[0].Flagged
                ? ModerationStatus.Rejected
                : ModerationStatus.Approved
        };

        return result;
    }
}

// Usage in message handler
public class SendChatMessageCommand : IRequest<ChatMessageDto>
{
    public string Message { get; set; }
    public Guid RecipientId { get; set; }
}

public class SendChatMessageHandler : IRequestHandler<SendChatMessageCommand, ChatMessageDto>
{
    public async Task<ChatMessageDto> Handle(SendChatMessageCommand request)
    {
        // 1. Moderate text
        var moderation = await _textModerationService.ModerateText(request.Message);

        // 2. Block if flagged
        if (moderation.IsFlagged)
            throw new PolicyViolationException("Message violates community guidelines");

        // 3. Save message
        var message = new ChatMessage { /* ... */ };
        await _context.ChatMessages.AddAsync(message);
        await _context.SaveChangesAsync();

        return MapToDto(message);
    }
}
```

**Complexity:** Small
**Estimated Effort:** 2-3 weeks
**Dependencies:** OpenAI Moderation API

**Cost:** Free (included with OpenAI API subscription)

### 3.3 Automated Moderation Review Queue & Appeals

**Purpose:** Manage false positives and provide user recourse.

**Scope:**
- Admin dashboard for reviewing flagged content
- Bulk actions
- User appeal submission
- Audit trail of all moderation decisions

**Technical Requirements:**

**Backend:**
```csharp
public class ModerationAppeal
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; }
    public AppealStatus Status { get; set; } // Pending, Approved, Denied
    public Guid? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class SubmitModerationAppealCommand : IRequest<Unit>
{
    public Guid ContentId { get; set; }
    public string Reason { get; set; }
}

public class ReviewModerationAppealCommand : IRequest<Unit>
{
    public Guid AppealId { get; set; }
    public bool Approve { get; set; }
    public string AdminNotes { get; set; }
}
```

**Frontend (User):**
- Appeal button on flagged content
- Modal with reason input
- Appeal status tracking

**Frontend (Admin):**
- Appeal queue sorted by date
- Original content + moderation flags
- Approve/Deny buttons
- Notes for audit trail

**Endpoints:**
- `POST /api/moderation/appeals` - Submit appeal
- `GET /api/moderation/appeals/my` - User's appeals
- `GET /api/admin/moderation/appeals` - Admin queue
- `POST /api/admin/moderation/appeals/{id}/review` - Admin decision

**Complexity:** Small
**Estimated Effort:** 2 weeks
**Dependencies:** Moderation system (3.1, 3.2)

---

## 4. Additional Advanced Features (P3)

**Lower priority features that extend ecosystem and market appeal.**

### 4.1 Barcode Scanning for Food Logging

**Purpose:** Reduce friction in logging meals by scanning barcodes instead of manual search.

**Scope:**
- Mobile-compatible barcode scanner (browser-based Camera API)
- Integration with barcode database (OpenFoodFacts or similar)
- Fallback to manual entry
- Save frequently scanned items

**Technical Requirements:**

**Frontend:**
```typescript
import { useBarcodeScan } from "@/lib/hooks/useBarcodeScan";
import { useFoodSearch } from "@/lib/hooks/useFoodSearch";

export function BarcodeScanner() {
  const { startScan, isScanning } = useBarcodeScan();
  const { searchFood, isLoading } = useFoodSearch();

  const handleScan = async (barcode: string) => {
    // 1. Query local database first
    let food = await searchFood(`barcode:${barcode}`);

    // 2. If not found, query OpenFoodFacts API
    if (!food) {
      const response = await fetch(
        `https://world.openfoodfacts.org/api/v3/product/${barcode}.json`
      );
      const data = await response.json();
      food = parseOpenFoodFactsData(data);
    }

    // 3. Add to diary
    await addFoodToDiary(food);
  };

  return (
    <div>
      <button onClick={() => startScan()}>
        {isScanning ? "Scanning..." : "Scan Barcode"}
      </button>
      <video ref={videoRef} style={{ width: "100%" }} />
    </div>
  );
}
```

**Backend Integration:**
```csharp
public class OpenFoodFactsService
{
    public async Task<FoodDto> GetFoodByBarcode(string barcode)
    {
        var cacheKey = $"barcode:{barcode}";
        var cached = await _cache.GetAsync<FoodDto>(cacheKey);
        if (cached != null) return cached;

        var response = await _httpClient.GetAsync(
            $"https://world.openfoodfacts.org/api/v3/product/{barcode}.json");

        if (!response.IsSuccessStatusCode)
            throw new FoodNotFoundException($"Barcode {barcode} not found");

        var json = await response.Content.ReadAsAsync<JObject>();
        var food = ParseOpenFoodFactsProduct(json);

        await _cache.SetAsync(cacheKey, food, TimeSpan.FromDays(30));

        return food;
    }
}
```

**Endpoints:**
- `GET /api/Foods/search?barcode={code}` - Search by barcode
- `GET /api/Foods/{id}/add-to-diary` - Quick add (logged food, portion)

**Complexity:** Small-Medium
**Estimated Effort:** 2-3 weeks
**Dependencies:** Camera API (browser feature), OpenFoodFacts API (free)

### 4.2 Fitness Tracker Integration

**Purpose:** Import workout and step data from Fitbit, Apple Health, Google Fit.

**Scope:**
- OAuth integration with each platform
- Auto-sync workouts, steps, heart rate
- Merge with manual workout logs
- Historical data import on first connect

**Technical Requirements:**

**Architecture:**
```
┌──────────────────────────────────────┐
│  Fitness Tracker Integration         │
│  (Fitbit, Apple Health, Google Fit)  │
└────────────────┬─────────────────────┘
                 │
      ┌──────────┼──────────┐
      │          │          │
   Fitbit    AppleHealth  GoogleFit
      │          │          │
      └──────────┼──────────┘
                 │
         ┌───────▼───────┐
         │ Sync Service  │
         │ (Daily sync)  │
         └───────┬───────┘
                 │
    ┌────────────▼────────────┐
    │ Normalize to Workouts   │
    │ (MacroChef format)      │
    └────────────┬────────────┘
                 │
    ┌────────────▼────────────┐
    │ Merge with manual logs  │
    │ (Avoid duplicates)      │
    └────────────┬────────────┘
                 │
      ┌──────────▼──────────┐
      │ User Dashboard      │
      │ (Aggregated view)   │
      └─────────────────────┘
```

**Backend:**
```csharp
public class FitbitSyncService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMizanDbContext _context;

    public async Task<List<WorkoutDto>> SyncFitbitWorkouts(Guid userId, string accessToken)
    {
        var client = _httpFactory.CreateClient("Fitbit");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Fetch workouts for past 30 days
        var response = await client.GetAsync("https://api.fitbit.com/1/user/-/activities/date/2026-01-15.json");
        var json = await response.Content.ReadAsAsync<JObject>();

        var workouts = ParseFitbitActivities(json);

        // Store/update in database
        foreach (var workout in workouts)
        {
            var existing = await _context.Workouts
                .FirstOrDefaultAsync(w => w.ExternalId == workout.ExternalId);

            if (existing != null)
                existing.Update(workout);
            else
                await _context.Workouts.AddAsync(MapToWorkout(workout, userId));
        }

        await _context.SaveChangesAsync();
        return workouts;
    }
}

public class FitnessTrackerIntegrationCommand : IRequest<Unit>
{
    public string Provider { get; set; } // Fitbit, AppleHealth, GoogleFit
    public string AuthCode { get; set; }
}

public class FitnessTrackerIntegration
{
    public Guid UserId { get; set; }
    public string Provider { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime TokenExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastSyncAt { get; set; }
}
```

**Frontend:**
- Settings page with "Connect Tracker" buttons
- OAuth pop-up for each platform
- Sync status (last sync, next sync)
- Historical import progress

**Complexity:** Medium-Large
**Estimated Effort:** 5-6 weeks (per platform)
**Dependencies:** OAuth2, API clients for each platform

**Platforms to Support (Phased):**
1. **Phase 1:** Fitbit (largest fitness tracker user base)
2. **Phase 2:** Apple Health (iOS users)
3. **Phase 3:** Google Fit (Android users)

### 4.3 Recipe Import from Websites

**Purpose:** Allow users to import recipes from popular cooking sites (AllRecipes, Food Network, etc.).

**Scope:**
- Web scraper to parse recipe HTML
- Extract ingredients, instructions, nutrition (if available)
- Save as MacroChef recipe
- Support common recipe schema (JSON-LD)

**Technical Requirements:**

**Backend:**
```csharp
public class RecipeImportService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IHtmlParser _htmlParser;

    public async Task<RecipeDto> ImportFromUrl(string recipeUrl)
    {
        // 1. Fetch HTML
        var html = await _httpFactory.CreateClient().GetStringAsync(recipeUrl);

        // 2. Parse JSON-LD schema
        var recipe = ExtractRecipeSchema(html);

        // 3. Parse ingredients and match against food database
        var ingredients = await MatchIngredients(recipe.Ingredients);

        // 4. Estimate nutrition (if not in schema)
        if (!recipe.HasNutrition)
            recipe.Nutrition = await EstimateNutrition(ingredients);

        // 5. Create MacroChef recipe
        return MapToMacroChefRecipe(recipe, ingredients);
    }

    private RecipeSchema ExtractRecipeSchema(string html)
    {
        // Parse JSON-LD schema from HTML
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var schemaScripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        foreach (var script in schemaScripts)
        {
            var json = JObject.Parse(script.InnerText);
            if (json["@type"]?.ToString() == "Recipe")
                return JsonConvert.DeserializeObject<RecipeSchema>(json.ToString());
        }

        throw new RecipeParseException($"No Recipe schema found at {recipeUrl}");
    }

    private async Task<List<FoodMatch>> MatchIngredients(List<string> ingredients)
    {
        var matches = new List<FoodMatch>();

        foreach (var ingredient in ingredients)
        {
            // Parse quantity and unit
            var parsed = ParseIngredientLine(ingredient);

            // Search database for food
            var food = await _context.Foods
                .Where(f => f.Name.Contains(parsed.Name))
                .FirstOrDefaultAsync();

            if (food == null)
                food = await _openFoodFactsService.SearchFood(parsed.Name);

            matches.Add(new FoodMatch
            {
                Food = food,
                Quantity = parsed.Quantity,
                Unit = parsed.Unit
            });
        }

        return matches;
    }
}

public class ImportRecipeCommand : IRequest<RecipeDto>
{
    public string RecipeUrl { get; set; }
}
```

**Frontend:**
- Recipe import modal with URL input
- Preview showing extracted recipe
- Ingredient verification (user confirms matches)
- Save as personal recipe

**Endpoints:**
- `POST /api/Recipes/import-from-url` - Import recipe

**Complexity:** Medium
**Estimated Effort:** 3-4 weeks
**Dependencies:** HTML parsing library, recipe schema parsing

**Supported Sites (Initially):**
- AllRecipes
- Food Network
- BBC Good Food
- (any site with JSON-LD Recipe schema)

### 4.4 Social Features

**Purpose:** Community engagement, recipe sharing, social accountability.

**Scope:**
- Share recipes, meal plans, achievements
- Social feed (trending recipes, community challenges)
- Leaderboards (achievements)
- Private community groups (household + friends)

**Technical Requirements:**

Not included in this roadmap depth - would require significant UI/backend work. Estimate **Large (8-10 weeks)** and **P3 priority**.

**Key Services:**
- Feed generation (activity feed, following system)
- Social graph (friends, followers)
- Privacy controls (public, friends-only, private)
- Notifications (friend added, recipe liked, achievement unlocked)

### 4.5 Advanced Analytics & Reporting

**Purpose:** Data-driven insights for trainers and users.

**Scope:**
- Advanced charts (multi-metric trends, correlation analysis)
- Custom report generation (PDF export)
- Predictive analytics (body weight trajectory)
- Cohort analysis (trainer comparing client groups)

**Technical Requirements:**

**Example: Weight Projection**
```csharp
public class WeightProjectionService
{
    public WeightProjectionDto ProjectWeight(Guid userId, int weeksAhead)
    {
        // 1. Get last 4 weeks of weight data
        var weights = await _context.BodyMeasurements
            .Where(m => m.UserId == userId && m.MeasurementType == MeasurementType.Weight)
            .OrderByDescending(m => m.MeasuredAt)
            .Take(28)
            .ToListAsync();

        // 2. Fit linear regression
        var trend = FitTrendLine(weights);

        // 3. Project forward
        var projectedWeight = trend.Slope * weeksAhead + trend.Intercept;
        var goalAchievementDate = CalculateGoalDate(trend, userGoal);

        return new WeightProjectionDto
        {
            CurrentWeight = weights.First().Value,
            ProjectedWeight = projectedWeight,
            TrendSlope = trend.Slope,
            GoalAchievementDate = goalAchievementDate,
            Confidence = CalculateConfidence(weights)
        };
    }
}
```

Estimate **Large (6-8 weeks)** and **P3 priority**.

### 4.6 Mobile App (React Native or Flutter)

**Purpose:** Expand to iOS/Android native apps.

**Scope:**
- Replicate web app features for mobile
- Native camera for photo logging
- Push notifications
- Offline-first sync

**Complexity:** Extra-Large
**Estimated Effort:** 12-16 weeks
**Priority:** P3 (lower)

**Recommendation:** Consider after core features (P0, P1, P2) are complete and stable.

---

## Implementation Timeline & Dependencies

### Phase 1: Q1-Q2 2026 (Trainer Completion - P0)

| Feature | Start | Duration | End | Dependencies |
|---------|-------|----------|-----|--------------|
| 1.1 Meal Plan Assignment | 1/6 | 4w | 2/3 | None |
| 1.2 Progress Dashboard | 1/13 | 3w | 2/3 | 1.1 (partial) |
| 1.3 Goal Setting | 1/20 | 2w | 2/3 | Notification system |
| 1.4 Bulk Operations | 2/3 | 3w | 2/24 | 1.1-1.3 (all) |
| 1.5 Enhanced Permissions | 2/3 | 2w | 2/17 | 1.1-1.4 (all) |

**Milestone:** End of Q2 = Complete trainer platform

### Phase 2: Q2-Q3 2026 (AI Features - P1)

| Feature | Start | Duration | End | Dependencies |
|---------|-------|----------|-----|--------------|
| 2.1 Meal Recommendations | 2/17 | 5w | 3/24 | Food database, OpenAI API |
| 2.2 AI Meal Plans | 3/10 | 6w | 4/21 | 2.1 (complete) |
| 2.3 Nutrition Insights | 3/24 | 4w | 4/21 | Food database with nutrients |
| 2.4 Macro Optimization | 4/7 | 3w | 4/28 | 2.3 (complete) |

**Milestone:** End of Q3 = AI assistant complete

### Phase 3: Q3-Q4 2026 (Safety & Moderation - P2)

| Feature | Start | Duration | End | Dependencies |
|---------|-------|----------|-----|--------------|
| 3.1 Image Moderation | 4/28 | 3w | 5/19 | Azure Content Safety API |
| 3.2 Text Moderation | 5/5 | 2w | 5/19 | OpenAI Moderation API |
| 3.3 Appeals System | 5/19 | 2w | 6/2 | 3.1-3.2 (both) |

**Milestone:** End of Q4 = Moderation complete

### Phase 4: Q4 2026 + (Extensions - P3)

Implement as capacity allows, prioritized by business impact:
1. Barcode scanning (low effort, high engagement impact)
2. Fitness tracker integration (high effort, high engagement impact)
3. Recipe import (medium effort, medium impact)
4. Social features (high effort, engagement differentiator)
5. Advanced analytics (medium effort, trainer value add)
6. Mobile app (very high effort, market expansion)

---

## Technical Debt & Infrastructure

### Required Investments (No User-Facing Value, Essential for Scale)

#### 1. Analytics & Monitoring Infrastructure

**Purpose:** Track feature usage, debug issues, monitor performance.

**Components:**
- **Application Insights:** (Azure) - Telemetry + error tracking
- **Serilog:** Structured logging
- **Grafana:** Dashboards for ops team

**Effort:** 2-3 weeks
**Cost:** ~$50-100/month (Azure, Grafana Cloud)

**Critical Metrics to Track:**
- API latency (p50, p95, p99)
- Error rates by endpoint
- OpenAI API costs + rate limit usage
- Feature adoption (% of users using meal recommendations, etc.)
- Moderation queue length

#### 2. Background Job Queue (Already Partially Done)

**Purpose:** Handle long-running tasks (bulk assignments, AI generation, syncs).

**Status:** Hangfire infrastructure exists
**Next Steps:** Expand for scheduled jobs (daily snapshots, email digests, weekly syncs)

**Estimated Effort:** 1-2 weeks
**Cost:** Minimal (self-hosted Hangfire dashboard)

#### 3. Data Warehouse (Future-Looking)

**Purpose:** As data volume grows, real-time queries will be slow. Need separate OLAP database.

**Timeline:** Post-P1 (when aggregations become bottleneck)

**Options:**
- **Postgres Materialized Views:** Simplest, no extra infrastructure
- **BigQuery:** Google's data warehouse (serverless, scalable)
- **Snowflake:** Enterprise option (expensive)

#### 4. API Rate Limiting & Quota Management

**Purpose:** Prevent abuse, manage costs (OpenAI, Azure).

**Components:**
- IP-based rate limiting (general DoS protection)
- Per-user rate limiting (API quota)
- API key management (trainers, third-party integrations)

**Effort:** 1 week
**Cost:** Minimal

### Testing Infrastructure Additions

**Current:** xUnit + Vitest
**Gaps:**
- Performance testing (load testing meal plan generation)
- AI integration testing (mock OpenAI responses)
- Moderation testing (comprehensive content test sets)

**Effort:** 2-3 weeks

---

## Success Metrics & KPIs

### Feature Adoption

| Feature | Target | Measurement |
|---------|--------|-------------|
| Meal Recommendations | 40% of users | Weekly active users / Total |
| AI Meal Plans | 20% of users | Plans created via AI / Total |
| Trainer Features | 90% of trainers | Bulk operations used / Total trainers |
| Moderation | 100% of uploads | Images scanned / Images uploaded |

### Engagement

| Metric | Target | Current |
|--------|--------|---------|
| Daily Active Users (DAU) | +30% after AI features | To be established |
| Retention (30-day) | 60% | To be established |
| Time in app (avg daily) | +15 min after AI | To be established |

### Business Impact

| Metric | Target | Timeline |
|--------|--------|----------|
| Premium conversions (trainers) | 20% of users | Q3 2026 |
| Cost per recommendation | <$0.05 | Ongoing optimization |
| Moderation accuracy | >95% | Ongoing improvement |

---

## Risk Assessment & Mitigation

### Technical Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|-----------|
| OpenAI API rate limits | Meal plan generation fails | Medium | Implement queue, fallback to deterministic |
| Food database incomplete | Poor recommendations | Medium | Integrate OpenFoodFacts, user feedback loop |
| Image moderation false positives | User complaints | Medium | Appeal system, continuous model tuning |
| Performance degradation | Dashboard slow at scale | Medium | Implement snapshots, data warehouse |

### Business Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|-----------|
| AI costs exceed budget | Profitability hit | Low | Usage monitoring, feature gating for free tier |
| Moderation liability | Legal exposure | Low | Legal review, comprehensive audit trail |
| Market saturation | User growth stalls | Medium | Focus on differentiation (trainer features) |

### Regulatory Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|-----------|
| GDPR data deletion | Audit trail incomplete | Low | Implement anonymization, data retention policy |
| Nutrition advice liability | Health claim issues | Low | Disclaimer that AI is "informational only" |
| Content moderation compliance | COPPA, GDPR | Medium | Legal review of moderation policies |

---

## Budget Estimate

### Development Costs

| Phase | Features | Engineers | Weeks | Cost (at $150/hr × 40h/wk) |
|-------|----------|-----------|-------|---------------------------|
| P0 | Trainer completion | 1 | 14 | $84,000 |
| P1 | AI features | 1 | 18 | $108,000 |
| P2 | Moderation | 0.5 | 7 | $42,000 |
| P3 | Extensions | TBD | TBD | TBD |

**Total (P0-P2):** ~$234,000 over 9 months

### Infrastructure & API Costs (Annual)

| Service | Monthly | Annual |
|---------|---------|--------|
| OpenAI API (meal recommendations) | ~$500 | $6,000 |
| Azure Content Safety | ~$50 | $600 |
| Azure Application Insights | ~$50 | $600 |
| AWS S3 (image storage) | ~$100 | $1,200 |
| Hangfire (job queue) | $0 (self-hosted) | $0 |
| Postgres (larger instance) | ~$200 | $2,400 |
| Redis (larger instance) | ~$50 | $600 |
| **Total** | **~$950** | **$11,400** |

---

## Rollout Strategy

### Feature Flags

All new features behind feature flags for gradual rollout:

```csharp
public class FeatureFlags
{
    public bool MealRecommendationsEnabled { get; set; }
    public bool AiMealPlansEnabled { get; set; }
    public bool ContentModerationEnabled { get; set; }
    public double MealRecommendationBeta { get; set; } // % of users
}
```

### Phases

1. **Alpha (Internal):** Feature team + close users (Week 1)
2. **Beta (Limited):** 10% of user base (Weeks 2-3)
3. **Wider Beta (50%):** If metrics good (Weeks 4-6)
4. **General Availability:** Full rollout (Week 7+)

**Rollback:** Each phase can be disabled via feature flags in < 1 second.

---

## Documentation & Knowledge Transfer

As features are implemented:

1. **CLAUDE.md Updates:** Instructions for Claude when working on features
2. **API_REFERENCE.md:** New endpoint documentation
3. **ARCHITECTURE.md:** System-level changes (AI integration patterns, etc.)
4. **Implementation Guides:** Step-by-step for complex features (meal plan generation, moderation flow)
5. **Cost Tracking Guide:** How to monitor OpenAI/Azure spend

---

## Conclusion

This roadmap establishes a clear path to a fully-featured meal planning and training platform with AI-powered insights. The phased approach prioritizes:

1. **Revenue generation** (Trainer features)
2. **User engagement** (AI recommendations)
3. **Risk mitigation** (Content moderation)
4. **Market expansion** (Third-party integrations)

Regular review (quarterly) is recommended to adjust based on:
- User adoption metrics
- Engineering capacity
- Competitive landscape
- Technology changes (new LLM models, moderation APIs)

The estimated 18-24 month timeline assumes a team of 1-2 full-time engineers. Scaling the team can compress timelines accordingly.
