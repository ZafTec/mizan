"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import ConfirmationModal from "@/components/ConfirmationModal";

interface RecipeActionsProps {
    recipeId: string;
    isOwner: boolean;
    isFavorited: boolean;
}

export default function RecipeActions({ recipeId, isOwner, isFavorited: initialFavorited }: RecipeActionsProps) {
    const router = useRouter();
    const [isFavorited, setIsFavorited] = useState(initialFavorited);
    const [isToggling, setIsToggling] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [showCopied, setShowCopied] = useState(false);

    const handleToggleFavorite = async () => {
        setIsToggling(true);
        try {
            const response = await fetch(`/api/bff/Recipes/${recipeId}/favorite`, {
                method: "POST",
            });


            if (!response.ok) {
                const text = await response.text();
                throw new Error(`Failed to toggle favorite.\n` + (text ? `: ${text}` : ""));
            }

            const data = await response.json();
            setIsFavorited(data.isFavorited);
            router.refresh();
        } catch (error) {
            console.error("[Recipe Favorite] Error:", error);
            alert("Failed to update favorites");
        } finally {
            setIsToggling(false);
        }
    };

    const handleDelete = async () => {
        setIsDeleting(true);
        setShowDeleteModal(false);

        try {
            const response = await fetch(`/api/bff/Recipes/${recipeId}`, {
                method: "DELETE",
            });
            if (!response.ok) {
                const text = await response.text();
                console.error('[Recipe Delete] Error response:', text);

                try {
                    const data = JSON.parse(text);
                    throw new Error(data.message || "Failed to delete recipe");
                } catch {
                    throw new Error(`Failed to delete recipe: ${response.status}`);
                }
            }
            router.push("/recipes");
            router.refresh();
        } catch (error: any) {
            console.error('[Recipe Delete] Failed:', error);
            alert(error.message || "Failed to delete recipe");
            setIsDeleting(false);
        }
    };

    const handleShare = async () => {
        const url = `${window.location.origin}/recipes/${recipeId}`;
        try {
            await navigator.clipboard.writeText(url);
            setShowCopied(true);
            setTimeout(() => setShowCopied(false), 2000);
        } catch (error) {
            console.error("Error copying to clipboard:", error);
            alert("Failed to copy link");
        }
    };

    const handleAddToMealPlan = () => {
        router.push(`/meals/add/${recipeId}`);
    };

    return (
        <>
            <ConfirmationModal
                isOpen={showDeleteModal}
                onClose={() => setShowDeleteModal(false)}
                onConfirm={handleDelete}
                title="Delete Recipe"
                message="Are you sure you want to delete this recipe? This action cannot be undone."
                confirmText="Delete Recipe"
                isLoading={isDeleting}
            />

            <div className="card p-6 space-y-4">
                {/* Primary Actions */}
                <div className="flex flex-wrap gap-3">
                    <button onClick={handleAddToMealPlan} className="btn-primary flex-1 sm:flex-none">
                        <i className="ri-add-line" />
                        Add to Meal Plan
                    </button>
                    <button
                        onClick={handleToggleFavorite}
                        disabled={isToggling}
                        className={`btn-secondary flex-1 sm:flex-none ${isFavorited ? "bg-red-100! text-red-600!" : ""}`}
                    >
                        <i className={isFavorited ? "ri-heart-3-fill" : "ri-heart-3-line"} />
                        {isFavorited ? "Favorited" : "Save to Favorites"}
                    </button>
                    <button onClick={handleShare} className={`btn-secondary flex-1 sm:flex-none transition-all ${showCopied ? "bg-green-100! text-green-700!" : ""}`}>
                        {showCopied ? (
                            <>
                                <i className="ri-check-line" />
                                Copied!
                            </>
                        ) : (
                            <>
                                <i className="ri-share-line" />
                                Share
                            </>
                        )}
                    </button>
                </div>

                {/* Owner Actions */}
                {isOwner && (
                    <div className="flex flex-wrap gap-3 pt-3 border-t border-slate-200">
                        <button
                            onClick={() => router.push(`/recipes/${recipeId}/edit`)}
                            className="btn-secondary flex-1 sm:flex-none"
                        >
                            <i className="ri-edit-line" />
                            Edit Recipe
                        </button>
                        <button
                            onClick={() => setShowDeleteModal(true)}
                            disabled={isDeleting}
                            className="btn-secondary flex-1 sm:flex-none text-red-600! border-red-200! hover:bg-red-50!"
                        >
                            <i className="ri-delete-bin-line" />
                            {isDeleting ? "Deleting..." : "Delete Recipe"}
                        </button>
                    </div>
                )}
            </div>
        </>
    );
}
