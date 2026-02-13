#!/bin/sh
set -e

# Run migrations only if explicitly enabled
if [ "$RUN_DB_MIGRATIONS" = "true" ]; then
  echo "RUN_DB_MIGRATIONS is true, running database migrations..."
  
  # Retry logic: try 3 times, waiting 10 seconds between attempts
  retry_count=0
  max_retries=3
  retry_delay=10
  
  while [ $retry_count -lt $max_retries ]; do
    if bunx drizzle-kit migrate; then
      echo "Migrations completed successfully!"
      break
    else
      retry_count=$((retry_count + 1))
      if [ $retry_count -lt $max_retries ]; then
        echo "Migration attempt $retry_count failed. Retrying in ${retry_delay} seconds..."
        sleep $retry_delay
      else
        echo "ERROR: Migration failed after $max_retries attempts. Exiting."
        exit 1
      fi
    fi
  done
else
  echo "RUN_DB_MIGRATIONS is not set to 'true', skipping database migrations."
fi

echo "Starting server..."
exec bun server.js
