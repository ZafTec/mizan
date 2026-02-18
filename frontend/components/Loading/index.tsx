import loaderStyle from "./index.module.css"

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

export default function Loading({ size = "lg", className, color }: LoadingProps) {
    const { size: resolvedSize, gap } = sizeMap[size];
    return (
        <div
            data-testid="loading"
            className={`${loaderStyle.loader} ${className ? ` ${className}` : ""}`}
            style={{
                "--size": resolvedSize,
                "--gap": gap,
                ...(color ? { "--color": color } : {}),
            } as React.CSSProperties}
        >
            <span></span>
            <span></span>
            <span></span>
            <span></span>
            <span></span>
            <span></span>
        </div>
    );
}
