#!/bin/sh
set -e

# Run migrations only if explicitly enabled
if [ "$RUN_DB_MIGRATIONS" = "true" ]; then
  echo "RUN_DB_MIGRATIONS is true, running database migrations..."
  bunx drizzle-kit migrate
  echo "Migrations completed successfully!"
else
  echo "RUN_DB_MIGRATIONS is not set to 'true', skipping database migrations."
fi

echo "Starting server..."
exec bun server.js
