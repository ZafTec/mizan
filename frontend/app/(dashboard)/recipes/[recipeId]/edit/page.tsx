import { redirect } from "next/navigation";
export default async function EditRecipePage({
  params,
}: {
  params: Promise<{ recipeId: string }>;
}) {
  const { recipeId } = await params;
  redirect(`/recipes/${encodeURIComponent(recipeId)}#ingredients`);
}
