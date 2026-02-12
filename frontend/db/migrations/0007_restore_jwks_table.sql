CREATE TABLE IF NOT EXISTS "jwks" (
  "id" text PRIMARY KEY,
  "public_key" text NOT NULL,
  "private_key" text NOT NULL,
  "created_at" timestamp with time zone DEFAULT now() NOT NULL,
  "expires_at" timestamp with time zone,
  "alg" varchar(16),
  "crv" varchar(16)
);

ALTER TABLE "jwks" ADD COLUMN IF NOT EXISTS "expires_at" timestamp with time zone;
ALTER TABLE "jwks" ADD COLUMN IF NOT EXISTS "alg" varchar(16);
ALTER TABLE "jwks" ADD COLUMN IF NOT EXISTS "crv" varchar(16);
