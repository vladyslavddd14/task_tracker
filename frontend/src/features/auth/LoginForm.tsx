import { useState, type FormEvent } from 'react';
import { ApiError } from '../../api/client';
import { FieldError } from '../../components/Feedback';
import { t } from '../../i18n';
import { describeError } from '../../lib/errors';
import { validateEmail } from '../../lib/validation';

interface LoginFormProps {
  onSubmit: (email: string, password: string) => Promise<void>;
}

export function LoginForm({ onSubmit }: LoginFormProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ email?: string | null; password?: string | null }>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const next = {
      email: validateEmail(email),
      password: password ? null : t.validation.required,
    };
    setErrors(next);
    setFormError(null);
    if (next.email || next.password) return;

    setBusy(true);
    try {
      await onSubmit(email.trim(), password);
    } catch (error) {
      setFormError(
        error instanceof ApiError && error.status === 401
          ? t.auth.loginFailed
          : describeError(error),
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="form">
      {formError && (
        <div className="alert error" role="alert">
          {formError}
        </div>
      )}
      <div className="field">
        <label htmlFor="login-email">{t.auth.email}</label>
        <input
          id="login-email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          aria-invalid={Boolean(errors.email)}
          aria-describedby={errors.email ? 'login-email-error' : undefined}
        />
        <FieldError id="login-email-error" message={errors.email} />
      </div>
      <div className="field">
        <label htmlFor="login-password">{t.auth.password}</label>
        <input
          id="login-password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          aria-invalid={Boolean(errors.password)}
          aria-describedby={errors.password ? 'login-password-error' : undefined}
        />
        <FieldError id="login-password-error" message={errors.password} />
      </div>
      <button type="submit" className="button primary" disabled={busy}>
        {t.auth.submitLogin}
      </button>
    </form>
  );
}
