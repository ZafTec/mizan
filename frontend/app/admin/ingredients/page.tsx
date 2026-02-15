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
        <div className="space-y-6">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Manage Ingredients</h1>
                    <p className="text-slate-500 dark:text-slate-400 mt-1">Add, edit, or delete public food ingredients</p>
                </div>
                <Link href="/admin/ingredients/add" className="btn-primary">
                    <i className="ri-add-line" />
                    Add Ingredient
                </Link>
            </div>

            {/* Search & Stats */}
            <div className="flex flex-col md:flex-row gap-4 items-center justify-between">
                <form className="relative w-full md:w-96">
                    <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
                    <input
                        name="search"
                        type="search"
                        placeholder="Search ingredients..."
                        defaultValue={searchTerm}
                        className="input pl-10 h-11"
                    />
                </form>
                <div className="text-sm text-slate-500">
                    Showing <span className="font-bold text-slate-900">{ingredients.length}</span> of <span className="font-bold text-slate-900">{totalCount}</span> ingredients
                </div>
            </div>

            {/* Table */}
            <div className="card overflow-hidden">
                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="bg-slate-50/50 border-b border-slate-100">
                                <SortableHeader sortKey="name" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500">Ingredient Name</SortableHeader>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500">Portion</th>
                                <SortableHeader sortKey="calories" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500 text-center">Calories</SortableHeader>
                                <SortableHeader sortKey="protein" currentSort={sortBy} currentOrder={sortOrder} baseUrl={baseUrl} className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500 text-center">Proteins</SortableHeader>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500 text-center">Carbs</th>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500 text-center">Fats</th>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500 text-center">Status</th>
                                <th className="px-6 py-4 text-xs uppercase tracking-wider font-bold text-slate-500 text-right">Actions</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-100">
                            {ingredients.length === 0 ? (
                                <tr>
                                    <td colSpan={8} className="px-6 py-12 text-center text-slate-500">
                                        No ingredients found.
                                    </td>
                                </tr>
                            ) : (
                                ingredients.map((ingredient) => (
                                    <tr key={ingredient.id} className="hover:bg-slate-50/50 transition-colors group">
                                        <td className="px-6 py-4">
                                            <div className="text-sm font-bold text-slate-900 group-hover:text-brand-600 transition-colors">
                                                {ingredient.name}
                                            </div>
                                            {ingredient.brand && (
                                                <div className="text-xs text-slate-500">{ingredient.brand}</div>
                                            )}
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="text-sm text-slate-700">
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
                                                <span className="px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-600 text-xs font-bold border border-emerald-100 inline-flex items-center gap-1">
                                                    <i className="ri-checkbox-circle-fill" />
                                                    Verified
                                                </span>
                                            ) : (
                                                <span className="px-2 py-0.5 rounded-full bg-slate-100 text-slate-500 text-xs font-bold border border-slate-200 inline-flex items-center gap-1">
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
