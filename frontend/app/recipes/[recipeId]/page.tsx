import Image from "next/image";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Plus } from "lucide-react";
import { getRecipeById } from "@/data/recipe";
import { getUserOptionalServer } from "@/helper/session";
import { LogEntryButton } from "@/components/logging/LogEntryButton";
import RecipeIngredients from "@/components/log/RecipeIngredients";
import RecipeActions from "./RecipeActions";

export async function generateMetadata({
  params,
}: {
  params: Promise<{ recipeId: string }>;
}) {
  const recipe = await getRecipeById((await params).recipeId);
  return { title: `${recipe?.title || "Recipe not found"} · Mizan` };
}

export default async function RecipePage({
  params,
}: {
  params: Promise<{ recipeId: string }>;
}) {
  const { recipeId } = await params;
  const [user, recipe] = await Promise.all([
    getUserOptionalServer(),
    getRecipeById(recipeId),
  ]);
  if (!recipe) notFound();
  const macros = [
    {
      name: "Calories",
      value: recipe.nutrition?.caloriesPerServing,
      unit: "kcal",
    },
    { name: "Protein", value: recipe.nutrition?.proteinGrams, unit: "g" },
    { name: "Carbs", value: recipe.nutrition?.carbsGrams, unit: "g" },
    { name: "Fat", value: recipe.nutrition?.fatGrams, unit: "g" },
  ];
  return (
    <div className="log-page max-w-4xl!">
      <Link href="/recipes" className="text-link mb-6">
        <ArrowLeft size={15} />
        Recipes
      </Link>
      <header className="log-page-header">
        <div className="min-w-0">
          <h1 className="break-words">{recipe.title}</h1>
          <p className="log-muted mt-2">
            {recipe.servings} {recipe.servings === 1 ? "serving" : "servings"}
            {recipe.prepTimeMinutes
              ? ` · ${recipe.prepTimeMinutes} min prep`
              : ""}
            {recipe.cookTimeMinutes
              ? ` · ${recipe.cookTimeMinutes} min cook`
              : ""}
          </p>
        </div>
        {user && (
          <LogEntryButton
            kind="meal"
            recipe={recipe}
            className="btn-primary shrink-0"
          >
            <Plus size={16} />
            Log this recipe
          </LogEntryButton>
        )}
      </header>
      {recipe.description && (
        <p className="log-muted leading-relaxed mb-6 max-w-prose">
          {recipe.description}
        </p>
      )}
      {recipe.imageUrl && (
        <div className="relative w-full h-64 sm:h-80 mb-8">
          <Image
            src={recipe.imageUrl}
            alt={recipe.title}
            fill
            sizes="(max-width: 768px) 100vw, 896px"
            className="object-cover rounded-sm"
          />
        </div>
      )}
      <p className="text-sm log-muted mb-3">Nutrition per serving</p>
      <div className="nutrition-strip">
        {macros.map((macro) => (
          <div key={macro.name}>
            <p className="text-sm log-muted">{macro.name}</p>
            <p className="nutrition-value num mb-0!">
              {macro.value != null ? Math.round(macro.value) : "—"}{" "}
              <span className="text-sm font-normal log-muted">
                {macro.unit}
              </span>
            </p>
          </div>
        ))}
      </div>
      {user && (
        <div className="sm:hidden mb-6">
          <LogEntryButton
            kind="meal"
            recipe={recipe}
            className="btn-primary w-full"
          >
            Log this recipe
          </LogEntryButton>
        </div>
      )}
      <RecipeIngredients recipe={recipe} />
      {recipe.instructions && (
        <section className="mt-8">
          <h2 className="mb-4">Instructions</h2>
          <p className="whitespace-pre-line leading-relaxed text-sm log-muted max-w-prose">
            {recipe.instructions}
          </p>
        </section>
      )}
      {user ? (
        <RecipeActions
          recipeId={recipeId}
          isOwner={recipe.isOwner}
          isFavorited={recipe.isFavorited}
        />
      ) : (
        <Link href="/login" className="btn-primary mt-8">
          Sign in to log this recipe
        </Link>
      )}
    </div>
  );
}
