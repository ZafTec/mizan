interface LoadingProps {
  size?: "sm" | "md" | "lg";
  className?: string;
  color?: string;
}

const sizeMap = {
  sm: { size: "18px", gap: "2px" },
  md: { size: "40px", gap: "4px" },
  lg: { size: "70px", gap: "5px" },
};

const DELAYS = [0, 200, 300, 400, 500, 600];

export default function Loading({
  size = "lg",
  className,
  color,
}: LoadingProps) {
  const { size: resolvedSize, gap } = sizeMap[size];
  return (
    <div
      data-testid="loading"
      className={`grid size-(--size) grid-cols-3 gap-(--gap) [--color:oklch(0.55_0.16_155)] ${className ?? ""}`}
      style={
        {
          "--size": resolvedSize,
          "--gap": gap,
          ...(color ? { "--color": color } : {}),
        } as React.CSSProperties
      }
    >
      {DELAYS.map((delay) => (
        <span
          key={delay}
          className="size-full animate-loader-blink bg-(--color)"
          style={{ animationDelay: `${delay}ms` }}
        />
      ))}
    </div>
  );
}
