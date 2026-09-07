import { redirect } from "next/navigation";
export default async function FavoritesPage({
  searchParams,
}: {
  searchParams: Promise<{ page?: string }>;
}) {
  const { page } = await searchParams;
  redirect(
    `/recipes?pinned=true${page ? `&page=${encodeURIComponent(page)}` : ""}`,
  );
}
