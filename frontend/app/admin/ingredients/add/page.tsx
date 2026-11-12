import { addIngredient } from "@/data/ingredient";
import IngredientForm from "../IngredientForm";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export default function AddIngredientPage() {
  return (
    <>
      <AdminPageHeader
        title="New food"
        lead="A new building block for recipes and logs."
        back={{ href: "/admin/ingredients", label: "Foods" }}
      />

      <div className="max-w-2xl">
        <IngredientForm action={addIngredient} submitText="Create food" />
      </div>
    </>
  );
}
