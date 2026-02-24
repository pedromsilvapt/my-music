import {createFileRoute} from '@tanstack/react-router'
import AuditDetailPage from "../components/audits/audit-detail-page.tsx";

export const Route = createFileRoute('/audits/$auditId')({
    component: AuditDetail,
})

function AuditDetail() {
    return <AuditDetailPage/>;
}
