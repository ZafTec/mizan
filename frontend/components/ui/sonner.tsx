"use client"

import { Toaster as Sonner } from "sonner"

type ToasterProps = React.ComponentProps<typeof Sonner>

const Toaster = ({ ...props }: ToasterProps) => {
  return (
    <Sonner
      theme="system"
      className="toaster group"
      toastOptions={{
        classNames: {
          toast:
            "group toast rounded-2xl border border-border bg-card text-card-foreground shadow-[var(--shadow-panel)] backdrop-blur-xl",
          title: "text-sm font-semibold text-card-foreground",
          description: "group-[.toast]:text-muted-foreground",
          actionButton:
            "group-[.toast]:bg-primary group-[.toast]:text-primary-foreground",
          cancelButton:
            "group-[.toast]:bg-muted group-[.toast]:text-muted-foreground",
          success:
            "border-brand-500/20 bg-brand-50 text-brand-900 dark:border-brand-500/25 dark:bg-brand-950/70 dark:text-brand-100",
          info:
            "border-accent-500/20 bg-accent-50 text-accent-900 dark:border-accent-500/25 dark:bg-accent-950/70 dark:text-accent-100",
          warning:
            "border-yellow-500/20 bg-yellow-50 text-yellow-900 dark:border-yellow-500/25 dark:bg-yellow-950/70 dark:text-yellow-100",
          error:
            "border-red-500/20 bg-red-50 text-red-900 dark:border-red-500/25 dark:bg-red-950/70 dark:text-red-100",
        },
      }}
      {...props}
    />
  )
}

export { Toaster }
