import mongoose from "mongoose";


let client: typeof mongoose;

export default async function MongoDBClient() {

	if (client) {
		return client;
	}

	if (!process.env.MONGODB_URI || !process.env.MONGODB_DB) {
		throw new Error("Please add your Mongo URI to .env.local");
	}
	
	client = await mongoose.connect(process.env.MONGODB_URI, { dbName: process.env.MONGODB_DB });
	
	return client;
}
