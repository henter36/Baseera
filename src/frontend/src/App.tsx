import { NavLink, Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/AuthProvider'
import { AttachmentsPage } from './pages/AttachmentsPage'
import { AuditPage } from './pages/AuditPage'
import { FacilitiesPage } from './pages/FacilitiesPage'
import { LoginPage } from './pages/LoginPage'
import { NoteCreatePage } from './pages/notes/NoteCreatePage'
import { NoteDetailPage } from './pages/notes/NoteDetailPage'
import { NoteEditPage } from './pages/notes/NoteEditPage'
import { NotesListPage } from './pages/notes/NotesListPage'
import { RegionsPage } from './pages/RegionsPage'
import { UsersPage } from './pages/UsersPage'

function Shell({ children }: { children: React.ReactNode }) {
  const { me, logout, hasPermission } = useAuth()
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">بصيرة</div>
        <div className="brand-sub">دعم اتخاذ القرار والإشراف التشغيلي</div>
        <nav className="nav" aria-label="القائمة الرئيسية">
          {hasPermission('Organization.View') && <NavLink to="/regions" className={({ isActive }) => isActive ? 'active' : undefined}>المناطق</NavLink>}
          {hasPermission('Organization.View') && <NavLink to="/facilities" className={({ isActive }) => isActive ? 'active' : undefined}>السجون</NavLink>}
          {hasPermission('Notes.View') && <NavLink to="/notes" className={({ isActive }) => isActive ? 'active' : undefined}>الملاحظات</NavLink>}
          {hasPermission('Users.View') && <NavLink to="/users" className={({ isActive }) => isActive ? 'active' : undefined}>المستخدمون</NavLink>}
          {hasPermission('Audit.View') && <NavLink to="/audit" className={({ isActive }) => isActive ? 'active' : undefined}>سجل التدقيق</NavLink>}
          {hasPermission('Attachments.Upload') && <NavLink to="/attachments" className={({ isActive }) => isActive ? 'active' : undefined}>المرفقات</NavLink>}
        </nav>
        <div style={{ marginTop: '2rem', fontSize: '0.9rem' }}>
          <div>{me?.displayNameAr}</div>
          <button className="secondary" style={{ marginTop: '0.75rem' }} onClick={logout}>
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
      <Route path="/notes/new" element={<Protected><NoteCreatePage /></Protected>} />
      <Route path="/notes/:id" element={<Protected><NoteDetailPage /></Protected>} />
      <Route path="/notes/:id/edit" element={<Protected><NoteEditPage /></Protected>} />
      <Route path="/users" element={<Protected><UsersPage /></Protected>} />
      <Route path="/audit" element={<Protected><AuditPage /></Protected>} />
      <Route path="/attachments" element={<Protected><AttachmentsPage /></Protected>} />
    </Routes>
  )
}
