import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { t } from '../i18n';
import { useAuth } from '../features/auth/AuthContext';
import { useThemeToggle } from './ThemeProvider';

/** Header with navigation, theme switch and sign out, shared by all signed-in pages. */
export function AppLayout() {
  const { user, logout } = useAuth();
  const { theme, toggle } = useThemeToggle();
  const navigate = useNavigate();

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    isActive ? 'nav-link active' : 'nav-link';

  return (
    <>
      <a className="skip-link" href="#main">
        {t.nav.skipToContent}
      </a>
      <header className="app-header">
        <span className="brand">{t.appName}</span>
        <nav aria-label={t.nav.main} className="nav">
          <NavLink to="/today" className={linkClass}>
            {t.nav.today}
          </NavLink>
          <NavLink to="/tasks" className={linkClass}>
            {t.nav.tasks}
          </NavLink>
          <NavLink to="/settings" className={linkClass}>
            {t.nav.settings}
          </NavLink>
        </nav>
        <div className="header-actions">
          <span className="user-name">{user?.displayName}</span>
          <button type="button" className="button small" onClick={toggle}>
            {theme === 'dark' ? t.nav.switchToLight : t.nav.switchToDark}
          </button>
          <button
            type="button"
            className="button small"
            onClick={() => {
              logout();
              navigate('/login');
            }}
          >
            {t.nav.logout}
          </button>
        </div>
      </header>
      <main id="main" className="app-main">
        <Outlet />
      </main>
    </>
  );
}
