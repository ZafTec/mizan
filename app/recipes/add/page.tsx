
export default function Page() {
    return (
        <div>
            <h1>Add Recipe</h1>
            <form>
                <label htmlFor="name">Name</label>
                <input type="text" id="name" name="name" />
                <label htmlFor="description">Description</label>
                <textarea id="description" name="description" />
                <button type="submit">Add Recipe</button>
            </form>
        </div>
    )
}
