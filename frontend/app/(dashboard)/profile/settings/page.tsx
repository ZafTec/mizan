"use client";

import { useEffect, useMemo, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CldUploadWidget } from "next-cloudinary";
import Loading from "@/components/Loading";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { authClient, useSession } from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import { downloadProfileExport, getProfileObservations, type ProfileObservations } from "@/lib/api/profile";
import { useTheme } from "@/lib/hooks/useTheme";
import { appToast } from "@/lib/toast";

type SessionItem = {
	id: string;
	token: string;
	ipAddress?: string;
	userAgent?: string;
	createdAt: string;
	expiresAt: string;
};

const DELETE_CONFIRMATION_TEXT = "DELETE";

export default function ProfileSettingsPage() {
	const { data: session, isPending } = useSession();
	const { settings: appearance, updateSettings: updateAppearance, persistSettings } = useTheme();
	const router = useRouter();

	const [name, setName] = useState("");
	const [image, setImage] = useState("");
	const [currentPassword, setCurrentPassword] = useState("");
	const [newPassword, setNewPassword] = useState("");
	const [confirmPassword, setConfirmPassword] = useState("");
	const [deletePassword, setDeletePassword] = useState("");
	const [deleteConfirmation, setDeleteConfirmation] = useState("");
	const [sessions, setSessions] = useState<SessionItem[]>([]);
	const [observations, setObservations] = useState<ProfileObservations | null>(null);
	const [loadingSessions, setLoadingSessions] = useState(true);
	const [loadingObservations, setLoadingObservations] = useState(true);
	const [savingProfile, setSavingProfile] = useState(false);
	const [uploadingAvatar, setUploadingAvatar] = useState(false);
	const [savingAppearance, setSavingAppearance] = useState(false);
	const [changingPassword, setChangingPassword] = useState(false);
	const [exportingData, setExportingData] = useState(false);
	const [deletingAccount, setDeletingAccount] = useState(false);
	const [revoking, setRevoking] = useState<string | null>(null);

	useEffect(() => {
		if (!session?.user) {
			return;
		}

		setName(session.user.name ?? "");
		setImage(session.user.image ?? "");
	}, [session?.user]);

	useEffect(() => {
		if (!session?.user) {
			return;
		}

		void fetchSessions();
		void fetchObservations();
	}, [session?.user]);

	const activeSessions = useMemo(
		() => sessions.filter((activeSession) => new Date(activeSession.expiresAt) > new Date()),
		[sessions]
	);

	if (isPending) {
		return (
			<div className="flex min-h-[60vh] items-center justify-center">
				<Loading />
			</div>
		);
	}

	if (!session?.user) {
		router.push("/login");
		return null;
	}

	const user = session.user;
	const showRoleBadge = Boolean(user.role && user.role !== "user");
	const displayName = name.trim() || user.name || user.email;
	const previewImage = image || user.image || "";
	const hasProfileChanges = name.trim() !== (user.name ?? "") || image.trim() !== (user.image ?? "");

	async function fetchSessions() {
		setLoadingSessions(true);
		try {
			const result = await authClient.listSessions();
			setSessions(
				(result.data ?? []).map((item: any) => ({
					id: item.id,
					token: item.token,
					ipAddress: item.ipAddress || undefined,
					userAgent: item.userAgent || undefined,
					createdAt:
						typeof item.createdAt === "string"
							? item.createdAt
							: new Date(item.createdAt).toISOString(),
					expiresAt:
						typeof item.expiresAt === "string"
							? item.expiresAt
							: new Date(item.expiresAt).toISOString(),
				}))
			);
		} catch (error) {
			console.error("Failed to fetch sessions:", error);
			appToast.error(error, "Failed to load sessions");
		} finally {
			setLoadingSessions(false);
		}
	}

	async function fetchObservations() {
		setLoadingObservations(true);
		try {
			setObservations(await getProfileObservations());
		} catch (error) {
			console.error("Failed to fetch profile observations:", error);
			appToast.error(error, "Failed to load account observations");
		} finally {
			setLoadingObservations(false);
		}
	}

	async function syncProfileDetails({
		nextName,
		nextImage,
	}: {
		nextName?: string | null;
		nextImage?: string | null;
	}) {
		const includeName = nextName !== undefined;
		const includeImage = nextImage !== undefined;
		const trimmedName = nextName?.trim() ?? "";
		const trimmedImage = nextImage?.trim() ?? "";

		await Promise.all([
			authClient.updateUser({
				...(includeName ? { name: trimmedName || undefined } : {}),
				...(includeImage ? { image: trimmedImage || undefined } : {}),
			} as never),
			clientApi("/api/Users/me", {
				method: "PUT",
				body: {
					...(includeName ? { name: trimmedName || null } : {}),
					...(includeImage ? { image: trimmedImage || null } : {}),
				},
			}),
		]);
	}

	async function handleUpdateProfile() {
		setSavingProfile(true);
		try {
			await syncProfileDetails({ nextName: name, nextImage: image });
			appToast.success("Account details updated");
			window.location.reload();
		} catch (error) {
			console.error("Failed to update profile:", error);
			appToast.error(error, "Failed to update account details");
		} finally {
			setSavingProfile(false);
		}
	}

	async function handleAvatarUpload(result: any) {
		const nextImage = typeof result?.info?.secure_url === "string" ? result.info.secure_url : "";
		if (!nextImage) {
			appToast.error("Avatar upload finished but no image URL was returned");
			return;
		}

		const previousImage = image;
		setImage(nextImage);
		setUploadingAvatar(true);

		try {
			await syncProfileDetails({ nextImage });
			router.refresh();
			appToast.success("Avatar updated");
		} catch (error) {
			setImage(previousImage);
			console.error("Failed to persist uploaded avatar:", error);
			appToast.error(error, "Failed to save uploaded avatar");
		} finally {
			setUploadingAvatar(false);
		}
	}

	async function handleSaveAppearance() {
		setSavingAppearance(true);
		try {
			await persistSettings();
			router.refresh();
			appToast.success("Appearance preferences saved");
		} catch (error) {
			console.error("Failed to save appearance:", error);
			appToast.error(error, "Failed to save appearance preferences");
		} finally {
			setSavingAppearance(false);
		}
	}

	async function handleChangePassword() {
		if (newPassword !== confirmPassword) {
			appToast.error("Passwords do not match");
			return;
		}

		if (newPassword.length < 8) {
			appToast.error("Password must be at least 8 characters");
			return;
		}

		setChangingPassword(true);
		try {
			await authClient.changePassword({
				currentPassword,
				newPassword,
				revokeOtherSessions: true,
			});

			setCurrentPassword("");
			setNewPassword("");
			setConfirmPassword("");
			appToast.success("Password updated. Other sessions were revoked.");
			await fetchSessions();
		} catch (error) {
			console.error("Failed to change password:", error);
			appToast.error(error, "Password change failed. Check your current password.");
		} finally {
			setChangingPassword(false);
		}
	}

	async function handleRevokeSession(sessionToken: string) {
		setRevoking(sessionToken);
		try {
			await authClient.revokeSession({ token: sessionToken });
			setSessions((current) => current.filter((item) => item.token !== sessionToken));
			appToast.success("Session revoked");
		} catch (error) {
			console.error("Failed to revoke session:", error);
			appToast.error(error, "Failed to revoke session");
		} finally {
			setRevoking(null);
		}
	}

	async function handleRevokeAllOtherSessions() {
		setRevoking("all");
		try {
			await authClient.revokeSessions();
			appToast.success("Other sessions revoked");
			await fetchSessions();
		} catch (error) {
			console.error("Failed to revoke all other sessions:", error);
			appToast.error(error, "Failed to revoke other sessions");
		} finally {
			setRevoking(null);
		}
	}

	async function handleExportData() {
		setExportingData(true);
		try {
			const { blob, filename } = await downloadProfileExport();
			const objectUrl = URL.createObjectURL(blob);
			const anchor = document.createElement("a");
			anchor.href = objectUrl;
			anchor.download = filename;
			document.body.appendChild(anchor);
			anchor.click();
			anchor.remove();
			URL.revokeObjectURL(objectUrl);
			appToast.success("Your export is downloading");
		} catch (error) {
			console.error("Failed to export profile data:", error);
			appToast.error(error, "Failed to export your data");
		} finally {
			setExportingData(false);
		}
	}

	async function handleDeleteAccount() {
		if (deleteConfirmation !== DELETE_CONFIRMATION_TEXT) {
			appToast.error(`Type ${DELETE_CONFIRMATION_TEXT} to confirm account deletion`);
			return;
		}

		setDeletingAccount(true);
		try {
			await authClient.deleteUser(
				deletePassword.trim()
					? { password: deletePassword.trim(), callbackURL: "/" }
					: { callbackURL: "/" }
			);
			window.location.href = "/";
		} catch (error) {
			console.error("Failed to delete account:", error);
			appToast.error(error, "Account deletion failed. A fresh session or valid password may be required.");
		} finally {
			setDeletingAccount(false);
		}
	}

	return (
		<div className="mx-auto max-w-5xl space-y-6">
			<div className="surface-panel p-6 sm:p-8">
				<div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
					<div className="flex items-center gap-4">
						<AvatarPreview image={previewImage} email={user.email} name={displayName} />
						<div>
							<p className="eyebrow mb-3">
								<AnimatedIcon name="user" size={14} aria-hidden="true" />
								Settings center
							</p>
							<h1 className="text-3xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								{displayName}
							</h1>
							<p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Manage your account, app preferences, sessions, and exported data.
							</p>
						</div>
					</div>

					<div className={`grid gap-3 ${showRoleBadge ? "sm:grid-cols-3" : "sm:grid-cols-2"}`}>
						{showRoleBadge ? <SummaryBadge label="Role" value={user.role || "user"} /> : null}
						<SummaryBadge
							label="Active sessions"
							value={loadingSessions ? "--" : String(activeSessions.length)}
						/>
						<SummaryBadge
							label="Streak"
							value={loadingObservations || !observations ? "--" : `${observations.streakCount} days`}
						/>
					</div>
				</div>
			</div>

			<div className="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
				<div className="space-y-6">
					<section className="card p-6">
						<SectionHeading
							icon="user"
							title="Account details"
							description="Keep your profile details up to date everywhere you use them."
						/>

						<div className="mt-6 grid gap-6 lg:grid-cols-[auto_1fr]">
							<div className="space-y-3">
								<AvatarPreview image={previewImage} email={user.email} name={name || user.name} size="lg" />
								<CldUploadWidget
									signatureEndpoint="/api/sign-cloudinary-params"
									onSuccess={(result: any) => void handleAvatarUpload(result)}
								>
									{({ open }) => (
										<button type="button" onClick={() => open()} disabled={uploadingAvatar} className="btn-secondary w-full justify-center">
											<AnimatedIcon name="upload" size={16} aria-hidden="true" />
											{uploadingAvatar ? "Saving avatar..." : "Upload avatar"}
										</button>
									)}
								</CldUploadWidget>
							</div>

							<div className="grid gap-4">
								<Field label="Display name">
									<input
										type="text"
										value={name}
										onChange={(event) => setName(event.target.value)}
										className="input"
										placeholder="Enter your display name"
									/>
								</Field>

								<Field label="Email address" helper="Email changes are still not implemented.">
									<input type="email" value={user.email} disabled className="input cursor-not-allowed bg-charcoal-blue-50 dark:bg-charcoal-blue-900" />
								</Field>

								<Field label="Avatar URL" helper="You can paste a URL or use the upload button.">
									<input
										type="text"
										value={image}
										onChange={(event) => setImage(event.target.value)}
										className="input"
										placeholder="https://..."
									/>
								</Field>

								<div className="flex flex-wrap gap-3">
									<button onClick={handleUpdateProfile} disabled={savingProfile || uploadingAvatar || !hasProfileChanges} className="btn-primary">
										{savingProfile ? "Saving..." : "Save account changes"}
									</button>
									<Link href="/profile" className="btn-secondary">
										Back to profile
									</Link>
								</div>
							</div>
						</div>
					</section>

					<section className="card p-6">
						<SectionHeading
							icon="lock"
							title="Security"
							description="Update your password and manage sign-in security."
						/>

						<div className="mt-6 grid gap-4">
							<Field label="Current password">
								<input type="password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} className="input" />
							</Field>
							<Field label="New password" helper="Minimum 8 characters.">
								<input type="password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} className="input" />
							</Field>
							<Field label="Confirm new password">
								<input type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} className="input" />
							</Field>
							<div className="rounded-3xl border border-charcoal-blue-200 bg-charcoal-blue-50/90 p-4 text-sm text-charcoal-blue-600 dark:border-white/10 dark:bg-charcoal-blue-900/70 dark:text-charcoal-blue-300">
								Changing your password revokes other active sessions.
							</div>
							<button
								onClick={handleChangePassword}
								disabled={changingPassword || !currentPassword || !newPassword || !confirmPassword}
								className="btn-primary w-full justify-center sm:w-auto"
							>
								{changingPassword ? "Updating..." : "Change password"}
							</button>
						</div>
					</section>

					<section className="card p-6">
						<SectionHeading
							icon="sparkles"
							title="Appearance"
							description="Preferences sync to your account and apply across sessions."
						/>

						<div className="mt-6 space-y-6">
							<div className="grid gap-3 sm:grid-cols-3">
								{([
									{ value: "light", label: "Light", icon: "sun" },
									{ value: "dark", label: "Dark", icon: "moon" },
									{ value: "system", label: "System", icon: "home" },
								] as const).map((option) => (
									<button
										key={option.value}
										onClick={() => updateAppearance({ theme: option.value })}
										className={`rounded-3xl border p-4 text-left transition-colors ${
											appearance.theme === option.value
												? "border-brand-300 bg-brand-50/80 text-brand-900 dark:border-brand-500/30 dark:bg-brand-500/10 dark:text-brand-200"
												: "border-charcoal-blue-200 bg-white text-charcoal-blue-700 hover:border-charcoal-blue-300 dark:border-white/10 dark:bg-charcoal-blue-950 dark:text-charcoal-blue-300 dark:hover:border-white/20"
										}`}
									>
										<div className="flex items-center gap-3">
											<span className="icon-chip h-10 w-10 text-current">
												<AnimatedIcon name={option.icon} size={18} aria-hidden="true" />
											</span>
											<div>
												<p className="font-medium">{option.label}</p>
												<p className="text-xs opacity-70">{option.value === "system" ? "Follow OS preference" : `${option.label} mode always`}</p>
											</div>
										</div>
									</button>
								))}
							</div>

							<div className="grid gap-3 sm:grid-cols-2">
								<ToggleCard
									label="Compact mode"
									description="Tighten spacing for denser layouts."
									checked={appearance.compactMode}
									onChange={(checked) => updateAppearance({ compactMode: checked })}
								/>
								<ToggleCard
									label="Reduce animations"
									description="Keep motion subdued across the app."
									checked={appearance.reduceAnimations}
									onChange={(checked) => updateAppearance({ reduceAnimations: checked })}
								/>
							</div>

							<button onClick={handleSaveAppearance} disabled={savingAppearance} className="btn-primary w-full justify-center sm:w-auto">
								{savingAppearance ? "Saving..." : "Save appearance"}
							</button>
						</div>
					</section>
				</div>

				<div className="space-y-6">
					<section className="card p-6">
						<SectionHeading
							icon="activity"
							title="Usage observations"
							description="Computed signals from your nutrition, progress, and tool activity."
						/>

						{loadingObservations || !observations ? (
							<div className="mt-6 flex min-h-40 items-center justify-center">
								<Loading />
							</div>
						) : (
							<div className="mt-6 grid gap-3 sm:grid-cols-2">
								<ObservationCard label="Joined" value={formatDate(observations.joinedAt)} helper="Account age" />
								<ObservationCard label="Current streak" value={`${observations.streakCount} days`} helper={`Longest: ${observations.longestStreak} days`} />
								<ObservationCard label="Meal logging" value={`${observations.mealLoggingDays}/14 days`} helper={`Average ${observations.averageCalories} kcal on logged days`} />
								<ObservationCard label="Achievements" value={`${observations.achievementCount}`} helper={`${observations.totalAchievementPoints} total points`} />
								<ObservationCard label="MCP calls" value={`${observations.mcpCalls}`} helper={`${observations.mcpSuccessRate}% success rate`} />
								<ObservationCard label="Active goal" value={observations.goalSummary} helper="Based on your saved nutrition targets" />
							</div>
						)}
					</section>

					<section className="card p-6">
						<SectionHeading
							icon="users"
							title="Sessions"
							description="Revoke devices without leaving this page."
						/>

						{loadingSessions ? (
							<div className="mt-6 flex min-h-40 items-center justify-center">
								<Loading />
							</div>
						) : (
							<div className="mt-6 space-y-4">
								<div className="flex flex-wrap items-center justify-between gap-3 rounded-3xl border border-charcoal-blue-200 bg-charcoal-blue-50/90 p-4 dark:border-white/10 dark:bg-charcoal-blue-900/70">
									<div>
										<p className="text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">{activeSessions.length} active session{activeSessions.length === 1 ? "" : "s"}</p>
										<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">Current device remains signed in when revoking others.</p>
									</div>
									{activeSessions.length > 1 ? (
										<button onClick={handleRevokeAllOtherSessions} disabled={revoking === "all"} className="btn-secondary">
											{revoking === "all" ? "Revoking..." : "Revoke other sessions"}
										</button>
									) : null}
								</div>

								<div className="space-y-3">
									{activeSessions.map((activeSession) => {
										const isCurrent = activeSession.token === session.session?.token;
										return (
											<div key={activeSession.id} className={`rounded-3xl border p-4 ${isCurrent ? "border-brand-300 bg-brand-50/80 dark:border-brand-500/30 dark:bg-brand-500/10" : "border-charcoal-blue-200 bg-white dark:border-white/10 dark:bg-charcoal-blue-950"}`}>
												<div className="flex items-start justify-between gap-4">
													<div className="min-w-0">
														<div className="flex items-center gap-2">
															<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">{getDeviceInfo(activeSession.userAgent)}</p>
															{isCurrent ? <span className="rounded-full bg-brand-100 px-2 py-0.5 text-xs font-medium text-brand-800 dark:bg-brand-500/20 dark:text-brand-200">Current</span> : null}
														</div>
														<p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{activeSession.ipAddress || "Unknown IP"} - Started {formatDateTime(activeSession.createdAt)}</p>
														<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">Expires {formatDateTime(activeSession.expiresAt)}</p>
													</div>
													{!isCurrent ? (
														<button onClick={() => handleRevokeSession(activeSession.token)} disabled={revoking === activeSession.token} className="rounded-full border border-red-200 px-3 py-1.5 text-sm font-medium text-red-600 transition-colors hover:bg-red-50 disabled:opacity-50 dark:border-red-500/20 dark:hover:bg-red-500/10">
															{revoking === activeSession.token ? "Revoking..." : "Revoke"}
														</button>
													) : null}
												</div>
											</div>
										);
									})}
								</div>
							</div>
						)}
					</section>

					<section className="card p-6">
						<SectionHeading
							icon="upload"
							title="Export data"
							description="Download a JSON export of your stored data."
						/>

						<div className="mt-6 space-y-4 rounded-3xl border border-charcoal-blue-200 bg-charcoal-blue-50/90 p-4 dark:border-white/10 dark:bg-charcoal-blue-900/70">
							<p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
								The export includes your account profile, goals, meals, meal plans, measurements, workouts, achievements, recipes, favorites, and MCP usage metadata.
							</p>
							<button onClick={handleExportData} disabled={exportingData} className="btn-primary w-full justify-center">
								{exportingData ? "Preparing export..." : "Download my data"}
							</button>
						</div>
					</section>

					<section className="card border-red-200 p-6 dark:border-red-500/20">
						<SectionHeading
							icon="badgeAlert"
							title="Danger zone"
							description="Delete your account permanently."
						/>

						<div className="mt-6 space-y-4 rounded-3xl border border-red-200 bg-red-50/70 p-4 dark:border-red-500/20 dark:bg-red-500/10">
							<Field label={`Type ${DELETE_CONFIRMATION_TEXT} to confirm`}>
								<input type="text" value={deleteConfirmation} onChange={(event) => setDeleteConfirmation(event.target.value)} className="input" placeholder={DELETE_CONFIRMATION_TEXT} />
							</Field>
							<Field label="Password (optional)" helper="Use this if password confirmation is required instead of a fresh session.">
								<input type="password" value={deletePassword} onChange={(event) => setDeletePassword(event.target.value)} className="input" />
							</Field>
							<button onClick={handleDeleteAccount} disabled={deletingAccount || deleteConfirmation !== DELETE_CONFIRMATION_TEXT} className="w-full rounded-full bg-red-600 px-4 py-3 text-sm font-medium text-white transition-colors hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60">
								{deletingAccount ? "Deleting account..." : "Delete account permanently"}
							</button>
						</div>
					</section>
				</div>
			</div>
		</div>
	);
}

function SectionHeading({
	icon,
	title,
	description,
}: {
	icon: Parameters<typeof AnimatedIcon>[0]["name"];
	title: string;
	description: string;
}) {
	return (
		<div className="flex items-start gap-3">
			<span className="icon-chip h-11 w-11 text-brand-600 dark:text-brand-300">
				<AnimatedIcon name={icon} size={18} aria-hidden="true" />
			</span>
			<div>
				<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{title}</h2>
				<p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{description}</p>
			</div>
		</div>
	);
}

function SummaryBadge({ label, value }: { label: string; value: string }) {
	return (
		<div className="rounded-3xl border border-charcoal-blue-200 bg-white/90 px-4 py-3 dark:border-white/10 dark:bg-charcoal-blue-950/70">
			<p className="text-xs font-semibold uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">{label}</p>
			<p className="mt-2 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{value}</p>
		</div>
	);
}

function ObservationCard({
	label,
	value,
	helper,
}: {
	label: string;
	value: string;
	helper: string;
}) {
	return (
		<div className="rounded-3xl border border-charcoal-blue-200 bg-charcoal-blue-50/90 p-4 dark:border-white/10 dark:bg-charcoal-blue-900/70">
			<p className="text-xs font-semibold uppercase tracking-[0.14em] text-charcoal-blue-500 dark:text-charcoal-blue-400">{label}</p>
			<p className="mt-3 text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">{value}</p>
			<p className="mt-2 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{helper}</p>
		</div>
	);
}

function ToggleCard({
	label,
	description,
	checked,
	onChange,
}: {
	label: string;
	description: string;
	checked: boolean;
	onChange: (checked: boolean) => void;
}) {
	return (
		<button
			type="button"
			onClick={() => onChange(!checked)}
			className="flex items-center justify-between rounded-3xl border border-charcoal-blue-200 bg-white p-4 text-left transition-colors hover:border-charcoal-blue-300 dark:border-white/10 dark:bg-charcoal-blue-950 dark:hover:border-white/20"
		>
			<div>
				<p className="font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100">{label}</p>
				<p className="mt-1 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">{description}</p>
			</div>
			<span className={`relative h-6 w-11 rounded-full transition-colors ${checked ? "bg-brand-600" : "bg-charcoal-blue-300 dark:bg-charcoal-blue-700"}`}>
				<span className={`absolute top-0.5 h-5 w-5 rounded-full bg-white transition-transform ${checked ? "translate-x-5" : "translate-x-0.5"}`} />
			</span>
		</button>
	);
}

function Field({
	label,
	helper,
	children,
}: {
	label: string;
	helper?: string;
	children: React.ReactNode;
}) {
	return (
		<label className="grid gap-2">
			<span className="text-sm font-medium text-charcoal-blue-700 dark:text-charcoal-blue-300">{label}</span>
			{children}
			{helper ? <span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">{helper}</span> : null}
		</label>
	);
}

function AvatarPreview({
	image,
	email,
	name,
	size = "md",
}: {
	image?: string;
	email?: string | null;
	name?: string | null;
	size?: "md" | "lg";
}) {
	const dimension = size === "lg" ? 96 : 72;

	if (image) {
		return (
			<div className="relative overflow-hidden rounded-[28px] ring-1 ring-brand-500/20" style={{ width: dimension, height: dimension }}>
				<Image src={image} alt={name || email || "User"} fill sizes={`${dimension}px`} className="object-cover" />
			</div>
		);
	}

	return (
		<div
			className="flex items-center justify-center rounded-[28px] bg-brand-600 font-semibold text-white ring-1 ring-brand-500/20 dark:bg-brand-500"
			style={{ width: dimension, height: dimension }}
		>
			{(email || "U").charAt(0).toUpperCase()}
		</div>
	);
}

function getDeviceInfo(userAgent?: string) {
	if (!userAgent) return "Unknown device";
	const ua = userAgent.toLowerCase();
	let browser = "Unknown browser";
	let os = "Unknown OS";

	if (ua.includes("chrome") && !ua.includes("edge")) browser = "Chrome";
	else if (ua.includes("firefox")) browser = "Firefox";
	else if (ua.includes("safari") && !ua.includes("chrome")) browser = "Safari";
	else if (ua.includes("edge")) browser = "Edge";

	if (ua.includes("windows")) os = "Windows";
	else if (ua.includes("mac")) os = "macOS";
	else if (ua.includes("linux")) os = "Linux";
	else if (ua.includes("android")) os = "Android";
	else if (ua.includes("ios") || ua.includes("iphone") || ua.includes("ipad")) os = "iOS";

	return `${browser} on ${os}`;
}

function formatDate(value?: string | null) {
	if (!value) return "-";
	return new Date(value).toLocaleDateString(undefined, {
		year: "numeric",
		month: "short",
		day: "numeric",
	});
}

function formatDateTime(value?: string | null) {
	if (!value) return "-";
	return new Date(value).toLocaleString();
}
