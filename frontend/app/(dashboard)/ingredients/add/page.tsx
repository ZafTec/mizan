import { getUserServer } from "@/helper/session";
import { redirect } from "next/navigation";
import AddIngredientForm from "./AddIngredientForm";

export default async function Page() {
	const user = await getUserServer();

	if (user.role !== "admin") {
		redirect("/ingredients");
	}

	return <AddIngredientForm />;
}
