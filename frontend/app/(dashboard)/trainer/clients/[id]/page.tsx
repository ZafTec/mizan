import { ClientNutritionView } from "@/components/trainer/ClientNutritionView";
import { ClientHeader } from "@/components/trainer/ClientHeader";

export default async function ClientDetailPage({
	params,
}: {
	params: Promise<{ id: string }>;
}) {
	const { id } = await params;

	return (
		<div className="container mx-auto px-4 py-8">
			<ClientHeader clientId={id} />

			<div className="mt-8">
				<ClientNutritionView clientId={id} />
			</div>
		</div>
	);
}
