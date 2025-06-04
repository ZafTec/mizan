# MacroChef - Meal Planning, Nutrition & Fitness Tracking Application

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

### Workout Tracking
- Log and track workouts, exercises, and progress over time
- View workout history and performance analytics

### Body Composition Tracking
- Record and visualize body metrics (weight, body fat %, muscle mass, etc.)
- Track changes and trends with charts

### Gamification & Achievements
- Earn achievements for hitting nutrition and workout goals
- Set personal goals and milestones for both intake and exercise
- Unlock badges and rewards for consistency and progress

### Social Features
- Share your macro goals, achievements, and progress with friends or the community
- View and comment on others' achievements (privacy controls available)

### Trainer & Coaching
- Assign a trainer or coach to your account
- Grant trainers permission to view your progress, nutrition, and workouts
- In-app chat with trainers for advice and feedback
- Trainers can set goals, review progress, and provide personalized recommendations

## Technical Features

- **Modern UI**: Built with Next.js, React, and Tailwind CSS
- **Responsive Design**: Works seamlessly on desktop and mobile devices
- **Server-Side Rendering**: Fast load times and SEO-friendly pages
- **Authentication**: Secure user accounts and data privacy
- **Data Persistence**: MongoDB integration for data storage
- **Real-time Updates**: Dynamic data loading when navigating between weeks
- **Workout & Body Data**: Persistent storage and analytics for workouts and body composition
- **Gamification Engine**: Achievement and badge system for nutrition and fitness
- **Social & Sharing**: Share progress, goals, and achievements with others
- **Trainer Permissions**: Role-based access for trainers/coaches, with secure data sharing
- **In-app Chat**: Real-time messaging between users and trainers

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
