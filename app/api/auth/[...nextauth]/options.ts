import type { NextAuthOptions } from "next-auth";
import GithubProvider from "next-auth/providers/github";
import CredentialsProvider from "next-auth/providers/credentials";
import { getUserByEmail } from "@/data/user";

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
				},
				password: {
					label: "Password: ",
					type: "password",
				},
			},
			async authorize(credentials) {
				const username = credentials?.username;
				const password = credentials?.password;

				if (!username || !password) {
					return null;
				}

				const user = await getUserByEmail(username);
				// console.log(user);

				// TODO: Add password hashing and verification

				if (user) {
					return {
						...user,
						id: typeof user.id === 'string' ? user.id : user.id.toString(),
					};
				} else {
					return null;
				}
			},
		})
	],
};
