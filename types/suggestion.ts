import { ID } from "./id";
import Recipe from "./recipe";
import User from "./user";

export default interface Suggestion {
  _id: ID;
  user: User;
  recipes: Recipe[];
  createdAt: Date;
  updatedAt: Date;
}
