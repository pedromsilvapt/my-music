import {createFileRoute} from '@tanstack/react-router'
import PurchasesPage from "../components/purchases/purchases-page.tsx";

export const Route = createFileRoute('/purchases')({
    component: Purchases,
})

function Purchases() {
    return <PurchasesPage/>;
}