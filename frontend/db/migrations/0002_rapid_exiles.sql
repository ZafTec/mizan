ALTER TABLE "users" ADD COLUMN "theme_preference" varchar(20) DEFAULT 'system';--> statement-breakpoint
ALTER TABLE "users" ADD COLUMN "compact_mode" boolean DEFAULT false;--> statement-breakpoint
ALTER TABLE "users" ADD COLUMN "reduce_animations" boolean DEFAULT false;