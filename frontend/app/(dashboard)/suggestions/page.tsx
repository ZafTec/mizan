import SuggestedRecipes from '@/components/SuggestedRecipes';
import { getTodaySuggestions } from '@/data/suggestion';
import { getUserServer } from '@/helper/session';

export const dynamic = 'force-dynamic';

export default async function Page() {
	const user = await getUserServer();
	const recipes = await getTodaySuggestions();
	return <SuggestedRecipes user={user} suggestions={recipes} />;
}
