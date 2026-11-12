import { getIngredientById, updateIngredient } from "@/data/ingredient";
import IngredientForm from "../../IngredientForm";
import AdminPageHeader from "@/components/admin/AdminPageHeader";
import { notFound } from "next/navigation";

export default async function EditIngredientPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const ingredient = await getIngredientById(id);

  if (!ingredient) {
    notFound();
  }

  return (
    <>
      <AdminPageHeader
        title="Edit food"
        lead={`Changes apply wherever ${ingredient.name} is used.`}
        back={{ href: "/admin/ingredients", label: "Foods" }}
      />

      <div className="max-w-2xl">
        <IngredientForm
          action={updateIngredient}
          initialData={ingredient}
          submitText="Save food"
        />
      </div>
    </>
  );
}
