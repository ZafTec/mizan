"use client"

import { toast as sonnerToast } from "sonner"

interface ToastOptions {
	title?: string
	description?: string
	variant?: "default" | "destructive"
	duration?: number
}

export function useToast() {
	const toast = (options: ToastOptions) => {
		const { title, description, variant = "default", duration } = options

		if (variant === "destructive") {
			sonnerToast.error(title, {
				description,
				duration,
			})
		} else {
			sonnerToast(title, {
				description,
				duration,
			})
		}
	}

	return { toast }
}
