import { useState, type FormEvent } from 'react';
import { ApiError } from '../../api/client';
import { PROFILE_TEMPLATES, type ProfileTemplate } from '../../api/types';
import { FieldError } from '../../components/Feedback';
import { t } from '../../i18n';
import { describeError } from '../../lib/errors';
import { validateDisplayName, validateEmail, validatePassword } from '../../lib/validation';

export interface RegisterValues {
  email: string;
  password: string;
  displayName: string;
  timeZoneId: string;
  profileTemplate: ProfileTemplate;
}

interface RegisterFormProps {
  onSubmit: (values: RegisterValues) => Promise<void>;
}

function browserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}

export function RegisterForm({ onSubmit }: RegisterFormProps) {
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [template, setTemplate] = useState<ProfileTemplate>('Universal');
  const [errors, setErrors] = useState<Record<string, string | null>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const next = {
      displayName: validateDisplayName(displayName),
      email: validateEmail(email),
      password: validatePassword(password),
    };
    setErrors(next);
    setFormError(null);
    if (Object.values(next).some(Boolean)) return;

    setBusy(true);
    try {
      await onSubmit({
        email: email.trim(),
        password,
        displayName: displayName.trim(),
        timeZoneId: browserTimeZone(),
        profileTemplate: template,
      });
    } catch (error) {
      setFormError(describeError(error, t.auth.emailTaken));
      if (error instanceof ApiError && error.status === 409) {
        setErrors({ email: t.auth.emailTaken });
      }
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
        <label htmlFor="reg-name">{t.auth.displayName}</label>
        <input
          id="reg-name"
          autoComplete="name"
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
          aria-invalid={Boolean(errors.displayName)}
          aria-describedby={errors.displayName ? 'reg-name-error' : undefined}
        />
        <FieldError id="reg-name-error" message={errors.displayName} />
      </div>
      <div className="field">
        <label htmlFor="reg-email">{t.auth.email}</label>
        <input
          id="reg-email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          aria-invalid={Boolean(errors.email)}
          aria-describedby={errors.email ? 'reg-email-error' : undefined}
        />
        <FieldError id="reg-email-error" message={errors.email} />
      </div>
      <div className="field">
        <label htmlFor="reg-password">{t.auth.password}</label>
        <input
          id="reg-password"
          type="password"
          autoComplete="new-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          aria-invalid={Boolean(errors.password)}
          aria-describedby={errors.password ? 'reg-password-error' : 'reg-password-hint'}
        />
        <p id="reg-password-hint" className="hint">
          {t.auth.passwordHint}
        </p>
        <FieldError id="reg-password-error" message={errors.password} />
      </div>
      <fieldset className="field">
        <legend>{t.auth.profileTemplate}</legend>
        {PROFILE_TEMPLATES.map((option) => (
          <label key={option} className="radio-row">
            <input
              type="radio"
              name="template"
              value={option}
              checked={template === option}
              onChange={() => setTemplate(option)}
            />
            <span>
              <strong>{t.template[option]}</strong>
              <span className="hint block">{t.templateHint[option]}</span>
            </span>
          </label>
        ))}
      </fieldset>
      <button type="submit" className="button primary" disabled={busy}>
        {t.auth.submitRegister}
      </button>
    </form>
  );
}
