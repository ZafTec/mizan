import { redirect } from "next/navigation";
import { auth } from "@/lib/auth";
import Link from "next/link";

export const metadata = {
  title: "Trainer-Client Relationships | Admin",
  description: "Manage trainer-client relationships",
};

interface Relationship {
  id: string;
  trainerId: string;
  clientId: string;
  status: string;
  canViewNutrition: boolean;
  canViewWorkouts: boolean;
  canViewMeasurements: boolean;
  canMessage: boolean;
  startedAt: string | null;
  endedAt: string | null;
  createdAt: string;
}

async function getRelationships(): Promise<
  Array<Relationship & { trainerEmail: string; clientEmail: string }>
> {
  // For now, return empty array since we need backend API endpoint
  // TODO: Implement backend API endpoint /api/TrainerClientRelationships
  return [];
}

export default async function RelationshipsPage() {
  const session = await auth.api.getSession({
    headers: await import("next/headers").then((mod) => mod.headers()),
  });

  if (!session?.user) {
    redirect("/login");
  }

  if (session.user.role !== "admin") {
    redirect("/");
  }

  const relationships = await getRelationships();

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold mb-2">
            Trainer-Client Relationships
          </h1>
          <p className="text-muted-foreground">
            {relationships.length} total relationships
          </p>
        </div>
        <Link
          href="/admin"
          className="px-4 py-2 text-sm border rounded-lg hover:bg-accent"
        >
          ← Back to Dashboard
        </Link>
      </div>

      <div className="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg p-6 mb-6">
        <h3 className="text-lg font-semibold text-yellow-800 dark:text-yellow-200 mb-2">
          Feature In Development
        </h3>
        <p className="text-sm text-yellow-700 dark:text-yellow-300 mb-4">
          Trainer-client relationship management requires a backend API endpoint
          that hasn't been implemented yet.
        </p>
        <p className="text-sm text-yellow-700 dark:text-yellow-300">
          <strong>Backend TODO:</strong>
        </p>
        <ul className="list-disc list-inside text-sm text-yellow-700 dark:text-yellow-300 mt-2 space-y-1">
          <li>
            Create GET /api/TrainerClientRelationships endpoint with admin
            authorization
          </li>
          <li>Include user details (email, name) for trainers and clients</li>
          <li>Add filtering by status (pending, active, paused, ended)</li>
          <li>Implement pagination (50 per page)</li>
        </ul>
      </div>

      <div className="bg-card rounded-lg border overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-muted">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Trainer
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Client
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Status
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Permissions
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider">
                  Started
                </th>
                <th className="px-6 py-3 text-right text-xs font-medium uppercase tracking-wider">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {relationships.length === 0 ? (
                <tr>
                  <td
                    colSpan={6}
                    className="px-6 py-8 text-center text-muted-foreground"
                  >
                    No relationships to display. Backend API endpoint required.
                  </td>
                </tr>
              ) : (
                relationships.map((rel) => (
                  <tr key={rel.id} className="hover:bg-muted/50">
                    <td className="px-6 py-4">
                      <p className="font-medium">{rel.trainerEmail}</p>
                    </td>
                    <td className="px-6 py-4">
                      <p className="font-medium">{rel.clientEmail}</p>
                    </td>
                    <td className="px-6 py-4">
                      <span
                        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                          rel.status === "active"
                            ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
                            : rel.status === "pending"
                            ? "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200"
                            : rel.status === "paused"
                            ? "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200"
                            : "bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200"
                        }`}
                      >
                        {rel.status}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-sm">
                      <div className="flex gap-2 flex-wrap">
                        {rel.canViewNutrition && (
                          <span className="px-2 py-1 bg-primary/10 text-primary rounded text-xs">
                            Nutrition
                          </span>
                        )}
                        {rel.canViewWorkouts && (
                          <span className="px-2 py-1 bg-primary/10 text-primary rounded text-xs">
                            Workouts
                          </span>
                        )}
                        {rel.canViewMeasurements && (
                          <span className="px-2 py-1 bg-primary/10 text-primary rounded text-xs">
                            Measurements
                          </span>
                        )}
                        {rel.canMessage && (
                          <span className="px-2 py-1 bg-primary/10 text-primary rounded text-xs">
                            Messaging
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-6 py-4 text-sm text-muted-foreground">
                      {rel.startedAt
                        ? new Date(rel.startedAt).toLocaleDateString()
                        : "—"}
                    </td>
                    <td className="px-6 py-4 text-right">
                      <button className="text-primary hover:underline text-sm">
                        Manage →
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
