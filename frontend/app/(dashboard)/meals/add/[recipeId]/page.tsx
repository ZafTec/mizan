import { redirect } from "next/navigation";
export default async function LogRecipePage({
  params,
}: {
  params: Promise<{ recipeId: string }>;
}) {
  const { recipeId } = await params;
  redirect(`/recipes/${encodeURIComponent(recipeId)}`);
}
