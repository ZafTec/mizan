import { ClientList } from "@/components/trainer/ClientList";
import { TrainerStats } from "@/components/trainer/TrainerStats";
import { RecentMessages } from "@/components/trainer/RecentMessages";
import { TrainerPendingRequests } from "@/components/trainer/TrainerPendingRequests";

export default function TrainerDashboard() {
	return (
		<div className="container mx-auto px-4 py-8">
			<h1 className="text-3xl font-bold mb-8">Trainer Dashboard</h1>

			<div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
				<TrainerStats />
			</div>

			<div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
				<div className="bg-white dark:bg-slate-900 rounded-lg shadow-md p-6">
					<h2 className="text-xl font-semibold mb-4">Pending Requests</h2>
					<TrainerPendingRequests />
				</div>

				<div className="bg-white dark:bg-slate-900 rounded-lg shadow-md p-6">
					<h2 className="text-xl font-semibold mb-4">Recent Messages</h2>
					<RecentMessages />
				</div>
			</div>

			<div className="grid grid-cols-1 gap-6">
				<div className="bg-white dark:bg-slate-900 rounded-lg shadow-md p-6">
					<h2 className="text-xl font-semibold mb-4">Your Clients</h2>
					<ClientList />
				</div>
			</div>
		</div>
	);
}
