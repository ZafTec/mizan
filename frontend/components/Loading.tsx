interface LoadingProps {
  size?: "sm" | "md" | "lg";
}

export default function Loading({ size = "lg" }: LoadingProps) {
  const sizeClasses = {
    sm: "h-4 w-4",
    md: "h-8 w-8",
    lg: "h-12 w-12",
  };

  return (
    <div
      className={`animate-spin rounded-full border-b-2 border-brand-500 ${sizeClasses[size]}`}
    />
  );
}
