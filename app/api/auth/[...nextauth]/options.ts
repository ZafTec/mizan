import type { NextAuthOptions } from "next-auth";
import GithubProvider from "next-auth/providers/github";
import CredentialsProvider from "next-auth/providers/credentials";

export const options: NextAuthOptions = {
	providers: [
		GithubProvider({
			clientId: process.env.GITHUB_AUTH_CLIENT_ID as string,
			clientSecret: process.env.GITHUB_AUTH_CLIENT_SECRET as string,
		}),
		CredentialsProvider({
			name: "Credentials",
			credentials: {
				username: {
					label: "Username: ",
					type: "text",
					placeholder: "test",
				},
				password: {
					label: "Password: ",
					type: "password",
					placeholder: "test",
				},
			},
			async authorize(credentials) {
                const username = credentials?.username;
                const password = credentials?.password;

				if (username === "test" && password === "test") {
					// return { id: 1, name: "Test User", email: "test@email.com", password: "test" };
                    return null;
				} else {
					return null;
				}
			},
		})
	],
};
