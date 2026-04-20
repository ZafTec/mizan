import * as React from "react"
import { Slot } from "@radix-ui/react-slot"
import { cn, cva } from "@/lib/utils"

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-2xl text-sm font-semibold transition-all duration-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/25 focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default:
          "bg-brand-600 text-white shadow-lg shadow-brand-500/20 hover:-translate-y-0.5 hover:bg-brand-700 hover:shadow-xl hover:shadow-brand-500/20 dark:bg-brand-500 dark:hover:bg-brand-400",
        destructive:
          "bg-red-600 text-white shadow-lg shadow-red-500/20 hover:-translate-y-0.5 hover:bg-red-700",
        outline:
          "border border-charcoal-blue-300 bg-white text-charcoal-blue-700 shadow-sm hover:-translate-y-0.5 hover:bg-charcoal-blue-50 hover:border-charcoal-blue-400 dark:border-white/10 dark:bg-charcoal-blue-950/75 dark:text-charcoal-blue-200 dark:hover:bg-charcoal-blue-900 dark:hover:border-white/20",
        secondary:
          "border border-charcoal-blue-200 bg-secondary text-secondary-foreground shadow-sm hover:-translate-y-0.5 hover:bg-charcoal-blue-100 hover:border-charcoal-blue-300 dark:border-white/10 dark:hover:border-white/15 dark:hover:bg-white/5",
        ghost:
          "border border-charcoal-blue-200 text-charcoal-blue-600 hover:bg-charcoal-blue-50 hover:border-charcoal-blue-300 hover:text-charcoal-blue-900 dark:border-white/10 dark:text-charcoal-blue-300 dark:hover:bg-white/5 dark:hover:border-white/15 dark:hover:text-white",
        link: "text-primary underline-offset-4 hover:underline",
      },
      size: {
        default: "h-11 px-4 py-2",
        sm: "h-9 px-3 text-xs",
        lg: "h-12 px-6 text-base",
        icon: "h-11 w-11",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  asChild?: boolean
  variant?: "default" | "destructive" | "outline" | "secondary" | "ghost" | "link"
  size?: "default" | "sm" | "lg" | "icon"
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button"
    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        {...props}
      />
    )
  }
)
Button.displayName = "Button"

export { Button, buttonVariants }
