/**
 * DATABASE SCHEMA - FRONTEND (DRIZZLE/BETTER-AUTH)
 *
 * ⚠️ CRITICAL: This is the SOURCE OF TRUTH for user related database schema.
 *
 * When modifying tables, you MUST update the corresponding backend entities:
 * - Users table → backend/Mizan.Domain/Entities/User.cs
 * - Sessions table → backend/Mizan.Domain/Entities/Session.cs
 * - Accounts table → backend/Mizan.Domain/Entities/Account.cs
 * - All other tables → backend/Mizan.Domain/Entities/
 *
 * After updating backend entities, create EF Core migration:
 * cd backend/Mizan.Api
 * dotnet ef migrations add <MigrationName> --project ../Mizan.Infrastructure --startup-project .
 */

import {
  pgTable,
  uuid,
  varchar,
  text,
  boolean,
  timestamp,
  integer,
  decimal,
  date,
  jsonb,
  primaryKey,
  unique,
  index,
} from "drizzle-orm/pg-core";

// ==================== BetterAuth Core Tables ====================

export const users = pgTable("users", {
  id: uuid("id").defaultRandom().primaryKey(),
  email: varchar("email", { length: 255 }).notNull().unique(),
  emailVerified: boolean("email_verified").default(false),
  name: varchar("name", { length: 255 }),
  image: text("image"),
  role: varchar("role", { length: 50 }).default("user"),
  banned: boolean("banned").default(false),
  banReason: text("ban_reason"),
  banExpires: timestamp("ban_expires", { withTimezone: true }),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

export const accounts = pgTable(
  "accounts",
  {
    id: uuid("id").defaultRandom().primaryKey(),
    accountId: text("account_id").notNull(),
    providerId: text("provider_id").notNull(),
    userId: uuid("user_id")
      .references(() => users.id, { onDelete: "cascade" })
      .notNull(),
    accessToken: text("access_token"),
    refreshToken: text("refresh_token"),
    idToken: text("id_token"),
    accessTokenExpiresAt: timestamp("access_token_expires_at", { withTimezone: true }),
    refreshTokenExpiresAt: timestamp("refresh_token_expires_at", { withTimezone: true }),
    scope: text("scope"),
    password: text("password"),
    createdAt: timestamp("created_at", { withTimezone: true }).defaultNow().notNull(),
    updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow().notNull(),
  },
  (t) => ({
    userIdIdx: index("accounts_userId_idx").on(t.userId),
  })
);

export const sessions = pgTable("sessions", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  token: varchar("token", { length: 255 }).notNull().unique(),
  expiresAt: timestamp("expires_at", { withTimezone: true }).notNull(),
  ipAddress: text("ip_address"),
  userAgent: text("user_agent"),
  impersonatedBy: uuid("impersonated_by"),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow().notNull(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow().notNull(),
});

export const verification = pgTable("verification", {
  id: uuid("id").defaultRandom().primaryKey(),
  identifier: text("identifier").notNull(),
  value: text("value").notNull(),
  expiresAt: timestamp("expires_at", { withTimezone: true }).notNull(),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

// ==================== Household (Organization) Tables ====================

export const households = pgTable("households", {
  id: uuid("id").defaultRandom().primaryKey(),
  name: varchar("name", { length: 255 }).notNull(),
  createdBy: uuid("created_by").references(() => users.id),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
});

export const householdMembers = pgTable(
  "household_members",
  {
    householdId: uuid("household_id")
      .references(() => households.id, { onDelete: "cascade" })
      .notNull(),
    userId: uuid("user_id")
      .references(() => users.id, { onDelete: "cascade" })
      .notNull(),
    role: varchar("role", { length: 20 }).default("member"), // owner, admin, member
    canEditRecipes: boolean("can_edit_recipes").default(true),
    canEditShoppingList: boolean("can_edit_shopping_list").default(true),
    canViewNutrition: boolean("can_view_nutrition").default(false),
    joinedAt: timestamp("joined_at", { withTimezone: true }).defaultNow(),
  },
  (t) => ({
    pk: primaryKey({ columns: [t.householdId, t.userId] }),
  })
);

// ==================== Food & Nutrition Tables ====================

export const foods = pgTable(
  "foods",
  {
    id: uuid("id").defaultRandom().primaryKey(),
    name: varchar("name", { length: 255 }).notNull(),
    brand: varchar("brand", { length: 255 }),
    barcode: varchar("barcode", { length: 100 }),
    servingSize: decimal("serving_size", { precision: 10, scale: 2 }).default("100"),
    servingUnit: varchar("serving_unit", { length: 50 }).default("g"),
    caloriesPer100g: integer("calories_per_100g").notNull(),
    proteinPer100g: decimal("protein_per_100g", { precision: 8, scale: 2 }).notNull(),
    carbsPer100g: decimal("carbs_per_100g", { precision: 8, scale: 2 }).notNull(),
    fatPer100g: decimal("fat_per_100g", { precision: 8, scale: 2 }).notNull(),
    fiberPer100g: decimal("fiber_per_100g", { precision: 8, scale: 2 }),
    sugarPer100g: decimal("sugar_per_100g", { precision: 8, scale: 2 }),
    sodiumPer100g: decimal("sodium_per_100g", { precision: 8, scale: 2 }),
    isVerified: boolean("is_verified").default(false),
    createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
    updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
  },
  (t) => ({
    barcodeIdx: index("idx_foods_barcode").on(t.barcode),
  })
);

export const recipes = pgTable("recipes", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id").references(() => users.id, { onDelete: "set null" }),
  householdId: uuid("household_id").references(() => households.id, {
    onDelete: "set null",
  }),
  title: varchar("title", { length: 255 }).notNull(),
  description: text("description"),
  servings: integer("servings").default(1),
  prepTimeMinutes: integer("prep_time_minutes"),
  cookTimeMinutes: integer("cook_time_minutes"),
  sourceUrl: text("source_url"),
  imageUrl: text("image_url"),
  isPublic: boolean("is_public").default(false),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

export const recipeIngredients = pgTable("recipe_ingredients", {
  id: uuid("id").defaultRandom().primaryKey(),
  recipeId: uuid("recipe_id")
    .references(() => recipes.id, { onDelete: "cascade" })
    .notNull(),
  foodId: uuid("food_id").references(() => foods.id, { onDelete: "set null" }),
  ingredientText: varchar("ingredient_text", { length: 255 }).notNull(),
  amount: decimal("amount", { precision: 10, scale: 2 }),
  unit: varchar("unit", { length: 50 }),
  sortOrder: integer("sort_order").default(0),
});

export const recipeInstructions = pgTable("recipe_instructions", {
  id: uuid("id").defaultRandom().primaryKey(),
  recipeId: uuid("recipe_id")
    .references(() => recipes.id, { onDelete: "cascade" })
    .notNull(),
  stepNumber: integer("step_number").notNull(),
  instruction: text("instruction").notNull(),
});

export const recipeNutrition = pgTable("recipe_nutrition", {
  recipeId: uuid("recipe_id")
    .references(() => recipes.id, { onDelete: "cascade" })
    .primaryKey(),
  caloriesPerServing: integer("calories_per_serving"),
  proteinGrams: decimal("protein_grams", { precision: 8, scale: 2 }),
  carbsGrams: decimal("carbs_grams", { precision: 8, scale: 2 }),
  fatGrams: decimal("fat_grams", { precision: 8, scale: 2 }),
  fiberGrams: decimal("fiber_grams", { precision: 8, scale: 2 }),
  sugarGrams: decimal("sugar_grams", { precision: 8, scale: 2 }),
  sodiumMg: decimal("sodium_mg", { precision: 8, scale: 2 }),
});

export const recipeTags = pgTable("recipe_tags", {
  id: uuid("id").defaultRandom().primaryKey(),
  recipeId: uuid("recipe_id")
    .references(() => recipes.id, { onDelete: "cascade" })
    .notNull(),
  tag: varchar("tag", { length: 50 }).notNull(),
});

// ==================== Food Diary Tables ====================

export const foodDiaryEntries = pgTable(
  "food_diary_entries",
  {
    id: uuid("id").defaultRandom().primaryKey(),
    userId: uuid("user_id")
      .references(() => users.id, { onDelete: "cascade" })
      .notNull(),
    foodId: uuid("food_id").references(() => foods.id, { onDelete: "set null" }),
    recipeId: uuid("recipe_id").references(() => recipes.id, {
      onDelete: "set null",
    }),
    entryDate: date("entry_date").notNull(),
    mealType: varchar("meal_type", { length: 20 }), // breakfast, lunch, dinner, snack
    servings: decimal("servings", { precision: 6, scale: 2 }).default("1"),
    calories: integer("calories"),
    proteinGrams: decimal("protein_grams", { precision: 8, scale: 2 }),
    carbsGrams: decimal("carbs_grams", { precision: 8, scale: 2 }),
    fatGrams: decimal("fat_grams", { precision: 8, scale: 2 }),
    loggedAt: timestamp("logged_at", { withTimezone: true }).defaultNow(),
  },
  (t) => ({
    userDateIdx: index("idx_food_diary_user_date").on(t.userId, t.entryDate),
  })
);

// ==================== Meal Planning Tables ====================

export const mealPlans = pgTable("meal_plans", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  householdId: uuid("household_id").references(() => households.id, {
    onDelete: "set null",
  }),
  name: varchar("name", { length: 255 }),
  startDate: date("start_date").notNull(),
  endDate: date("end_date").notNull(),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

export const mealPlanRecipes = pgTable("meal_plan_recipes", {
  id: uuid("id").defaultRandom().primaryKey(),
  mealPlanId: uuid("meal_plan_id")
    .references(() => mealPlans.id, { onDelete: "cascade" })
    .notNull(),
  recipeId: uuid("recipe_id")
    .references(() => recipes.id, { onDelete: "cascade" })
    .notNull(),
  date: date("date").notNull(),
  mealType: varchar("meal_type", { length: 20 }),
  servings: decimal("servings", { precision: 6, scale: 2 }).default("1"),
});

// ==================== Shopping List Tables ====================

export const shoppingLists = pgTable("shopping_lists", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  householdId: uuid("household_id").references(() => households.id, {
    onDelete: "set null",
  }),
  name: varchar("name", { length: 255 }),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

export const shoppingListItems = pgTable("shopping_list_items", {
  id: uuid("id").defaultRandom().primaryKey(),
  shoppingListId: uuid("shopping_list_id")
    .references(() => shoppingLists.id, { onDelete: "cascade" })
    .notNull(),
  foodId: uuid("food_id").references(() => foods.id, { onDelete: "set null" }),
  itemName: varchar("item_name", { length: 255 }).notNull(),
  amount: decimal("amount", { precision: 10, scale: 2 }),
  unit: varchar("unit", { length: 50 }),
  category: varchar("category", { length: 100 }),
  isChecked: boolean("is_checked").default(false),
});

// ==================== User Goals Tables ====================

export const userGoals = pgTable("user_goals", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  goalType: varchar("goal_type", { length: 50 }), // weight_loss, muscle_gain, maintenance
  targetCalories: integer("target_calories"),
  targetProteinGrams: decimal("target_protein_grams", { precision: 8, scale: 2 }),
  targetCarbsGrams: decimal("target_carbs_grams", { precision: 8, scale: 2 }),
  targetFatGrams: decimal("target_fat_grams", { precision: 8, scale: 2 }),
  targetWeight: decimal("target_weight", { precision: 6, scale: 2 }),
  weightUnit: varchar("weight_unit", { length: 10 }).default("kg"),
  targetDate: date("target_date"),
  isActive: boolean("is_active").default(true),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

// ==================== Fitness/Workout Tables ====================

export const exercises = pgTable("exercises", {
  id: uuid("id").defaultRandom().primaryKey(),
  name: varchar("name", { length: 255 }).notNull(),
  description: text("description"),
  category: varchar("category", { length: 50 }).notNull(), // strength, cardio, flexibility
  muscleGroup: varchar("muscle_group", { length: 100 }),
  equipment: varchar("equipment", { length: 100 }),
  videoUrl: text("video_url"),
  imageUrl: text("image_url"),
  isCustom: boolean("is_custom").default(false),
  createdByUserId: uuid("created_by_user_id").references(() => users.id, {
    onDelete: "set null",
  }),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
});

export const workouts = pgTable("workouts", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  name: varchar("name", { length: 255 }),
  workoutDate: date("workout_date").notNull(),
  durationMinutes: integer("duration_minutes"),
  caloriesBurned: integer("calories_burned"),
  notes: text("notes"),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
});

export const workoutExercises = pgTable("workout_exercises", {
  id: uuid("id").defaultRandom().primaryKey(),
  workoutId: uuid("workout_id")
    .references(() => workouts.id, { onDelete: "cascade" })
    .notNull(),
  exerciseId: uuid("exercise_id")
    .references(() => exercises.id, { onDelete: "cascade" })
    .notNull(),
  sortOrder: integer("sort_order").default(0),
});

export const exerciseSets = pgTable("exercise_sets", {
  id: uuid("id").defaultRandom().primaryKey(),
  workoutExerciseId: uuid("workout_exercise_id")
    .references(() => workoutExercises.id, { onDelete: "cascade" })
    .notNull(),
  setNumber: integer("set_number").notNull(),
  reps: integer("reps"),
  weightKg: decimal("weight_kg", { precision: 6, scale: 2 }),
  durationSeconds: integer("duration_seconds"),
  distanceMeters: decimal("distance_meters", { precision: 10, scale: 2 }),
  completed: boolean("completed").default(false),
});

// ==================== Body Measurement Tables ====================

export const bodyMeasurements = pgTable("body_measurements", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  measurementDate: date("measurement_date").notNull(),
  weightKg: decimal("weight_kg", { precision: 6, scale: 2 }),
  bodyFatPercentage: decimal("body_fat_percentage", { precision: 5, scale: 2 }),
  muscleMassKg: decimal("muscle_mass_kg", { precision: 6, scale: 2 }),
  waistCm: decimal("waist_cm", { precision: 6, scale: 2 }),
  hipsCm: decimal("hips_cm", { precision: 6, scale: 2 }),
  chestCm: decimal("chest_cm", { precision: 6, scale: 2 }),
  armsCm: decimal("arms_cm", { precision: 6, scale: 2 }),
  thighsCm: decimal("thighs_cm", { precision: 6, scale: 2 }),
  notes: text("notes"),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
});

// ==================== Trainer/Client Tables ====================

export const trainerClientRelationships = pgTable(
  "trainer_client_relationships",
  {
    id: uuid("id").defaultRandom().primaryKey(),
    trainerId: uuid("trainer_id")
      .references(() => users.id, { onDelete: "cascade" })
      .notNull(),
    clientId: uuid("client_id")
      .references(() => users.id, { onDelete: "cascade" })
      .notNull(),
    status: varchar("status", { length: 20 }).default("pending"), // pending, active, paused, ended
    canViewNutrition: boolean("can_view_nutrition").default(true),
    canViewWorkouts: boolean("can_view_workouts").default(true),
    canViewMeasurements: boolean("can_view_measurements").default(false),
    canMessage: boolean("can_message").default(true),
    startedAt: timestamp("started_at", { withTimezone: true }),
    endedAt: timestamp("ended_at", { withTimezone: true }),
    createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  },
  (t) => ({
    uniqueRelationship: unique().on(t.trainerId, t.clientId),
  })
);

export const chatConversations = pgTable("chat_conversations", {
  id: uuid("id").defaultRandom().primaryKey(),
  trainerClientRelationshipId: uuid("trainer_client_relationship_id")
    .references(() => trainerClientRelationships.id, { onDelete: "cascade" })
    .notNull(),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

export const chatMessages = pgTable("chat_messages", {
  id: uuid("id").defaultRandom().primaryKey(),
  conversationId: uuid("conversation_id")
    .references(() => chatConversations.id, { onDelete: "cascade" })
    .notNull(),
  senderId: uuid("sender_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  content: text("content").notNull(),
  messageType: varchar("message_type", { length: 20 }).default("text"), // text, image, workout_share, recipe_share
  sentAt: timestamp("sent_at", { withTimezone: true }).defaultNow(),
  readAt: timestamp("read_at", { withTimezone: true }),
});

// ==================== Gamification Tables ====================

export const achievements = pgTable("achievements", {
  id: uuid("id").defaultRandom().primaryKey(),
  name: varchar("name", { length: 100 }).notNull(),
  description: text("description"),
  iconUrl: text("icon_url"),
  points: integer("points").default(0),
  category: varchar("category", { length: 50 }), // nutrition, workout, streak, social
});

export const userAchievements = pgTable(
  "user_achievements",
  {
    userId: uuid("user_id")
      .references(() => users.id, { onDelete: "cascade" })
      .notNull(),
    achievementId: uuid("achievement_id")
      .references(() => achievements.id, { onDelete: "cascade" })
      .notNull(),
    earnedAt: timestamp("earned_at", { withTimezone: true }).defaultNow(),
  },
  (t) => ({
    pk: primaryKey({ columns: [t.userId, t.achievementId] }),
  })
);

export const streaks = pgTable("streaks", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  streakType: varchar("streak_type", { length: 50 }).notNull(), // logging, workout, calorie_goal
  currentCount: integer("current_count").default(0),
  longestCount: integer("longest_count").default(0),
  lastActivityDate: date("last_activity_date"),
});

// ==================== AI Chat Thread Tables ====================

export const aiChatThreads = pgTable("ai_chat_threads", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id")
    .references(() => users.id, { onDelete: "cascade" })
    .notNull(),
  threadType: varchar("thread_type", { length: 50 }).default("nutrition"), // nutrition, workout, general
  threadData: jsonb("thread_data").default("{}"),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});
