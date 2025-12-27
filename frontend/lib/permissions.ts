import { createAccessControl } from "better-auth/plugins/access";
import { adminAc } from "better-auth/plugins/admin/access";

/**
 * Permission System for MacroChef
 *
 * Defines resources and actions for fine-grained access control.
 * Used by Better Auth access plugin to enforce permissions beyond simple roles.
 */

// Define all resources and their possible actions
export const statement = {
  // User management (admin only)
  user: ["create", "read", "update", "delete", "ban", "impersonate"],

  // Recipe management
  recipe: ["create", "read", "update", "delete"],

  // Meal plan management
  mealPlan: ["create", "read", "update", "delete"],

  // Workout management
  workout: ["create", "read", "update", "delete", "assign"],

  // Household management
  household: ["create", "read", "update", "delete", "invite"],

  // Trainer-client relationship management
  trainerClient: ["create", "read", "update", "delete", "message"],

  // Client data access (trainer permissions)
  clientData: ["viewNutrition", "viewWorkouts", "viewMeasurements"],
} as const;

// Create access controller
export const ac = createAccessControl(statement);

/**
 * User Role - Regular users
 * Can manage their own recipes, meal plans, workouts, and households
 */
export const userRole = ac.newRole({
  user: ["read", "update"], // Can only read/update own profile
  recipe: ["create", "read", "update", "delete"],
  mealPlan: ["create", "read", "update", "delete"],
  workout: ["create", "read", "update", "delete"],
  household: ["create", "read", "update", "invite"],
});

/**
 * Trainer Role - Fitness professionals
 * Can manage trainer-client relationships and view client data with consent
 */
export const trainerRole = ac.newRole({
  user: ["read"], // Can read client profiles (limited)
  recipe: ["create", "read", "update", "delete"],
  mealPlan: ["create", "read", "update", "delete"],
  workout: ["create", "read", "update", "delete", "assign"], // Can assign workouts to clients
  household: ["create", "read", "update", "invite"],
  trainerClient: ["create", "read", "update", "delete", "message"],
  clientData: ["viewNutrition", "viewWorkouts", "viewMeasurements"],
});

/**
 * Admin Role - System administrators
 * Full access to all resources
 */
export const adminRole = ac.newRole({
  ...adminAc.statements, // Include all default admin permissions
  user: ["create", "read", "update", "delete", "ban", "impersonate"],
  recipe: ["create", "read", "update", "delete"],
  mealPlan: ["create", "read", "update", "delete"],
  workout: ["create", "read", "update", "delete", "assign"],
  household: ["create", "read", "update", "delete", "invite"],
  trainerClient: ["create", "read", "update", "delete", "message"],
  clientData: ["viewNutrition", "viewWorkouts", "viewMeasurements"],
});
