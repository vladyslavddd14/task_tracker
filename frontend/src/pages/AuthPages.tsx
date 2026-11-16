import { Link, Navigate, useNavigate } from 'react-router-dom';
import { t } from '../i18n';
import { useAuth } from '../features/auth/AuthContext';
import { LoginForm } from '../features/auth/LoginForm';
import { RegisterForm } from '../features/auth/RegisterForm';

export function LoginPage() {
  const { status, login } = useAuth();
  const navigate = useNavigate();

  if (status === 'authenticated') return <Navigate to="/today" replace />;

  return (
    <main className="auth-page">
      <h1>{t.auth.loginTitle}</h1>
      <p className="muted">{t.appName}</p>
      <LoginForm
        onSubmit={async (email, password) => {
          await login(email, password);
          navigate('/today');
        }}
      />
      <p>
        {t.auth.noAccount} <Link to="/register">{t.auth.goRegister}</Link>
      </p>
    </main>
  );
}

export function RegisterPage() {
  const { status, register } = useAuth();
  const navigate = useNavigate();

  if (status === 'authenticated') return <Navigate to="/today" replace />;

  return (
    <main className="auth-page">
      <h1>{t.auth.registerTitle}</h1>
      <p className="muted">{t.appName}</p>
      <RegisterForm
        onSubmit={async (values) => {
          await register(values);
          navigate('/today');
        }}
      />
      <p>
        {t.auth.haveAccount} <Link to="/login">{t.auth.goLogin}</Link>
      </p>
    </main>
  );
}
