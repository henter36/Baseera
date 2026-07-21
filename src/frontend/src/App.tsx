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
import { NoteRoutingEffectivenessPage } from './pages/notes/NoteRoutingEffectivenessPage'
import { NoteRoutingSettingsPage } from './pages/notes/NoteRoutingSettingsPage'
import { NoteTypesSettingsPage } from './pages/notes/NoteTypesSettingsPage'
import { NotesListPage } from './pages/notes/NotesListPage'
import { RegionsPage } from './pages/RegionsPage'
import { UsersPage } from './pages/UsersPage'
import { EscalationOccurrencesPage } from './pages/escalations/EscalationOccurrencesPage'
import { EscalationPolicyDetailPage } from './pages/escalations/EscalationPolicyDetailPage'
import { EscalationPolicyFormPage } from './pages/escalations/EscalationPolicyFormPage'
import { EscalationsSettingsPage } from './pages/escalations/EscalationsSettingsPage'
import { OperationalDashboardPage } from './pages/dashboard/OperationalDashboardPage'

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
          {hasPermission('Notes.View') && <NavLink to="/notes" className={({ isActive }) => isActive ? 'active' : undefined}>الملاحظات</NavLink>}
          {(hasPermission('Dashboard.ViewOperational') || hasPermission('Dashboard.ViewRisk') || hasPermission('Dashboard.ViewRouting') || hasPermission('Dashboard.ViewCorrectiveActions')) &&
            <NavLink to="/dashboard" className={({ isActive }) => isActive ? 'active' : undefined}>لوحة المتابعة</NavLink>}
          {hasPermission('CorrectiveActions.View') && <NavLink to="/corrective-actions" className={({ isActive }) => isActive ? 'active' : undefined}>الإجراءات التصحيحية</NavLink>}
          {hasPermission('Notifications.ViewOwn') && <NavLink to="/notifications" className={({ isActive }) => isActive ? 'active' : undefined}>الإشعارات {unreadCount > 0 ? `(${unreadCount})` : ''}</NavLink>}
          {hasPermission('Escalations.View') && <NavLink to="/settings/escalations" className={({ isActive }) => isActive ? 'active' : undefined}>التصعيد</NavLink>}
          {hasPermission('Escalations.ViewOccurrences') && <NavLink to="/settings/escalations/occurrences" className={({ isActive }) => isActive ? 'active' : undefined}>حوادث التصعيد</NavLink>}
          {hasPermission('Notes.ManageTypes') && <NavLink to="/settings/note-types" className={({ isActive }) => isActive ? 'active' : undefined}>أنواع الملاحظات</NavLink>}
          {hasPermission('Notes.ViewRouting') && <NavLink to="/settings/note-routing" className={({ isActive }) => isActive ? 'active' : undefined}>توجيه الملاحظات</NavLink>}
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
      <Route path="/notes" element={<Protected><NotesListPage /></Protected>} />
      <Route path="/dashboard" element={<Protected><OperationalDashboardPage /></Protected>} />
      <Route path="/notes/new" element={<Protected><NoteCreatePage /></Protected>} />
      <Route path="/notes/:id" element={<Protected><NoteDetailPage /></Protected>} />
      <Route path="/notes/:id/edit" element={<Protected><NoteEditPage /></Protected>} />
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
