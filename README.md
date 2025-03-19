# MacroChef - Meal Planning & Nutrition Tracking Application

MacroChef is a comprehensive web application for meal planning, recipe management, and nutrition tracking. It helps users plan their meals, organize shopping lists, and track their nutritional intake.

## Features

### Recipe Management
- Create and store recipes with detailed nutritional information
- Search and filter recipes by name and ingredients
- View complete nutritional breakdown of each recipe

### Meal Planning
- Plan meals for each day of the week with an interactive calendar
- Add recipes to specific meal times (breakfast, lunch, dinner, snack)
- Adjust serving sizes for each planned meal
- Edit or remove planned meals as needed

### Shopping List
- Automatically generate shopping lists based on your meal plan
- View ingredients grouped by category
- Check off items as you shop
- Print shopping lists for offline use

### Nutrition Tracking
- Log daily meals and track nutritional intake
- View nutritional summaries by day and week
- Track calories, protein, carbs, fat, and fiber

## Technical Features

- **Modern UI**: Built with Next.js, React, and Tailwind CSS
- **Responsive Design**: Works seamlessly on desktop and mobile devices
- **Server-Side Rendering**: Fast load times and SEO-friendly pages
- **Authentication**: Secure user accounts and data privacy
- **Data Persistence**: MongoDB integration for data storage
- **Real-time Updates**: Dynamic data loading when navigating between weeks

## Getting Started

### Prerequisites
- Node.js 18.x or higher
- Yarn package manager
- MongoDB instance (local or cloud)

### Installation

1. Clone the repository
   ```
   git clone https://github.com/yourusername/macro_chef.git
   cd macro_chef
   ```

2. Install dependencies
   ```
   yarn
   ```

3. Create a `.env.local` file with the following variables:
   ```
   MONGODB_URI=your_mongodb_connection_string
   NEXT_PUBLIC_BASE_URL=http://localhost:3000
   TOKEN_SECRET=your_jwt_secret_key
   ```

4. Run the development server
   ```
   yarn dev
   ```

5. Open your browser and navigate to `http://localhost:3000`

## Development Resources

### Authentication
- [Medium article](https://medium.com/@Rushabh_/next-js-demystified-user-authentication-with-nextjs-mongodb-2a0e1e697526)
- [Youtube video](https://www.youtube.com/watch?v=N_sUsq_y10U)

## License

This project is licensed under the MIT License.
