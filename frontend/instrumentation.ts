export async function register() {
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    const { validateStartupConfig } = await import('./lib/startup-validation');
    
    // Only run validation if we are not in a build phase
    // NEXT_PHASE is confusing, but checking for a known runtime-only var or specific build var helps.
    // However, simply moving this here might be enough as instrumentation is often skipped or limited during build for static pages.
    // To be extra safe, we catch the error and only log it if it looks like a build.
    
    try {
        validateStartupConfig();
    } catch (error) {
        // If we are building, we might ignore this or log a warning instead of crashing
        if (process.env.NEXT_BUILD) {
            console.warn("Skipping startup validation during build");
        } else {
            // Re-throw in production runtime
            throw error;
        }
    }
  }
}
