import { NextResponse } from 'next/server';

/**
 * Public configuration endpoint
 * Returns runtime environment variables that are safe to expose to the client
 */
export async function GET() {
  return NextResponse.json({
    cloudinary: {
      cloudName: process.env.NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME || '',
      apiKey: process.env.NEXT_PUBLIC_CLOUDINARY_API_KEY || '',
    },
    appUrl: process.env.NEXT_PUBLIC_APP_URL || '',
    apiUrl: process.env.NEXT_PUBLIC_API_URL || '',
  });
}

export const dynamic = 'force-dynamic';
