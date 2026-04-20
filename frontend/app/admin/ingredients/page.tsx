import { getAllIngredient } from "@/data/ingredient";
import Pagination from "@/components/Pagination";
import SortableHeader from "@/components/SortableHeader";
import Link from "next/link";
import AdminIngredientActions from "./AdminIngredientActions";
import { parseListParams, buildListUrl } from "@/lib/utils/list-params";

export default async function AdminIngredientsPage({
    searchParams,
}: {
    searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
    const params = await searchParams;
    const { page, sortBy, sortOrder } = parseListParams(params);
    const searchTerm = params.search as string | undefined;

    const { ingredients, totalPages, totalCount } = await getAllIngredient(
        searchTerm,
        sortBy ?? undefined,
        sortOrder,
        page,
        20
    );
    const baseUrl = buildListUrl('/admin/ingredients', { search: searchTerm });

    return (
        <div className="space-y-6 lg:space-y-8">
            <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div className="space-y-2">
                    <p className="eyebrow">Moderation</p>
                    <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
                        Manage ingredients
                    </h1>
                    <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                        Add, edit, or delete public food ingredients.
                    </p>
                </div>
                <Link href="/admin/ingredients/add" className="btn-primary">
                    <i className="ri-add-line" />
                    Add Ingredient
                </Link>
            </header>

            {/* Search & Stats */}
	            <div className="flex flex-col md:flex-row gap-4 items-center justify-between">
                <form className="relative w-full md:w-96">
	                    <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-charcoal-blue-400" />
                    <input
                        name="search"
                        type="search"
                        placeholder="Search ingredients..."
                        defaultValue={searchTerm}
                        className="input pl-10 h-11"
                    />
                </form>
	                <div className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
	                    Showing <span className="font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">{ingredients.length}</span> of <span className="font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">{totalCount}</span> ingredients
                </div>
            </div>

            {/* Table */}
            <div className="card overflow-hidden">
	                <div className="overflow-x-auto">
	                    <table className="w-full text-left border-collapse">
	                        <thead>
	                            <tr className="border-b border-charcoal-blue-100 bg-charcoal-blue-50/50 dark:border-white/10 dark:bg-charcoal-blue-900/60">
	                                <SortableHeader sortKey="name" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Ingredient Name</SortableHeader>
	                                <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Portion</th>
	                                <SortableHeader sortKey="calories" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Calories</SortableHeader>
	                                <SortableHeader sortKey="protein" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Proteins</SortableHeader>
	                                <th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Carbs</th>
	                                <th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Fats</th>
	                                <th className="px-6 py-4 text-center text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Status</th>
	                                <th className="px-6 py-4 text-right text-xs font-bold uppercase tracking-wider text-charcoal-blue-500 dark:text-charcoal-blue-300">Actions</th>
                            </tr>
                        </thead>
	                        <tbody className="divide-y divide-charcoal-blue-100 dark:divide-white/10">
                            {ingredients.length === 0 ? (
                                <tr>
	                                    <td colSpan={8} className="px-6 py-12 text-center text-charcoal-blue-500 dark:text-charcoal-blue-400">
                                        No ingredients found.
                                    </td>
                                </tr>
                            ) : (
                                ingredients.map((ingredient) => (
	                                    <tr key={ingredient.id} className="group transition-colors hover:bg-charcoal-blue-50/50 dark:hover:bg-charcoal-blue-900/60">
                                        <td className="px-6 py-4">
	                                            <div className="text-sm font-bold text-charcoal-blue-900 transition-colors group-hover:text-brand-600 dark:text-charcoal-blue-100 dark:group-hover:text-brand-300">
                                                {ingredient.name}
                                            </div>
                                            {ingredient.brand && (
	                                                <div className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{ingredient.brand}</div>
                                            )}
                                        </td>
                                        <td className="px-6 py-4">
	                                            <div className="text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
                                                {ingredient.servingSize}{ingredient.servingUnit || 'g'}
                                            </div>
                                        </td>
                                        <td className="px-6 py-4 text-center">
                                            <span className="text-sm font-bold text-orange-600">
                                                {ingredient.caloriesPer100g}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 text-center">
                                            <span className="text-sm font-bold text-red-600">
                                                {ingredient.proteinPer100g}g
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 text-center">
                                            <span className="text-sm font-bold text-amber-600">
                                                {ingredient.carbsPer100g}g
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 text-center">
                                            <span className="text-sm font-bold text-yellow-600">
                                                {ingredient.fatPer100g}g
                                            </span>
                                        </td>
	                                        <td className="px-6 py-4 text-center">
                                            {ingredient.isVerified ? (
	                                                <span className="inline-flex items-center gap-1 rounded-full border border-emerald-100 bg-emerald-50 px-2 py-0.5 text-xs font-bold text-emerald-600 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300">
                                                    <i className="ri-checkbox-circle-fill" />
                                                    Verified
                                                </span>
                                            ) : (
	                                                <span className="inline-flex items-center gap-1 rounded-full border border-charcoal-blue-200 bg-charcoal-blue-100 px-2 py-0.5 text-xs font-bold text-charcoal-blue-500 dark:border-white/10 dark:bg-charcoal-blue-900/60 dark:text-charcoal-blue-300">
                                                    <i className="ri-question-line" />
                                                    Unverified
                                                </span>
                                            )}
                                        </td>
                                        <td className="px-6 py-4 text-right">
                                            <AdminIngredientActions id={ingredient.id} name={ingredient.name} />
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </div>

            {totalPages > 1 && (
                <Pagination
                    currentPage={page}
                    totalPages={totalPages}
                    totalCount={totalCount}
                    pageSize={20}
                    baseUrl={baseUrl}
                />
            )}
        </div>
    );
}
