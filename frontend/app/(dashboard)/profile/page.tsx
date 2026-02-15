"use client";

import { useSession, signOut } from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import Image from "next/image";
import Link from "next/link";
import { useState } from "react";
import { CldUploadWidget } from "next-cloudinary";
import { toast } from "sonner";

export default function ProfilePage() {
	const { data: session, isPending } = useSession();
	const [showEditModal, setShowEditModal] = useState(false);
	const [showNotificationsModal, setShowNotificationsModal] = useState(false);
	const [showPrivacyModal, setShowPrivacyModal] = useState(false);
	const [showAppearanceModal, setShowAppearanceModal] = useState(false);
	const [name, setName] = useState("");
	const [image, setImage] = useState("");
	const [isUpdating, setIsUpdating] = useState(false);

	if (isPending) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-500"></div>
			</div>
		);
	}

	if (!session?.user) {
		return (
			<div className="flex items-center justify-center min-h-screen">
				<p className="text-slate-500">Not authenticated</p>
			</div>
		);
	}

	const user = session.user;
	const isTrainer = user.role === "trainer" || user.role === "admin";

	const handleSignOut = async () => {
		await signOut();
		window.location.href = "/login";
	};

	const handleUpdateProfile = async () => {
		setIsUpdating(true);
		try {
			const updateData: Record<string, string> = {};
			if (name) updateData.name = name;
			if (image) updateData.image = image;

			await Promise.all([
				clientApi("/api/Users/me", {
					method: "PUT",
					body: {
						name: name || null,
						image: image || null,
					},
				}),
				Object.keys(updateData).length > 0
					? authClient.updateUser(updateData)
					: Promise.resolve(),
			]);
			setShowEditModal(false);
			window.location.reload();
		} catch (error) {
			console.error("Failed to update profile:", error);
			toast.error("Failed to update profile");
		} finally {
			setIsUpdating(false);
		}
	};

	const handleImageUpload = (result: any) => {
		const imageUrl = result.info.secure_url;
		setImage(imageUrl);
	};

	return (
		<div className="max-w-3xl mx-auto space-y-6">
			{/* Header */}
			<div>
				<h1 className="text-2xl font-bold text-slate-900">Profile</h1>
				<p className="text-slate-500 mt-1">Manage your account settings</p>
			</div>

			{/* Profile Card */}
			<div className="card p-6">
				<div className="flex flex-col sm:flex-row items-center gap-6">
					<div className="relative">
						{user.image ? (
							<Image
								src={user.image}
								alt={user.email || "User"}
								width={96}
								height={96}
								className="w-24 h-24 rounded-2xl object-cover border-4 border-white shadow-lg"
							/>
						) : (
							<div className="w-24 h-24 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-600 flex items-center justify-center border-4 border-white shadow-lg">
								<span className="text-3xl font-bold text-white">
									{user.email?.charAt(0).toUpperCase()}
								</span>
							</div>
						)}
						<CldUploadWidget
							uploadPreset="mizan_preset"
							onSuccess={async (result: any) => {
								const imageUrl = result.info.secure_url;
								try {
									await clientApi("/api/Users/me", {
										method: "PUT",
										body: {
											name: user.name || null,
											image: imageUrl,
										},
									});
									window.location.reload();
								} catch (error) {
									console.error("Failed to upload image:", error);
									toast.error("Failed to upload image");
								}
							}}
						>
							{({ open }) => (
								<button
									onClick={() => open()}
									className="absolute -bottom-2 -right-2 w-8 h-8 rounded-full bg-white shadow-md flex items-center justify-center hover:bg-slate-50 transition-colors"
								>
									<i className="ri-camera-line text-slate-600" />
								</button>
							)}
						</CldUploadWidget>
					</div>
					<div className="text-center sm:text-left flex-1">
						<h2 className="text-xl font-bold text-slate-900">
							{user.name || user.email?.split("@")[0]}
						</h2>
						<p className="text-slate-500">{user.email}</p>
					</div>
					<button
						onClick={() => {
							setName(user.name || "");
							setImage(user.image || "");
							setShowEditModal(true);
						}}
						className="btn-secondary"
					>
						<i className="ri-edit-line" />
						Edit Profile
					</button>
				</div>
			</div>

			{/* Quick Links */}
			<div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
				<Link href="/goal" className="card-hover p-5 group">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-600 flex items-center justify-center">
							<i className="ri-target-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">Nutrition Goals</h3>
							<p className="text-sm text-slate-500">Set your daily targets</p>
						</div>
						<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
					</div>
				</Link>

				<Link href="/meals" className="card-hover p-5 group">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-accent-400 to-accent-600 flex items-center justify-center">
							<i className="ri-bowl-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">Food Diary</h3>
							<p className="text-sm text-slate-500">Track your daily meals</p>
						</div>
						<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
					</div>
				</Link>

				<Link href="/meal-plan" className="card-hover p-5 group">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-sky-400 to-sky-600 flex items-center justify-center">
							<i className="ri-calendar-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">Meal Plan</h3>
							<p className="text-sm text-slate-500">Plan your weekly meals</p>
						</div>
						<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
					</div>
				</Link>

				<Link href="/suggestions" className="card-hover p-5 group">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-violet-400 to-violet-600 flex items-center justify-center">
							<i className="ri-magic-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">AI Coach</h3>
							<p className="text-sm text-slate-500">Get AI-powered suggestions</p>
						</div>
						<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
					</div>
				</Link>

				<Link href="/recipes" className="card-hover p-5 group">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-rose-400 to-rose-600 flex items-center justify-center">
							<i className="ri-book-3-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">My Recipes</h3>
							<p className="text-sm text-slate-500">View your collection</p>
						</div>
						<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
					</div>
				</Link>

				<Link href="/ingredients" className="card-hover p-5 group">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-green-400 to-green-600 flex items-center justify-center">
							<i className="ri-leaf-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">Foods</h3>
							<p className="text-sm text-slate-500">Browse food database</p>
						</div>
						<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
					</div>
				</Link>

				<Link href="/body-measurements" className="card-hover p-5 group">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-cyan-400 to-cyan-600 flex items-center justify-center">
							<i className="ri-body-scan-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">Body Measurements</h3>
							<p className="text-sm text-slate-500">Track your progress</p>
						</div>
						<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
					</div>
				</Link>

				<Link href="/achievements" className="card-hover p-5 group">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-amber-400 to-amber-600 flex items-center justify-center">
							<i className="ri-trophy-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">Achievements</h3>
							<p className="text-sm text-slate-500">Badges & streaks</p>
						</div>
						<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
					</div>
				</Link>

				<Link href="/workouts" className="card-hover p-5 group">
					<div className="flex items-center gap-4">
						<div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-indigo-400 to-indigo-600 flex items-center justify-center">
							<i className="ri-run-line text-xl text-white" />
						</div>
						<div className="flex-1">
							<h3 className="font-semibold text-slate-900">Workouts</h3>
							<p className="text-sm text-slate-500">Log your training</p>
						</div>
						<i className="ri-arrow-right-s-line text-xl text-slate-400 group-hover:text-brand-500 transition-colors" />
					</div>
				</Link>
			</div>

			{/* Trainer Features - Only visible for users with trainer role */}
			{isTrainer && (
				<div className="card p-6 border-2 border-emerald-200 bg-gradient-to-br from-emerald-50 to-teal-50">
					<div className="flex items-center gap-3 mb-4">
						<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 flex items-center justify-center">
							<i className="ri-user-star-line text-xl text-white" />
						</div>
						<div>
							<h2 className="font-semibold text-slate-900">Trainer Features</h2>
							<p className="text-sm text-slate-600">Manage your clients and training</p>
						</div>
					</div>

					<div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<Link href="/trainer" className="card-hover p-4 bg-white group">
							<div className="flex items-center gap-3">
								<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-400 to-teal-500 flex items-center justify-center">
									<i className="ri-dashboard-line text-lg text-white" />
								</div>
								<div className="flex-1">
									<h3 className="font-medium text-slate-900">Dashboard</h3>
									<p className="text-xs text-slate-500">View clients & stats</p>
								</div>
								<i className="ri-arrow-right-s-line text-lg text-slate-400 group-hover:text-emerald-500 transition-colors" />
							</div>
						</Link>

						<Link href="/trainer#clients" className="card-hover p-4 bg-white group">
							<div className="flex items-center gap-3">
								<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-400 to-blue-500 flex items-center justify-center">
									<i className="ri-group-line text-lg text-white" />
								</div>
								<div className="flex-1">
									<h3 className="font-medium text-slate-900">My Clients</h3>
									<p className="text-xs text-slate-500">Manage relationships</p>
								</div>
								<i className="ri-arrow-right-s-line text-lg text-slate-400 group-hover:text-emerald-500 transition-colors" />
							</div>
						</Link>

						<Link href="/trainer#requests" className="card-hover p-4 bg-white group">
							<div className="flex items-center gap-3">
								<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center">
									<i className="ri-user-add-line text-lg text-white" />
								</div>
								<div className="flex-1">
									<h3 className="font-medium text-slate-900">Requests</h3>
									<p className="text-xs text-slate-500">Pending client requests</p>
								</div>
								<i className="ri-arrow-right-s-line text-lg text-slate-400 group-hover:text-emerald-500 transition-colors" />
							</div>
						</Link>

						<Link href="/trainer#messages" className="card-hover p-4 bg-white group">
							<div className="flex items-center gap-3">
								<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-purple-400 to-purple-500 flex items-center justify-center">
									<i className="ri-message-3-line text-lg text-white" />
								</div>
								<div className="flex-1">
									<h3 className="font-medium text-slate-900">Messages</h3>
									<p className="text-xs text-slate-500">Chat with clients</p>
								</div>
								<i className="ri-arrow-right-s-line text-lg text-slate-400 group-hover:text-emerald-500 transition-colors" />
							</div>
						</Link>
					</div>
				</div>
			)}

			{/* Find a Trainer - Only visible for regular users (non-trainers) */}
			{!isTrainer && (
				<div className="card p-6 border-2 border-blue-200 bg-gradient-to-br from-blue-50 to-indigo-50">
					<div className="flex items-center gap-3 mb-4">
						<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center">
							<i className="ri-user-search-line text-xl text-white" />
						</div>
						<div>
							<h2 className="font-semibold text-slate-900">Personal Training</h2>
							<p className="text-sm text-slate-600">Connect with a certified trainer</p>
						</div>
					</div>

					<div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
						<Link href="/trainers" className="card-hover p-4 bg-white group">
							<div className="flex items-center gap-3">
								<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-400 to-indigo-500 flex items-center justify-center">
									<i className="ri-search-line text-lg text-white" />
								</div>
								<div className="flex-1">
									<h3 className="font-medium text-slate-900">Find a Trainer</h3>
									<p className="text-xs text-slate-500">Browse available trainers</p>
								</div>
								<i className="ri-arrow-right-s-line text-lg text-slate-400 group-hover:text-blue-500 transition-colors" />
							</div>
						</Link>

						<Link href="/trainers/my-trainer" className="card-hover p-4 bg-white group">
							<div className="flex items-center gap-3">
								<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-400 to-emerald-500 flex items-center justify-center">
									<i className="ri-user-heart-line text-lg text-white" />
								</div>
								<div className="flex-1">
									<h3 className="font-medium text-slate-900">My Trainer</h3>
									<p className="text-xs text-slate-500">View current trainer</p>
								</div>
								<i className="ri-arrow-right-s-line text-lg text-slate-400 group-hover:text-blue-500 transition-colors" />
							</div>
						</Link>

						<Link href="/trainers/requests" className="card-hover p-4 bg-white group">
							<div className="flex items-center gap-3">
								<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center">
									<i className="ri-mail-send-line text-lg text-white" />
								</div>
								<div className="flex-1">
									<h3 className="font-medium text-slate-900">My Requests</h3>
									<p className="text-xs text-slate-500">Pending trainer requests</p>
								</div>
								<i className="ri-arrow-right-s-line text-lg text-slate-400 group-hover:text-blue-500 transition-colors" />
							</div>
						</Link>

						<div className="card-hover p-4 bg-white">
							<div className="flex items-center gap-3">
								<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-purple-400 to-purple-500 flex items-center justify-center">
									<i className="ri-star-line text-lg text-white" />
								</div>
								<div className="flex-1">
									<h3 className="font-medium text-slate-900">Benefits</h3>
									<div className="text-xs text-slate-500 mt-1 space-y-1">
										<p>• Personalized meal plans</p>
										<p>• Custom workout programs</p>
										<p>• Progress tracking</p>
										<p>• Direct messaging</p>
									</div>
								</div>
							</div>
						</div>
					</div>
				</div>
			)}

			{/* Developer Settings */}
			<div className="card p-6 border-2 border-purple-200 bg-gradient-to-br from-purple-50 to-indigo-50">
				<div className="flex items-center gap-3 mb-4">
					<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-purple-500 to-indigo-600 flex items-center justify-center">
						<i className="ri-code-s-slash-line text-xl text-white" />
					</div>
					<div>
						<h2 className="font-semibold text-slate-900">Developer Settings</h2>
						<p className="text-sm text-slate-600">API access and integrations</p>
					</div>
				</div>

				<div className="grid grid-cols-1 gap-3">
					<Link href="/profile/mcp" className="card-hover p-4 bg-white group">
						<div className="flex items-center gap-3">
							<div className="w-10 h-10 rounded-xl bg-gradient-to-br from-purple-400 to-indigo-500 flex items-center justify-center">
								<i className="ri-openai-line text-lg text-white" />
							</div>
							<div className="flex-1">
								<h3 className="font-medium text-slate-900">MCP Integration</h3>
								<p className="text-xs text-slate-500">Model Context Protocol tokens & analytics</p>
							</div>
							<i className="ri-arrow-right-s-line text-lg text-slate-400 group-hover:text-purple-500 transition-colors" />
						</div>
					</Link>
				</div>
			</div>

			{/* Account Settings */}
			<div className="card p-6 space-y-4">
				<h2 className="font-semibold text-slate-900 flex items-center gap-2">
					<i className="ri-settings-3-line text-brand-500" />
					Account Settings
				</h2>

				<div className="divide-y divide-slate-100">
					<button
						onClick={() => setShowNotificationsModal(true)}
						className="w-full flex items-center justify-between py-4 hover:bg-slate-50 -mx-6 px-6 transition-colors"
					>
						<div className="flex items-center gap-3">
							<i className="ri-notification-3-line text-slate-400" />
							<span className="text-slate-700">Notifications</span>
						</div>
						<i className="ri-arrow-right-s-line text-slate-400" />
					</button>
					<button
						onClick={() => setShowPrivacyModal(true)}
						className="w-full flex items-center justify-between py-4 hover:bg-slate-50 -mx-6 px-6 transition-colors"
					>
						<div className="flex items-center gap-3">
							<i className="ri-lock-line text-slate-400" />
							<span className="text-slate-700">Privacy & Security</span>
						</div>
						<i className="ri-arrow-right-s-line text-slate-400" />
					</button>
					<button
						onClick={() => setShowAppearanceModal(true)}
						className="w-full flex items-center justify-between py-4 hover:bg-slate-50 -mx-6 px-6 transition-colors"
					>
						<div className="flex items-center gap-3">
							<i className="ri-palette-line text-slate-400" />
							<span className="text-slate-700">Appearance</span>
						</div>
						<i className="ri-arrow-right-s-line text-slate-400" />
					</button>
				</div>
			</div>

			{/* Danger Zone */}
			<div className="card p-6 border-red-200">
				<h2 className="font-semibold text-red-600 flex items-center gap-2 mb-4">
					<i className="ri-error-warning-line" />
					Danger Zone
				</h2>
				<div className="flex items-center justify-between">
					<div>
						<p className="font-medium text-slate-900">Sign Out</p>
						<p className="text-sm text-slate-500">Sign out of your account</p>
					</div>
					<button
						onClick={handleSignOut}
						className="px-4 py-2 rounded-xl border border-red-200 text-red-600 hover:bg-red-50 transition-colors"
					>
						Sign Out
					</button>
				</div>
			</div>

			{/* Edit Profile Modal */}
			{showEditModal && (
				<div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
					<div className="card max-w-md w-full p-6 space-y-4">
						<div className="flex items-center justify-between">
							<h3 className="text-lg font-semibold text-slate-900">
								Edit Profile
							</h3>
							<button
								onClick={() => setShowEditModal(false)}
								className="text-slate-400 hover:text-slate-600"
							>
								<i className="ri-close-line text-xl" />
							</button>
						</div>

						<div className="space-y-4">
							<div>
								<label className="block text-sm font-medium text-slate-700 mb-1">
									Name
								</label>
								<input
									type="text"
									value={name}
									onChange={(e) => setName(e.target.value)}
									className="input"
									placeholder="Enter your name"
								/>
							</div>

							<div>
								<label className="block text-sm font-medium text-slate-700 mb-1">
									Profile Picture URL
								</label>
								<input
									type="text"
									value={image}
									onChange={(e) => setImage(e.target.value)}
									className="input"
									placeholder="https://..."
								/>
								<p className="text-xs text-slate-500 mt-1">
									Or use the camera button on your profile picture
								</p>
							</div>
						</div>

						<div className="flex gap-3">
							<button
								onClick={() => setShowEditModal(false)}
								className="btn-secondary flex-1"
								disabled={isUpdating}
							>
								Cancel
							</button>
							<button
								onClick={handleUpdateProfile}
								className="btn-primary flex-1"
								disabled={isUpdating}
							>
								{isUpdating ? "Saving..." : "Save Changes"}
							</button>
						</div>
					</div>
				</div>
			)}

			{/* Notifications Modal */}
			{showNotificationsModal && (
				<div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
					<div className="card max-w-md w-full p-6 space-y-4">
						<div className="flex items-center justify-between">
							<h3 className="text-lg font-semibold text-slate-900">
								Notification Settings
							</h3>
							<button
								onClick={() => setShowNotificationsModal(false)}
								className="text-slate-400 hover:text-slate-600"
							>
								<i className="ri-close-line text-xl" />
							</button>
						</div>

						<div className="space-y-4">
							<div className="flex items-center justify-between py-3 border-b border-slate-100">
								<div>
									<p className="font-medium text-slate-900">Meal Reminders</p>
									<p className="text-sm text-slate-500">
										Get notified about upcoming meals
									</p>
								</div>
								<input type="checkbox" className="toggle" defaultChecked />
							</div>

							<div className="flex items-center justify-between py-3 border-b border-slate-100">
								<div>
									<p className="font-medium text-slate-900">Workout Reminders</p>
									<p className="text-sm text-slate-500">
										Get notified about scheduled workouts
									</p>
								</div>
								<input type="checkbox" className="toggle" defaultChecked />
							</div>

							<div className="flex items-center justify-between py-3 border-b border-slate-100">
								<div>
									<p className="font-medium text-slate-900">Goal Achievements</p>
									<p className="text-sm text-slate-500">
										Celebrate when you hit your targets
									</p>
								</div>
								<input type="checkbox" className="toggle" defaultChecked />
							</div>

							<div className="flex items-center justify-between py-3">
								<div>
									<p className="font-medium text-slate-900">Weekly Reports</p>
									<p className="text-sm text-slate-500">
										Receive weekly progress summaries
									</p>
								</div>
								<input type="checkbox" className="toggle" />
							</div>
						</div>

						<button
							onClick={() => setShowNotificationsModal(false)}
							className="btn-primary w-full"
						>
							Save Preferences
						</button>
					</div>
				</div>
			)}

			{/* Privacy & Security Modal */}
			{showPrivacyModal && (
				<div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
					<div className="card max-w-md w-full p-6 space-y-4">
						<div className="flex items-center justify-between">
							<h3 className="text-lg font-semibold text-slate-900">
								Privacy & Security
							</h3>
							<button
								onClick={() => setShowPrivacyModal(false)}
								className="text-slate-400 hover:text-slate-600"
							>
								<i className="ri-close-line text-xl" />
							</button>
						</div>

						<div className="space-y-4">
							<div className="flex items-center justify-between py-3 border-b border-slate-100">
								<div>
									<p className="font-medium text-slate-900">Profile Visibility</p>
									<p className="text-sm text-slate-500">
										Who can see your profile
									</p>
								</div>
								<select className="input py-1 px-2 text-sm">
									<option>Everyone</option>
									<option>Household Only</option>
									<option>Private</option>
								</select>
							</div>

							<div className="flex items-center justify-between py-3 border-b border-slate-100">
								<div>
									<p className="font-medium text-slate-900">Activity Sharing</p>
									<p className="text-sm text-slate-500">
										Share your progress with friends
									</p>
								</div>
								<input type="checkbox" className="toggle" />
							</div>

							<div className="flex items-center justify-between py-3 border-b border-slate-100">
								<div>
									<p className="font-medium text-slate-900">
										Two-Factor Authentication
									</p>
									<p className="text-sm text-slate-500">Add an extra security layer</p>
								</div>
								<button className="text-brand-500 text-sm font-medium">
									Enable
								</button>
							</div>

							<div className="flex items-center justify-between py-3">
								<div>
									<p className="font-medium text-slate-900">Change Password</p>
									<p className="text-sm text-slate-500">Update your password</p>
								</div>
								<button className="text-brand-500 text-sm font-medium">
									Update
								</button>
							</div>
						</div>

						<button
							onClick={() => setShowPrivacyModal(false)}
							className="btn-primary w-full"
						>
							Save Settings
						</button>
					</div>
				</div>
			)}

			{/* Appearance Modal */}
			{showAppearanceModal && (
				<div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
					<div className="card max-w-md w-full p-6 space-y-4">
						<div className="flex items-center justify-between">
							<h3 className="text-lg font-semibold text-slate-900">Appearance</h3>
							<button
								onClick={() => setShowAppearanceModal(false)}
								className="text-slate-400 hover:text-slate-600"
							>
								<i className="ri-close-line text-xl" />
							</button>
						</div>

						<div className="space-y-4">
							<div>
								<p className="font-medium text-slate-900 mb-3">Theme</p>
								<div className="grid grid-cols-3 gap-3">
									<button className="p-4 rounded-xl border-2 border-brand-500 bg-brand-50 flex flex-col items-center gap-2">
										<i className="ri-sun-line text-2xl text-brand-500" />
										<span className="text-sm font-medium text-slate-900">
											Light
										</span>
									</button>
									<button className="p-4 rounded-xl border-2 border-slate-200 hover:border-slate-300 flex flex-col items-center gap-2">
										<i className="ri-moon-line text-2xl text-slate-400" />
										<span className="text-sm font-medium text-slate-600">
											Dark
										</span>
									</button>
									<button className="p-4 rounded-xl border-2 border-slate-200 hover:border-slate-300 flex flex-col items-center gap-2">
										<i className="ri-computer-line text-2xl text-slate-400" />
										<span className="text-sm font-medium text-slate-600">
											System
										</span>
									</button>
								</div>
							</div>

							<div className="pt-4 border-t border-slate-100">
								<p className="font-medium text-slate-900 mb-3">Display</p>
								<div className="flex items-center justify-between py-2">
									<span className="text-slate-700">Compact Mode</span>
									<input type="checkbox" className="toggle" />
								</div>
								<div className="flex items-center justify-between py-2">
									<span className="text-slate-700">Reduce Animations</span>
									<input type="checkbox" className="toggle" />
								</div>
							</div>
						</div>

						<button
							onClick={() => setShowAppearanceModal(false)}
							className="btn-primary w-full"
						>
							Save Preferences
						</button>
					</div>
				</div>
			)}
		</div>
	);
}
