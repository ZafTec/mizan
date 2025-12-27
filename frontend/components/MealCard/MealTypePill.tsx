
import { MealType } from '@/types/meal';

interface MealTypePillProps {
    mealType: MealType;
}

export default function MealTypePill({ mealType }: MealTypePillProps) {
    const pillClasses = {
        Meal: 'bg-green-200',
        Snack: 'bg-blue-200',
        Drink: 'bg-yellow-200',
    };

    return (
        <span className={`text-sm text-gray-500 px-2 py-1 rounded-full ${pillClasses[mealType]}`}>
            {mealType}
        </span>
    );
}

