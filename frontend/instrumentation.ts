export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    const { validateStartupConfig } = await import("./lib/startup-validation");

    try {
      validateStartupConfig();
    } catch (error) {
      if (process.env.NEXT_BUILD) {
        console.warn("Skipping startup validation during build");
      } else {
        throw error;
      }
    }
  }
}
