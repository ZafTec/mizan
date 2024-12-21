import getUser from "@/context/AuthProvider";


export default async function About() {
	const { user } = await getUser();

	return (
		<div>
			<h1>About</h1>
			<p>
				This is a simple recipe manager to help you track your macros.
			</p>
            <pre>
                {JSON.stringify(user, null, 4)}
            </pre>
		</div>
	)
}
