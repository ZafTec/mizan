"use client";

import { useState, useMemo } from "react";

interface PasswordInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
	showStrength?: boolean;
}

function getStrength(password: string): { label: string; level: number; color: string } {
	if (!password) return { label: "", level: 0, color: "" };
	let score = 0;
	if (password.length >= 8) score++;
	if (password.length >= 12) score++;
	if (/[A-Z]/.test(password)) score++;
	if (/[0-9]/.test(password)) score++;
	if (/[^A-Za-z0-9]/.test(password)) score++;

	if (score <= 2) return { label: "Weak", level: 1, color: "bg-red-500" };
	if (score <= 3) return { label: "Medium", level: 2, color: "bg-amber-500" };
	return { label: "Strong", level: 3, color: "bg-green-500" };
}

export function PasswordInput({ showStrength, className, ...props }: PasswordInputProps) {
	const [visible, setVisible] = useState(false);
	const value = typeof props.value === "string" ? props.value : "";
	const strength = useMemo(() => (showStrength ? getStrength(value) : null), [showStrength, value]);

	return (
		<div>
			<div className="relative">
				<input
					{...props}
					type={visible ? "text" : "password"}
					className={className || "input pr-10"}
				/>
				<button
					type="button"
					data-testid="password-toggle"
					onClick={() => setVisible(!visible)}
					className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 transition-colors"
					tabIndex={-1}
				>
					<i className={visible ? "ri-eye-off-line" : "ri-eye-line"} />
				</button>
			</div>
			{strength && value && (
				<div className="mt-2 flex items-center gap-2">
					<div className="flex gap-1 flex-1">
						{[1, 2, 3].map((i) => (
							<div
								key={i}
								className={`h-1 flex-1 rounded-full transition-colors ${
									i <= strength.level ? strength.color : "bg-slate-200 dark:bg-slate-700"
								}`}
							/>
						))}
					</div>
					<span className="text-xs text-slate-500 dark:text-slate-400">{strength.label}</span>
				</div>
			)}
		</div>
	);
}
