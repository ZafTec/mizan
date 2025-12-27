"use client";

import { useEffect, useState } from "react";
import { apiClient } from "@/lib/auth-client";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import {
	Dialog,
	DialogContent,
	DialogDescription,
	DialogHeader,
	DialogTitle,
} from "@/components/ui/dialog";
import { useToast } from "@/hooks/use-toast";

interface PendingRequest {
	relationshipId: string;
	clientId: string;
	clientName?: string;
	clientEmail?: string;
	requestedAt: string;
}

interface PermissionSettings {
	canViewNutrition: boolean;
	canViewWorkouts: boolean;
	canViewMeasurements: boolean;
	canMessage: boolean;
}

export function TrainerPendingRequests() {
	const [requests, setRequests] = useState<PendingRequest[]>([]);
	const [loading, setLoading] = useState(true);
	const [processingId, setProcessingId] = useState<string | null>(null);
	const [showPermissionsDialog, setShowPermissionsDialog] = useState(false);
	const [selectedRequest, setSelectedRequest] = useState<PendingRequest | null>(
		null
	);
	const [permissions, setPermissions] = useState<PermissionSettings>({
		canViewNutrition: true,
		canViewWorkouts: true,
		canViewMeasurements: false,
		canMessage: true,
	});
	const { toast } = useToast();

	async function fetchRequests() {
		try {
			const data = await apiClient<PendingRequest[]>(
				"/api/Trainers/requests"
			);
			setRequests(data);
		} catch (error) {
			console.error("Failed to fetch pending requests:", error);
			toast({
				title: "Error",
				description: "Failed to load pending requests",
				variant: "destructive",
			});
		} finally {
			setLoading(false);
		}
	}

	useEffect(() => {
		fetchRequests();
	}, []);

	function handleAcceptClick(request: PendingRequest) {
		setSelectedRequest(request);
		setShowPermissionsDialog(true);
	}

	async function handleAcceptConfirm() {
		if (!selectedRequest) return;

		setProcessingId(selectedRequest.relationshipId);
		try {
			await apiClient("/api/Trainers/respond", {
				method: "POST",
				body: JSON.stringify({
					relationshipId: selectedRequest.relationshipId,
					accept: true,
					...permissions,
				}),
			});

			toast({
				title: "Request Accepted",
				description: `${selectedRequest.clientName || selectedRequest.clientEmail} is now your client`,
			});

			setRequests((prev) =>
				prev.filter((r) => r.relationshipId !== selectedRequest.relationshipId)
			);
			setShowPermissionsDialog(false);
			setSelectedRequest(null);
		} catch (error) {
			console.error("Failed to accept request:", error);
			toast({
				title: "Error",
				description: "Failed to accept request. Please try again.",
				variant: "destructive",
			});
		} finally {
			setProcessingId(null);
		}
	}

	async function handleDecline(relationshipId: string) {
		setProcessingId(relationshipId);
		try {
			await apiClient("/api/Trainers/respond", {
				method: "POST",
				body: JSON.stringify({
					relationshipId,
					accept: false,
				}),
			});

			toast({
				title: "Request Declined",
				description: "The request has been declined",
			});

			setRequests((prev) => prev.filter((r) => r.relationshipId !== relationshipId));
		} catch (error) {
			console.error("Failed to decline request:", error);
			toast({
				title: "Error",
				description: "Failed to decline request. Please try again.",
				variant: "destructive",
			});
		} finally {
			setProcessingId(null);
		}
	}

	if (loading) {
		return (
			<div className="space-y-4">
				{[1, 2].map((i) => (
					<div key={i} className="animate-pulse flex items-center space-x-4">
						<div className="w-12 h-12 bg-gray-200 rounded-full"></div>
						<div className="flex-1">
							<div className="h-4 bg-gray-200 rounded w-1/3 mb-2"></div>
							<div className="h-3 bg-gray-200 rounded w-1/4"></div>
						</div>
					</div>
				))}
			</div>
		);
	}

	if (requests.length === 0) {
		return (
			<p className="text-center text-gray-500 py-8">
				No pending requests
			</p>
		);
	}

	return (
		<>
			<div className="space-y-4">
				{requests.map((request) => (
					<div
						key={request.relationshipId}
						className="flex items-center justify-between p-4 bg-gray-50 rounded-lg"
					>
						<div className="flex items-center space-x-3">
							<div className="w-12 h-12 bg-yellow-100 rounded-full flex items-center justify-center">
								<span className="text-yellow-700 font-semibold text-lg">
									{request.clientName?.[0] || request.clientEmail?.[0] || "?"}
								</span>
							</div>
							<div>
								<p className="font-medium">
									{request.clientName || request.clientEmail || "Unknown Client"}
								</p>
								<p className="text-sm text-gray-500">
									Requested {new Date(request.requestedAt).toLocaleDateString()}
								</p>
							</div>
						</div>

						<div className="flex items-center space-x-2">
							<Button
								size="sm"
								variant="outline"
								onClick={() => handleDecline(request.relationshipId)}
								disabled={processingId === request.relationshipId}
							>
								Decline
							</Button>
							<Button
								size="sm"
								onClick={() => handleAcceptClick(request)}
								disabled={processingId === request.relationshipId}
							>
								Accept
							</Button>
						</div>
					</div>
				))}
			</div>

			<Dialog open={showPermissionsDialog} onOpenChange={setShowPermissionsDialog}>
				<DialogContent>
					<DialogHeader>
						<DialogTitle>Set Client Permissions</DialogTitle>
						<DialogDescription>
							Choose what data you can access for{" "}
							{selectedRequest?.clientName || selectedRequest?.clientEmail}
						</DialogDescription>
					</DialogHeader>

					<div className="space-y-4 py-4">
						<div className="flex items-center space-x-2">
							<Checkbox
								id="canViewNutrition"
								checked={permissions.canViewNutrition}
								onCheckedChange={(checked) =>
									setPermissions((prev) => ({
										...prev,
										canViewNutrition: checked as boolean,
									}))
								}
							/>
							<Label htmlFor="canViewNutrition">View Nutrition Data</Label>
						</div>

						<div className="flex items-center space-x-2">
							<Checkbox
								id="canViewWorkouts"
								checked={permissions.canViewWorkouts}
								onCheckedChange={(checked) =>
									setPermissions((prev) => ({
										...prev,
										canViewWorkouts: checked as boolean,
									}))
								}
							/>
							<Label htmlFor="canViewWorkouts">View Workout Data</Label>
						</div>

						<div className="flex items-center space-x-2">
							<Checkbox
								id="canViewMeasurements"
								checked={permissions.canViewMeasurements}
								onCheckedChange={(checked) =>
									setPermissions((prev) => ({
										...prev,
										canViewMeasurements: checked as boolean,
									}))
								}
							/>
							<Label htmlFor="canViewMeasurements">
								View Body Measurements
							</Label>
						</div>

						<div className="flex items-center space-x-2">
							<Checkbox
								id="canMessage"
								checked={permissions.canMessage}
								onCheckedChange={(checked) =>
									setPermissions((prev) => ({
										...prev,
										canMessage: checked as boolean,
									}))
								}
							/>
							<Label htmlFor="canMessage">Message Client</Label>
						</div>
					</div>

					<div className="flex justify-end space-x-2">
						<Button
							variant="outline"
							onClick={() => {
								setShowPermissionsDialog(false);
								setSelectedRequest(null);
							}}
						>
							Cancel
						</Button>
						<Button
							onClick={handleAcceptConfirm}
							disabled={processingId === selectedRequest?.relationshipId}
						>
							Confirm & Accept
						</Button>
					</div>
				</DialogContent>
			</Dialog>
		</>
	);
}
