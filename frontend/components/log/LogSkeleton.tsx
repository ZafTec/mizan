export default function LogSkeleton() {
  return (
    <div
      className="log-page space-y-8"
      role="status"
      aria-label="Loading your log"
    >
      <div className="h-10 w-48 rounded bg-muted animate-pulse" />
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-6 border-y py-6">
        {[0, 1, 2, 3].map((item) => (
          <div key={item} className="h-16 bg-muted animate-pulse rounded" />
        ))}
      </div>
      {[0, 1, 2].map((item) => (
        <div key={item} className="h-20 border-b bg-muted/40 animate-pulse" />
      ))}
      <span className="sr-only">Loading your log</span>
    </div>
  );
}
