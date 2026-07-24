import { NavLink, Navigate, Route, Routes } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { api } from './api/client'
import { useAuth } from './auth/AuthProvider'
import { AttachmentsPage } from './pages/AttachmentsPage'
import { AuditPage } from './pages/AuditPage'
import { FacilitiesPage } from './pages/FacilitiesPage'
import { LoginPage } from './pages/LoginPage'
import { NotificationsPage } from './pages/NotificationsPage'
import { CorrectiveActionCreatePage } from './pages/corrective-actions/CorrectiveActionCreatePage'
import { CorrectiveActionDetailPage } from './pages/corrective-actions/CorrectiveActionDetailPage'
import { CorrectiveActionEditPage } from './pages/corrective-actions/CorrectiveActionEditPage'
import { CorrectiveActionsListPage } from './pages/corrective-actions/CorrectiveActionsListPage'
import { NoteCreatePage } from './pages/notes/NoteCreatePage'
import { NoteDetailPage } from './pages/notes/NoteDetailPage'
import { NoteEditPage } from './pages/notes/NoteEditPage'
import { ObservationWorkspacePage } from './pages/notes/ObservationWorkspacePage'
import { NoteRoutingEffectivenessPage } from './pages/notes/NoteRoutingEffectivenessPage'
import { NoteRoutingSettingsPage } from './pages/notes/NoteRoutingSettingsPage'
import { NoteTypesSettingsPage } from './pages/notes/NoteTypesSettingsPage'
import { RegionsPage } from './pages/RegionsPage'
import { UsersPage } from './pages/UsersPage'
import { EscalationOccurrencesPage } from './pages/escalations/EscalationOccurrencesPage'
import { EscalationPolicyDetailPage } from './pages/escalations/EscalationPolicyDetailPage'
import { EscalationPolicyFormPage } from './pages/escalations/EscalationPolicyFormPage'
import { EscalationsSettingsPage } from './pages/escalations/EscalationsSettingsPage'
import { FormAccessPage } from './pages/forms/FormAccessPage'
import { FormCreatePage } from './pages/forms/FormCreatePage'
import { FormDetailPage } from './pages/forms/FormDetailPage'
import { FormEditPage } from './pages/forms/FormEditPage'
import { FormReviewPage } from './pages/forms/FormReviewPage'
import { FormsGovernanceSettingsPage } from './pages/forms/FormsGovernanceSettingsPage'
import { FormsListPage } from './pages/forms/FormsListPage'
import { OperationalDashboardPage } from './pages/dashboard/OperationalDashboardPage'
import { FormVersionsPage } from './pages/forms/versions/FormVersionsPage'
import { FormVersionDetailPage } from './pages/forms/versions/FormVersionDetailPage'
import { FormDesignerPage } from './pages/forms/versions/FormDesignerPage'
import { FormVersionReviewPage } from './pages/forms/versions/FormVersionReviewPage'
import { FormVersionSnapshotPage } from './pages/forms/versions/FormVersionSnapshotPage'
import { FormTemplatesPage } from './pages/forms/templates/FormTemplatesPage'
import { FormCampaignsListPage, FormCampaignDetailPage } from './pages/form-campaigns/FormCampaignsListPage'
import { FormCampaignWizardPage } from './pages/form-campaigns/FormCampaignWizardPage'
import { FormCampaignCyclesPage, FormCampaignCycleDetailPage, FormCampaignPreviewPage } from './pages/form-campaigns/FormCampaignCyclesPage'
import { FormCompliancePage } from './pages/form-compliance/FormCompliancePage'
import { MyFormResponsesPage } from './pages/form-responses/MyFormResponsesPage'
import { RespondPage } from './pages/form-responses/RespondPage'
import { FormResponseReviewsPage, FormResponseReviewDetailPage } from './pages/form-responses/FormResponseReviewsPage'
import { ReferenceWorkspacePage } from './pages/workspaces/ReferenceWorkspacePage'

function isReferenceWorkspaceNavEnabled() {
  return import.meta.env.DEV || import.meta.env.VITE_ENABLE_REFERENCE_WORKSPACE === 'true'
}

function Shell({ children }: { children: React.ReactNode }) {
  const { me, logout, hasPermission } = useAuth()
  const [unreadCount, setUnreadCount] = useState(0)

  useEffect(() => {
    if (!hasPermission('Notifications.ViewOwn')) return
    let active = true
    const load = () => {
      api.notifications.unreadCount()
        .then((result) => { if (active) setUnreadCount(result.count) })
        .catch(() => { if (active) setUnreadCount(0) })
    }
    load()
    window.addEventListener('baseera:notifications-changed', load)
    const handle = window.setInterval(load, 60000)
    return () => {
      active = false
      window.removeEventListener('baseera:notifications-changed', load)
      window.clearInterval(handle)
    }
  }, [hasPermission])

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">بصيرة</div>
        <div className="brand-sub">دعم اتخاذ القرار والإشراف التشغيلي</div>
        <nav className="nav" aria-label="القائمة الرئيسية">
          {hasPermission('Organization.View') && <NavLink to="/regions" className={({ isActive }) => isActive ? 'active' : undefined}>المناطق</NavLink>}
          {hasPermission('Organization.View') && <NavLink to="/facilities" className={({ isActive }) => isActive ? 'active' : undefined}>السجون</NavLink>}
          {hasPermission('Notes.View') && <NavLink to="/notes/workspace" className={({ isActive }) => isActive ? 'active' : undefined}>الملاحظات</NavLink>}
          {hasPermission('Forms.View') && <NavLink to="/forms" className={({ isActive }) => isActive ? 'active' : undefined}>النماذج</NavLink>}
          {hasPermission('Forms.View') && <NavLink to="/form-templates" className={({ isActive }) => isActive ? 'active' : undefined}>قوالب النماذج</NavLink>}
          {(hasPermission('Forms.Publish') || hasPermission('Forms.ManageCampaigns') || hasPermission('Forms.View')) && (
            <NavLink to="/form-campaigns" className={({ isActive }) => isActive ? 'active' : undefined}>حملات النشر</NavLink>
          )}
          {hasPermission('Forms.Respond') && <NavLink to="/my-form-responses" className={({ isActive }) => isActive ? 'active' : undefined}>ردودي</NavLink>}
          {hasPermission('Forms.ReviewResponses') && <NavLink to="/form-response-reviews" className={({ isActive }) => isActive ? 'active' : undefined}>مراجعة الردود</NavLink>}
          {hasPermission('Forms.ViewComplianceDashboard') && <NavLink to="/form-compliance" className={({ isActive }) => isActive ? 'active' : undefined}>التزام النماذج</NavLink>}
          {(hasPermission('Dashboard.ViewOperational') || hasPermission('Dashboard.ViewRisk') || hasPermission('Dashboard.ViewRouting') || hasPermission('Dashboard.ViewCorrectiveActions')) &&
            <NavLink to="/dashboard" className={({ isActive }) => isActive ? 'active' : undefined}>لوحة المتابعة</NavLink>}
          {isReferenceWorkspaceNavEnabled() && hasPermission('Workspaces.View') && <NavLink to="/workspaces/reference" className={({ isActive }) => isActive ? 'active' : undefined}>مساحة مرجعية</NavLink>}
          {hasPermission('CorrectiveActions.View') && <NavLink to="/corrective-actions" className={({ isActive }) => isActive ? 'active' : undefined}>الإجراءات التصحيحية</NavLink>}
          {hasPermission('Notifications.ViewOwn') && <NavLink to="/notifications" className={({ isActive }) => isActive ? 'active' : undefined}>الإشعارات {unreadCount > 0 ? `(${unreadCount})` : ''}</NavLink>}
          {hasPermission('Escalations.View') && <NavLink to="/settings/escalations" className={({ isActive }) => isActive ? 'active' : undefined}>التصعيد</NavLink>}
          {hasPermission('Escalations.ViewOccurrences') && <NavLink to="/settings/escalations/occurrences" className={({ isActive }) => isActive ? 'active' : undefined}>حوادث التصعيد</NavLink>}
          {hasPermission('Notes.ManageTypes') && <NavLink to="/settings/note-types" className={({ isActive }) => isActive ? 'active' : undefined}>أنواع الملاحظات</NavLink>}
          {hasPermission('Notes.ViewRouting') && <NavLink to="/settings/note-routing" className={({ isActive }) => isActive ? 'active' : undefined}>توجيه الملاحظات</NavLink>}
          {hasPermission('Forms.ManageGovernance') && <NavLink to="/settings/forms-governance" className={({ isActive }) => isActive ? 'active' : undefined}>حوكمة النماذج</NavLink>}
          {hasPermission('Users.View') && <NavLink to="/users" className={({ isActive }) => isActive ? 'active' : undefined}>المستخدمون</NavLink>}
          {hasPermission('Audit.View') && <NavLink to="/audit" className={({ isActive }) => isActive ? 'active' : undefined}>سجل التدقيق</NavLink>}
          {hasPermission('Attachments.Upload') && <NavLink to="/attachments" className={({ isActive }) => isActive ? 'active' : undefined}>المرفقات</NavLink>}
        </nav>
        <div style={{ marginTop: '2rem', fontSize: '0.9rem' }}>
          <div>{me?.displayNameAr}</div>
          <button type="button" className="secondary" style={{ marginTop: '0.75rem' }} onClick={logout}>
            تسجيل الخروج
          </button>
        </div>
      </aside>
      <main className="content">{children}</main>
    </div>
  )
}

function Protected({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, loading } = useAuth()
  if (loading) return <div className="loading">جاري تحميل الجلسة…</div>
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return <Shell>{children}</Shell>
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<Protected><Navigate to="/regions" replace /></Protected>} />
      <Route path="/regions" element={<Protected><RegionsPage /></Protected>} />
      <Route path="/facilities" element={<Protected><FacilitiesPage /></Protected>} />
      <Route path="/notes" element={<Protected><ObservationWorkspacePage /></Protected>} />
      <Route path="/notes/workspace" element={<Protected><ObservationWorkspacePage /></Protected>} />
      <Route path="/dashboard" element={<Protected><OperationalDashboardPage /></Protected>} />
      <Route path="/workspaces/reference" element={<Protected><ReferenceWorkspacePage /></Protected>} />
      <Route path="/notes/new" element={<Protected><NoteCreatePage /></Protected>} />
      <Route path="/notes/:id" element={<Protected><NoteDetailPage /></Protected>} />
      <Route path="/notes/:id/edit" element={<Protected><NoteEditPage /></Protected>} />
      <Route path="/forms" element={<Protected><FormsListPage /></Protected>} />
      <Route path="/forms/new" element={<Protected><FormCreatePage /></Protected>} />
      <Route path="/forms/:id" element={<Protected><FormDetailPage /></Protected>} />
      <Route path="/forms/:id/edit" element={<Protected><FormEditPage /></Protected>} />
      <Route path="/forms/:id/review" element={<Protected><FormReviewPage /></Protected>} />
      <Route path="/forms/:id/access" element={<Protected><FormAccessPage /></Protected>} />
      <Route path="/forms/:formId/versions" element={<Protected><FormVersionsPage /></Protected>} />
      <Route path="/forms/:formId/versions/new" element={<Protected><FormVersionsPage /></Protected>} />
      <Route path="/forms/:formId/versions/:versionId" element={<Protected><FormVersionDetailPage /></Protected>} />
      <Route path="/forms/:formId/versions/:versionId/edit" element={<Protected><FormDesignerPage /></Protected>} />
      <Route path="/forms/:formId/versions/:versionId/review" element={<Protected><FormVersionReviewPage /></Protected>} />
      <Route path="/forms/:formId/versions/:versionId/snapshot" element={<Protected><FormVersionSnapshotPage /></Protected>} />
      <Route path="/form-templates" element={<Protected><FormTemplatesPage /></Protected>} />
      <Route path="/form-campaigns" element={<Protected><FormCampaignsListPage /></Protected>} />
      <Route path="/form-campaigns/new" element={<Protected><FormCampaignWizardPage /></Protected>} />
      <Route path="/form-campaigns/:campaignId" element={<Protected><FormCampaignDetailPage /></Protected>} />
      <Route path="/form-campaigns/:campaignId/edit" element={<Protected><FormCampaignWizardPage /></Protected>} />
      <Route path="/form-campaigns/:campaignId/targeting" element={<Protected><FormCampaignWizardPage /></Protected>} />
      <Route path="/form-campaigns/:campaignId/schedule" element={<Protected><FormCampaignWizardPage /></Protected>} />
      <Route path="/form-campaigns/:campaignId/preview" element={<Protected><FormCampaignPreviewPage /></Protected>} />
      <Route path="/form-campaigns/:campaignId/cycles" element={<Protected><FormCampaignCyclesPage /></Protected>} />
      <Route path="/form-campaigns/:campaignId/cycles/:cycleId" element={<Protected><FormCampaignCycleDetailPage /></Protected>} />
      <Route path="/my-form-responses" element={<Protected><MyFormResponsesPage /></Protected>} />
      <Route path="/form-assignments/:assignmentId/respond" element={<Protected><RespondPage /></Protected>} />
      <Route path="/form-response-reviews" element={<Protected><FormResponseReviewsPage /></Protected>} />
      <Route path="/form-responses/:responseId/review" element={<Protected><FormResponseReviewDetailPage /></Protected>} />
      <Route path="/form-compliance" element={<Protected><FormCompliancePage /></Protected>} />
      <Route path="/form-compliance/regions/:regionId" element={<Protected><FormCompliancePage /></Protected>} />
      <Route path="/form-compliance/facilities/:facilityId" element={<Protected><FormCompliancePage /></Protected>} />
      <Route path="/form-compliance/cycles/:cycleId" element={<Protected><FormCompliancePage /></Protected>} />

      <Route path="/settings/forms-governance" element={<Protected><FormsGovernanceSettingsPage /></Protected>} />
      <Route path="/notes/:noteId/corrective-actions/new" element={<Protected><CorrectiveActionCreatePage /></Protected>} />
      <Route path="/corrective-actions" element={<Protected><CorrectiveActionsListPage /></Protected>} />
      <Route path="/corrective-actions/:id" element={<Protected><CorrectiveActionDetailPage /></Protected>} />
      <Route path="/corrective-actions/:id/edit" element={<Protected><CorrectiveActionEditPage /></Protected>} />
      <Route path="/notifications" element={<Protected><NotificationsPage /></Protected>} />
      <Route path="/settings/escalations" element={<Protected><EscalationsSettingsPage /></Protected>} />
      <Route path="/settings/escalations/new" element={<Protected><EscalationPolicyFormPage mode="create" /></Protected>} />
      <Route path="/settings/escalations/occurrences" element={<Protected><EscalationOccurrencesPage /></Protected>} />
      <Route path="/settings/escalations/:id" element={<Protected><EscalationPolicyDetailPage /></Protected>} />
      <Route path="/settings/escalations/:id/edit" element={<Protected><EscalationPolicyFormPage mode="edit" /></Protected>} />
      <Route path="/settings/note-types" element={<Protected><NoteTypesSettingsPage /></Protected>} />
      <Route path="/settings/note-routing" element={<Protected><NoteRoutingSettingsPage /></Protected>} />
      <Route path="/settings/note-routing/effectiveness" element={<Protected><NoteRoutingEffectivenessPage /></Protected>} />
      <Route path="/users" element={<Protected><UsersPage /></Protected>} />
      <Route path="/audit" element={<Protected><AuditPage /></Protected>} />
      <Route path="/attachments" element={<Protected><AttachmentsPage /></Protected>} />
    </Routes>
  )
}
