import loaderStyle from "./index.module.css"


export default function Loading() {
    return (
        <div className={loaderStyle.loader}>
            <span></span>
            <span></span>
            <span></span>
            <span></span>
            <span></span>
            <span></span>
        </div>
    );
}
