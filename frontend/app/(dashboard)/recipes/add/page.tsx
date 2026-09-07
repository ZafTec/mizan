import { redirect } from "next/navigation";
export default function AddRecipePage() {
  redirect("/today?log=meal");
}
