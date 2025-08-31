import { createRootRoute } from '@tanstack/react-router'
import App from "../components/app.tsx";

export const Route = createRootRoute({
    component: () => (
        <>
            <App />
            {/*<div className="p-2 flex gap-2">*/}
            {/*    <Link to="/" className="[&.active]:font-bold">*/}
            {/*        Home*/}
            {/*    </Link>{' '}*/}
            {/*    <Link to="/about" className="[&.active]:font-bold">*/}
            {/*        About*/}
            {/*    </Link>*/}
            {/*</div>*/}
            {/*<hr />*/}
        </>
    ),
})
