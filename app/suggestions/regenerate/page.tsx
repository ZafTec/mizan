import { regenerateSuggestions } from '@/data/suggestion';
import { getUserServer } from '@/helper/session';
import { redirect } from 'next/navigation';

export default async function RegeneratePage() {
  const user = await getUserServer();
  
  // Only admins can regenerate suggestions
  if (!user.isAdmin) {
    return redirect('/suggestions');
  }
  
  // Regenerate the suggestions
  await regenerateSuggestions(user);
  
  // Redirect back to the suggestions page
  return redirect('/suggestions');
}
