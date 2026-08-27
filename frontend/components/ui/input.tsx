import { InputHTMLAttributes, forwardRef } from "react";
import { twMerge } from "tailwind-merge";

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  hint?: string;
}

const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ className, label, error, hint, id, ...props }, ref) => {
    const inputId = id || label?.toLowerCase().replace(/\s+/g, "-");

    return (
      <div className="w-full">
        {label && (
          <label
            htmlFor={inputId}
            className="block text-sm font-medium text-charcoal-blue-700 dark:text-charcoal-blue-300 mb-1.5"
          >
            {label}
            {props.required && <span className="text-red-500 ml-1">*</span>}
          </label>
        )}
        <input
          ref={ref}
          id={inputId}
          className={twMerge(
            "w-full rounded-xl border border-charcoal-blue-300 bg-white px-4 py-3 text-charcoal-blue-900 placeholder-charcoal-blue-400 transition-[border-color,box-shadow] duration-150 ease-out",
            "focus:outline-none focus:ring-2 focus:ring-brand-500/15 focus:border-brand-500 dark:border-white/10 dark:bg-charcoal-blue-900 dark:text-charcoal-blue-100 dark:placeholder-charcoal-blue-500",
            error
              ? "border-red-300 focus:ring-red-500/20 focus:border-red-500"
              : "",
            className,
          )}
          {...props}
        />
        {error && (
          <p className="mt-1.5 text-sm text-red-500 flex items-center gap-1">
            <i className="ri-error-warning-line" />
            {error}
          </p>
        )}
        {hint && !error && (
          <p className="mt-1.5 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
            {hint}
          </p>
        )}
      </div>
    );
  },
);

Input.displayName = "Input";

export { Input };
export default Input;
