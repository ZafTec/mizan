import { regenerateSuggestions } from '@/data/suggestion';
import { getUserServer } from '@/helper/session';
import { redirect } from 'next/navigation';

export const dynamic = 'force-dynamic';

export default async function RegeneratePage() {
  await getUserServer(); // Verify user is authenticated

  // Regenerate the suggestions
  await regenerateSuggestions();

  // Redirect back to the suggestions page
  return redirect('/suggestions');
}
