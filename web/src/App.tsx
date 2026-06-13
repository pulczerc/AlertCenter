import { NavLink, Navigate, Route, Routes } from 'react-router-dom'
import { NotificationsPage } from './pages/NotificationsPage'
import { UsersPage } from './pages/UsersPage'
import { AlertsPage } from './pages/AlertsPage'
import { OpsPage } from './pages/OpsPage'
import './App.css'

export default function App() {
  return (
    <div className="layout">
      <header className="topbar"><strong>AlertCenter</strong> ▸ admin</header>
      <nav className="nav">
        <NavLink to="/notifications">Notifications</NavLink>
        <NavLink to="/users">Users</NavLink>
        <NavLink to="/alerts">Alerts</NavLink>
        <NavLink to="/ops">Ops</NavLink>
      </nav>
      <main className="content">
        <Routes>
          <Route path="/" element={<Navigate to="/notifications" replace />} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/users" element={<UsersPage />} />
          <Route path="/alerts" element={<AlertsPage />} />
          <Route path="/ops" element={<OpsPage />} />
        </Routes>
      </main>
    </div>
  )
}
