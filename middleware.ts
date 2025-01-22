
import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'

const publicPaths = ['/', '/login', '/signup', '/verifyemail']

export function middleware(request: NextRequest) {
	const path = request.nextUrl.pathname
	const callbackUrl = request.nextUrl.searchParams.get('callbackUrl')

	// Define paths that are considered public (accessible without a token)
	const isPublicPath = publicPaths.includes(path)

	// Get the token from the cookies
	const token = request.cookies.get('auth_token')?.value || ''

	// Redirect logic based on the path and token presence
	// if(isPublicPath && token) {

	// 	// If trying to access a public path with a token, redirect to the home page
	// 	return NextResponse.redirect(new URL('/', request.nextUrl))
	// }

	// If trying to access a protected path without a token, redirect to the login page
	if (!isPublicPath && !token) {
		return NextResponse.redirect(new URL('/login?callbackUrl=' + path + (callbackUrl ? '&callbackUrl=' + callbackUrl : ''), request.nextUrl))
	}
		
}

// It specifies the paths for which this middleware should be executed. 
// In this case, it's applied to '/', '/profile', '/login', and '/signup'.
export const config = {
  matcher: [
	'/',
	'/profile',
	'/login',
	'/signup',
	'/verifyemail'
  ]
}
