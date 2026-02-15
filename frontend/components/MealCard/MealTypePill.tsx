
import { MealType } from '@/types/meal';

interface MealTypePillProps {
    mealType: MealType;
}

export default function MealTypePill({ mealType }: MealTypePillProps) {
    const pillClasses = {
        Meal: 'bg-green-200 dark:bg-green-900/30',
        Snack: 'bg-blue-200 dark:bg-blue-900/30',
        Drink: 'bg-yellow-200 dark:bg-yellow-900/30',
    };

    return (
        <span className={`text-sm text-gray-500 dark:text-gray-400 px-2 py-1 rounded-full ${pillClasses[mealType]}`}>
            {mealType}
        </span>
    );
}

