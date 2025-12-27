import { NextResponse } from 'next/server';

/**
 * Public configuration endpoint
 * Returns runtime environment variables that are safe to expose to the client
 */
export async function GET() {
  // Use non-NEXT_PUBLIC_ vars for runtime access in standalone builds
  // Fallback to NEXT_PUBLIC_ vars for backward compatibility
  return NextResponse.json({
    cloudinary: {
      cloudName: process.env.CLOUDINARY_CLOUD_NAME || process.env.NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME || '',
      apiKey: process.env.CLOUDINARY_API_KEY || process.env.NEXT_PUBLIC_CLOUDINARY_API_KEY || '',
    },
    appUrl: process.env.APP_URL || process.env.NEXT_PUBLIC_APP_URL || '',
    apiUrl: process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || '',
  });
}

export const dynamic = 'force-dynamic';
