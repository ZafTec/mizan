DROP TABLE IF EXISTS "rateLimit";
--> statement-breakpoint
CREATE TABLE "rateLimit" (
	"key" varchar(255) PRIMARY KEY NOT NULL,
	"request" integer NOT NULL,
	"expires_at" timestamp with time zone NOT NULL
);
