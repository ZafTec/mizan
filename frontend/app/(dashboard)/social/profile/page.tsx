import SocialProfileManager from "./SocialProfileManager";

export default function SocialProfilePage() {
  return <div className="space-y-6"><header><p className="eyebrow">Social privacy</p><h1 className="mt-2 text-3xl font-semibold sm:text-4xl">Profile and followers</h1><p className="mt-2 text-sm text-charcoal-blue-500">Only people you approve can read published workouts.</p></header><SocialProfileManager /></div>;
}
