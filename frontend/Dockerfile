# syntax=docker/dockerfile:1

# Frontend Development Dockerfile
FROM oven/bun:1-alpine AS base
WORKDIR /app

# Install dependencies with cache
FROM base AS deps
COPY package.json bun.lock ./
RUN --mount=type=cache,target=/root/.bun/install/cache \
    bun install --frozen-lockfile

# Copy source and run dev server
FROM base AS dev
COPY --from=deps /app/node_modules ./node_modules
COPY . .

EXPOSE 3000

ENV PORT=3000
ENV HOSTNAME="0.0.0.0"

CMD ["bun", "run", "dev"]
