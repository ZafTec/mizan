DROP TABLE IF EXISTS "rateLimit";
--> statement-breakpoint
CREATE TABLE "rateLimit" (
	"id" varchar(255) PRIMARY KEY NOT NULL,
	"key" varchar(255) NOT NULL,
	"count" integer NOT NULL,
	"last_request" integer NOT NULL
);
