ALTER TABLE "sessions" ALTER COLUMN "created_at" SET NOT NULL;
ALTER TABLE "sessions" ADD COLUMN "updated_at" timestamp DEFAULT now() NOT NULL;