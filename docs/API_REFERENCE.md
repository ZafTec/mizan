# MacroChef API Reference

**Version:** 1.0
**Base URL:** `http://localhost:5000` (development) | `https://yourdomain.com` (production)
**Authentication:** JWT Bearer tokens via BetterAuth

---

## Table of Contents

- [Authentication](#authentication)
- [Users](#users)
- [Foods](#foods)
- [Recipes](#recipes)
- [Meal Plans](#meal-plans)
- [Shopping Lists](#shopping-lists)
- [Workouts](#workouts)
- [Body Measurements](#body-measurements)
- [Households](#households)
- [Trainers](#trainers)
- [Chat](#chat)
- [Admin](#admin)
- [Error Codes](#error-codes)

---

## Authentication

### Sign Up

**Endpoint:** `POST /api/auth/sign-up`
**Authorization:** None (public)

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "name": "John Doe"
}
```

**Response (200 OK):**
```json
{
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "name": "John Doe",
    "role": "user",
    "createdAt": "2025-12-26T10:00:00Z"
  },
  "session": {
    "token": "eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresAt": "2025-12-27T10:00:00Z"
  }
}
```

---

### Sign In

**Endpoint:** `POST /api/auth/sign-in`
**Authorization:** None (public)

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response (200 OK):**
```json
{
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "name": "John Doe",
    "role": "user"
  },
  "session": {
    "token": "eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresAt": "2025-12-27T10:00:00Z"
  }
}
```

**Error (401 Unauthorized):**
```json
{
  "error": "Invalid email or password"
}
```

---

### Sign Out

**Endpoint:** `POST /api/auth/sign-out`
**Authorization:** Required (JWT)

**Response (200 OK):**
```json
{
  "success": true
}
```

---

### Get Session

**Endpoint:** `GET /api/auth/session`
**Authorization:** Required (JWT)

**Response (200 OK):**
```json
{
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "name": "John Doe",
    "role": "user"
  },
  "session": {
    "expiresAt": "2025-12-27T10:00:00Z"
  }
}
```

---

## Users

### Get Current User

**Endpoint:** `GET /api/Users/me`
**Authorization:** Required (JWT)

**Response (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "name": "John Doe",
  "role": "user",
  "emailVerified": true,
  "createdAt": "2025-01-01T00:00:00Z"
}
```

---

### Update Current User

**Endpoint:** `PUT /api/Users/me`
**Authorization:** Required (JWT)

**Request:**
```json
{
  "name": "John Smith",
  "image": "https://example.com/avatar.jpg"
}
```

**Response (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "name": "John Smith",
  "image": "https://example.com/avatar.jpg",
  "role": "user"
}
```

---

## Foods

### Search Foods

**Endpoint:** `GET /api/Foods/search`
**Authorization:** None (public)

**Query Parameters:**
- `searchTerm` (optional) - Text search in food name
- `barcode` (optional) - Barcode lookup
- `limit` (optional, default: 20) - Results limit

**Example:**
```
GET /api/Foods/search?searchTerm=chicken&limit=10
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "650e8400-e29b-41d4-a716-446655440001",
      "name": "Chicken Breast, Raw",
      "barcode": null,
      "servingSize": 100,
      "servingUnit": "g",
      "calories": 165,
      "protein": 31,
      "carbohydrates": 0,
      "fat": 3.6,
      "fiber": 0,
      "sugar": 0
    }
  ],
  "total": 1
}
```

---

### Get Food by ID

**Endpoint:** `GET /api/Foods/{id}`
**Authorization:** None (public)

**Response (200 OK):**
```json
{
  "id": "650e8400-e29b-41d4-a716-446655440001",
  "name": "Chicken Breast, Raw",
  "barcode": null,
  "servingSize": 100,
  "servingUnit": "g",
  "calories": 165,
  "protein": 31,
  "carbohydrates": 0,
  "fat": 3.6,
  "fiber": 0,
  "sugar": 0
}
```

---

### Create Food (Admin Only)

**Endpoint:** `POST /api/Foods`
**Authorization:** Required (JWT, admin role)

**Request:**
```json
{
  "name": "Quinoa, Cooked",
  "barcode": "1234567890123",
  "servingSize": 100,
  "servingUnit": "g",
  "calories": 120,
  "protein": 4.4,
  "carbohydrates": 21.3,
  "fat": 1.92,
  "fiber": 2.8,
  "sugar": 0.9
}
```

**Response (201 Created):**
```json
{
  "id": "750e8400-e29b-41d4-a716-446655440002",
  "name": "Quinoa, Cooked",
  "barcode": "1234567890123",
  "servingSize": 100,
  "servingUnit": "g",
  "calories": 120,
  "protein": 4.4,
  "carbohydrates": 21.3,
  "fat": 1.92,
  "fiber": 2.8,
  "sugar": 0.9
}
```

---

## Recipes

### List Recipes

**Endpoint:** `GET /api/Recipes`
**Authorization:** Optional (JWT for private recipes)

**Query Parameters:**
- `includePublic` (optional, default: false) - Include public recipes
- `userId` (optional) - Filter by user ID

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "850e8400-e29b-41d4-a716-446655440003",
      "name": "High-Protein Chicken Salad",
      "description": "Light and nutritious meal",
      "servings": 2,
      "prepTime": 15,
      "cookTime": 0,
      "isPublic": false,
      "userId": "550e8400-e29b-41d4-a716-446655440000",
      "createdAt": "2025-12-20T10:00:00Z",
      "ingredients": [
        {
          "foodId": "650e8400-e29b-41d4-a716-446655440001",
          "foodName": "Chicken Breast, Raw",
          "quantity": 200,
          "unit": "g"
        }
      ],
      "nutrition": {
        "calories": 330,
        "protein": 62,
        "carbohydrates": 0,
        "fat": 7.2
      }
    }
  ],
  "total": 1
}
```

---

### Get Recipe by ID

**Endpoint:** `GET /api/Recipes/{id}`
**Authorization:** Optional (required for private recipes)

**Response (200 OK):**
```json
{
  "id": "850e8400-e29b-41d4-a716-446655440003",
  "name": "High-Protein Chicken Salad",
  "description": "Light and nutritious meal",
  "servings": 2,
  "prepTime": 15,
  "cookTime": 0,
  "instructions": "1. Cook chicken\n2. Dice and mix with greens\n3. Serve",
  "isPublic": false,
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "ingredients": [
    {
      "foodId": "650e8400-e29b-41d4-a716-446655440001",
      "foodName": "Chicken Breast, Raw",
      "quantity": 200,
      "unit": "g"
    }
  ],
  "nutrition": {
    "calories": 330,
    "protein": 62,
    "carbohydrates": 0,
    "fat": 7.2
  }
}
```

---

### Create Recipe

**Endpoint:** `POST /api/Recipes`
**Authorization:** Required (JWT)

**Request:**
```json
{
  "name": "High-Protein Chicken Salad",
  "description": "Light and nutritious meal",
  "servings": 2,
  "prepTime": 15,
  "cookTime": 0,
  "instructions": "1. Cook chicken\n2. Dice and mix with greens\n3. Serve",
  "isPublic": false,
  "ingredients": [
    {
      "foodId": "650e8400-e29b-41d4-a716-446655440001",
      "quantity": 200,
      "unit": "g"
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "id": "850e8400-e29b-41d4-a716-446655440003",
  "name": "High-Protein Chicken Salad",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2025-12-26T10:00:00Z"
}
```

---

## Meal Plans

### List Meal Plans

**Endpoint:** `GET /api/MealPlans`
**Authorization:** Required (JWT)

**Query Parameters:**
- `startDate` (optional) - Filter by start date
- `endDate` (optional) - Filter by end date

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "950e8400-e29b-41d4-a716-446655440004",
      "name": "Week 1 - Bulking",
      "startDate": "2025-12-23",
      "endDate": "2025-12-29",
      "userId": "550e8400-e29b-41d4-a716-446655440000",
      "recipes": [
        {
          "recipeId": "850e8400-e29b-41d4-a716-446655440003",
          "recipeName": "High-Protein Chicken Salad",
          "dayOfWeek": 1,
          "mealType": "lunch"
        }
      ]
    }
  ],
  "total": 1
}
```

---

### Create Meal Plan

**Endpoint:** `POST /api/MealPlans`
**Authorization:** Required (JWT)

**Request:**
```json
{
  "name": "Week 1 - Bulking",
  "startDate": "2025-12-23",
  "endDate": "2025-12-29"
}
```

**Response (201 Created):**
```json
{
  "id": "950e8400-e29b-41d4-a716-446655440004",
  "name": "Week 1 - Bulking",
  "startDate": "2025-12-23",
  "endDate": "2025-12-29",
  "userId": "550e8400-e29b-41d4-a716-446655440000"
}
```

---

### Add Recipe to Meal Plan

**Endpoint:** `POST /api/MealPlans/{id}/recipes`
**Authorization:** Required (JWT, owner only)

**Request:**
```json
{
  "recipeId": "850e8400-e29b-41d4-a716-446655440003",
  "dayOfWeek": 1,
  "mealType": "lunch"
}
```

**Response (200 OK):**
```json
{
  "success": true
}
```

---

## Shopping Lists

### List Shopping Lists

**Endpoint:** `GET /api/ShoppingLists`
**Authorization:** Required (JWT)

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "a50e8400-e29b-41d4-a716-446655440005",
      "name": "Weekly Groceries",
      "userId": "550e8400-e29b-41d4-a716-446655440000",
      "createdAt": "2025-12-26T10:00:00Z",
      "items": [
        {
          "id": "b50e8400-e29b-41d4-a716-446655440006",
          "name": "Chicken Breast",
          "quantity": 2,
          "unit": "kg",
          "isChecked": false
        }
      ]
    }
  ],
  "total": 1
}
```

---

### Create Shopping List

**Endpoint:** `POST /api/ShoppingLists`
**Authorization:** Required (JWT)

**Request:**
```json
{
  "name": "Weekly Groceries"
}
```

**Response (201 Created):**
```json
{
  "id": "a50e8400-e29b-41d4-a716-446655440005",
  "name": "Weekly Groceries",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2025-12-26T10:00:00Z",
  "items": []
}
```

---

### Add Shopping List Item

**Endpoint:** `POST /api/ShoppingLists/{id}/items`
**Authorization:** Required (JWT, owner or household member)

**Request:**
```json
{
  "name": "Chicken Breast",
  "quantity": 2,
  "unit": "kg"
}
```

**Response (200 OK):**
```json
{
  "id": "b50e8400-e29b-41d4-a716-446655440006",
  "name": "Chicken Breast",
  "quantity": 2,
  "unit": "kg",
  "isChecked": false
}
```

---

### Toggle Shopping List Item

**Endpoint:** `PATCH /api/ShoppingLists/items/{itemId}/toggle`
**Authorization:** Required (JWT, owner or household member)

**Response (200 OK):**
```json
{
  "id": "b50e8400-e29b-41d4-a716-446655440006",
  "isChecked": true
}
```

---

## Workouts

### List Workouts

**Endpoint:** `GET /api/Workouts`
**Authorization:** Required (JWT)

**Query Parameters:**
- `startDate` (optional) - Filter by date range
- `endDate` (optional) - Filter by date range

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "c50e8400-e29b-41d4-a716-446655440007",
      "name": "Upper Body Day",
      "date": "2025-12-26",
      "duration": 60,
      "notes": "Felt strong today",
      "userId": "550e8400-e29b-41d4-a716-446655440000",
      "exercises": [
        {
          "exerciseName": "Bench Press",
          "sets": 4,
          "reps": 8,
          "weight": 80,
          "unit": "kg"
        }
      ]
    }
  ],
  "total": 1
}
```

---

### Log Workout

**Endpoint:** `POST /api/Workouts`
**Authorization:** Required (JWT)

**Request:**
```json
{
  "name": "Upper Body Day",
  "date": "2025-12-26",
  "duration": 60,
  "notes": "Felt strong today",
  "exercises": [
    {
      "exerciseId": "d50e8400-e29b-41d4-a716-446655440008",
      "sets": 4,
      "reps": 8,
      "weight": 80,
      "unit": "kg"
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "id": "c50e8400-e29b-41d4-a716-446655440007",
  "name": "Upper Body Day",
  "date": "2025-12-26",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2025-12-26T10:00:00Z"
}
```

---

## Body Measurements

### List Body Measurements

**Endpoint:** `GET /api/BodyMeasurements`
**Authorization:** Required (JWT)

**Query Parameters:**
- `startDate` (optional) - Filter by date range
- `endDate` (optional) - Filter by date range

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "e50e8400-e29b-41d4-a716-446655440009",
      "date": "2025-12-26",
      "weight": 80.5,
      "bodyFatPercentage": 15.2,
      "muscleMass": 68.2,
      "userId": "550e8400-e29b-41d4-a716-446655440000"
    }
  ],
  "total": 1
}
```

---

### Log Body Measurement

**Endpoint:** `POST /api/BodyMeasurements`
**Authorization:** Required (JWT)

**Request:**
```json
{
  "date": "2025-12-26",
  "weight": 80.5,
  "bodyFatPercentage": 15.2,
  "muscleMass": 68.2
}
```

**Response (201 Created):**
```json
{
  "id": "e50e8400-e29b-41d4-a716-446655440009",
  "date": "2025-12-26",
  "weight": 80.5,
  "bodyFatPercentage": 15.2,
  "muscleMass": 68.2,
  "userId": "550e8400-e29b-41d4-a716-446655440000"
}
```

---

## Households

### Get Household

**Endpoint:** `GET /api/Households/{id}`
**Authorization:** Required (JWT, member only)

**Response (200 OK):**
```json
{
  "id": "f50e8400-e29b-41d4-a716-446655440010",
  "name": "Smith Family",
  "members": [
    {
      "userId": "550e8400-e29b-41d4-a716-446655440000",
      "userName": "John Doe",
      "userEmail": "john@example.com",
      "role": "admin",
      "canEditRecipes": true,
      "canEditShoppingList": true,
      "canViewNutrition": true,
      "joinedAt": "2025-12-01T00:00:00Z"
    }
  ]
}
```

---

### Create Household

**Endpoint:** `POST /api/Households`
**Authorization:** Required (JWT)

**Request:**
```json
{
  "name": "Smith Family"
}
```

**Response (201 Created):**
```json
{
  "id": "f50e8400-e29b-41d4-a716-446655440010",
  "name": "Smith Family",
  "createdAt": "2025-12-26T10:00:00Z"
}
```

---

### Add Household Member

**Endpoint:** `POST /api/Households/{id}/members`
**Authorization:** Required (JWT, household admin only)

**Request:**
```json
{
  "userEmail": "jane@example.com",
  "role": "member",
  "canEditRecipes": true,
  "canEditShoppingList": true,
  "canViewNutrition": false
}
```

**Response (200 OK):**
```json
{
  "userId": "g50e8400-e29b-41d4-a716-446655440011",
  "role": "member",
  "canEditRecipes": true,
  "canEditShoppingList": true,
  "canViewNutrition": false
}
```

---

## Trainers

### List Trainer's Clients

**Endpoint:** `GET /api/Trainers/clients`
**Authorization:** Required (JWT, trainer or admin role)

**Response (200 OK):**
```json
{
  "items": [
    {
      "clientId": "550e8400-e29b-41d4-a716-446655440000",
      "clientName": "John Doe",
      "clientEmail": "john@example.com",
      "status": "active",
      "canViewNutrition": true,
      "canViewWorkouts": true,
      "canViewMeasurements": false,
      "canMessage": true,
      "startedAt": "2025-12-01T00:00:00Z"
    }
  ],
  "total": 1
}
```

---

### List Pending Client Requests

**Endpoint:** `GET /api/Trainers/requests`
**Authorization:** Required (JWT, trainer or admin role)

**Response (200 OK):**
```json
{
  "items": [
    {
      "clientId": "h50e8400-e29b-41d4-a716-446655440012",
      "clientName": "Jane Smith",
      "clientEmail": "jane@example.com",
      "status": "pending",
      "requestedAt": "2025-12-25T10:00:00Z"
    }
  ],
  "total": 1
}
```

---

### Get Client Nutrition Data

**Endpoint:** `GET /api/Trainers/clients/{clientId}/nutrition`
**Authorization:** Required (JWT, trainer with permission)

**Query Parameters:**
- `date` (required) - Date in format YYYY-MM-DD

**Example:**
```
GET /api/Trainers/clients/550e8400-e29b-41d4-a716-446655440000/nutrition?date=2025-12-26
```

**Response (200 OK):**
```json
{
  "date": "2025-12-26",
  "clientId": "550e8400-e29b-41d4-a716-446655440000",
  "entries": [
    {
      "foodId": "650e8400-e29b-41d4-a716-446655440001",
      "foodName": "Chicken Breast, Raw",
      "quantity": 200,
      "mealType": "lunch",
      "calories": 330,
      "protein": 62,
      "carbohydrates": 0,
      "fat": 7.2
    }
  ],
  "summary": {
    "totalCalories": 330,
    "totalProtein": 62,
    "totalCarbohydrates": 0,
    "totalFat": 7.2
  }
}
```

**Error (401 Unauthorized):**
```json
{
  "error": "No active relationship with this client"
}
```

**Error (403 Forbidden):**
```json
{
  "error": "No permission to view client nutrition data"
}
```

---

## Chat

### Get Conversation

**Endpoint:** `GET /api/Chat/{relationshipId}`
**Authorization:** Required (JWT, participant only)

**Response (200 OK):**
```json
{
  "conversationId": "i50e8400-e29b-41d4-a716-446655440013",
  "relationshipId": "j50e8400-e29b-41d4-a716-446655440014",
  "messages": [
    {
      "id": "k50e8400-e29b-41d4-a716-446655440015",
      "senderId": "550e8400-e29b-41d4-a716-446655440000",
      "senderName": "John Doe",
      "content": "How should I structure my workouts?",
      "messageType": "text",
      "sentAt": "2025-12-26T10:00:00Z"
    }
  ]
}
```

---

### Send Message

**Endpoint:** `POST /api/Chat/send`
**Authorization:** Required (JWT, participant only)

**Request:**
```json
{
  "conversationId": "i50e8400-e29b-41d4-a716-446655440013",
  "content": "How should I structure my workouts?",
  "messageType": "text"
}
```

**Response (201 Created):**
```json
{
  "id": "k50e8400-e29b-41d4-a716-446655440015",
  "sentAt": "2025-12-26T10:00:00Z"
}
```

---

## Admin

All admin endpoints require `admin` role.

### List Users

**Endpoint:** `POST /api/admin/list-users`
**Authorization:** Required (JWT, admin role)

**Request:**
```json
{
  "searchValue": "john",
  "searchField": "email",
  "searchOperator": "contains",
  "limit": 50,
  "offset": 0,
  "sortBy": "createdAt",
  "sortDirection": "desc"
}
```

**Response (200 OK):**
```json
{
  "users": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "email": "john@example.com",
      "name": "John Doe",
      "role": "user",
      "emailVerified": true,
      "banned": false,
      "createdAt": "2025-01-01T00:00:00Z"
    }
  ],
  "total": 1,
  "limit": 50,
  "offset": 0
}
```

---

### Create User

**Endpoint:** `POST /api/admin/create-user`
**Authorization:** Required (JWT, admin role)

**Request:**
```json
{
  "email": "newuser@example.com",
  "password": "SecurePassword123!",
  "name": "New User",
  "role": "user"
}
```

**Response (201 Created):**
```json
{
  "id": "l50e8400-e29b-41d4-a716-446655440016",
  "email": "newuser@example.com",
  "name": "New User",
  "role": "user",
  "createdAt": "2025-12-26T10:00:00Z"
}
```

---

### Set User Role

**Endpoint:** `POST /api/admin/set-role`
**Authorization:** Required (JWT, admin role)

**Request:**
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "role": "trainer"
}
```

**Response (200 OK):**
```json
{
  "success": true
}
```

---

### Ban User

**Endpoint:** `POST /api/admin/ban-user`
**Authorization:** Required (JWT, admin role)

**Request:**
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "banReason": "Violation of terms of service",
  "banExpiresIn": 604800
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "banExpiresAt": "2026-01-02T10:00:00Z"
}
```

---

### Unban User

**Endpoint:** `POST /api/admin/unban-user`
**Authorization:** Required (JWT, admin role)

**Request:**
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Response (200 OK):**
```json
{
  "success": true
}
```

---

### Impersonate User

**Endpoint:** `POST /api/admin/impersonate-user`
**Authorization:** Required (JWT, admin role)

**Request:**
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Response (200 OK):**
```json
{
  "session": {
    "token": "eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresAt": "2025-12-27T10:00:00Z",
    "impersonatedBy": "admin-user-id"
  }
}
```

---

### Stop Impersonating

**Endpoint:** `POST /api/admin/stop-impersonating`
**Authorization:** Required (JWT, admin role)

**Response (200 OK):**
```json
{
  "success": true
}
```

---

## Error Codes

| Status Code | Error Type | Description |
|-------------|-----------|-------------|
| 400 | Bad Request | Invalid request body or parameters |
| 401 | Unauthorized | Missing or invalid JWT token |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource not found |
| 409 | Conflict | Resource already exists (e.g., duplicate email) |
| 422 | Unprocessable Entity | Validation failed |
| 500 | Internal Server Error | Server-side error |

**Error Response Format:**
```json
{
  "error": "Error message describing the issue",
  "details": {
    "field": "Specific validation error"
  }
}
```

**Common Error Messages:**

- `"User must be authenticated"` - Missing or expired JWT
- `"No active relationship with this client"` - Trainer-client relationship not found or inactive
- `"No permission to view client nutrition data"` - Permission flag not enabled
- `"User is not a member of this household"` - Attempting to access household without membership

---

## Rate Limiting

Currently, no rate limiting is implemented. This will be added in future versions.

**Planned Limits:**
- Authentication endpoints: 10 requests/minute
- API endpoints: 100 requests/minute
- Admin endpoints: 50 requests/minute

---

## Pagination

List endpoints support pagination via query parameters:

- `limit` - Number of items per page (default: 20, max: 100)
- `offset` - Number of items to skip (default: 0)

**Example:**
```
GET /api/Recipes?limit=10&offset=20
```

**Response includes pagination metadata:**
```json
{
  "items": [...],
  "total": 50,
  "limit": 10,
  "offset": 20
}
```

---

## Versioning

API versioning is currently not implemented. All endpoints are on v1 (implicit).

Future versions will use URL path versioning:
- `/api/v1/...`
- `/api/v2/...`

---

## WebSocket Connections (SignalR)

### Chat Hub

**Endpoint:** `ws://localhost:5000/hubs/chat` (development)
**Authorization:** Required (JWT in query string)

**Connection:**
```typescript
import { HubConnectionBuilder } from "@microsoft/signalr";

const connection = new HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/chat", {
    accessTokenFactory: () => getJwtToken()
  })
  .build();

await connection.start();
```

**Join Conversation:**
```typescript
await connection.invoke("JoinConversation", conversationId);
```

**Send Message:**
```typescript
await connection.invoke("SendMessage", {
  conversationId: "...",
  content: "Hello!",
  messageType: "text"
});
```

**Receive Messages:**
```typescript
connection.on("ReceiveMessage", (message) => {
  console.log(message);
});
```

---

## Additional Resources

- **Swagger UI:** `http://localhost:5000/swagger` (development only)
- **OpenAPI Spec:** `http://localhost:5000/swagger/v1/swagger.json`
- **Health Check:** `http://localhost:5000/health`
