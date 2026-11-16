import { useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { api } from '../../api/endpoints';
import { PROFILE_TEMPLATES, type ProfileTemplate } from '../../api/types';
import { ErrorMessage, FieldError } from '../../components/Feedback';
import { t } from '../../i18n';
import { ApiError } from '../../api/client';
import { describeError } from '../../lib/errors';
import { validateDisplayName, validatePassword } from '../../lib/validation';
import { useAuth, useCurrentUser } from '../auth/AuthContext';
import { taskKeys } from '../tasks/queries';

function listTimeZones(current: string): string[] {
  let zones: string[] = [];
  try {
    zones = Intl.supportedValuesOf('timeZone');
  } catch {
    zones = [];
  }
  const all = new Set(['UTC', ...zones]);
  all.add(current);
  return [...all].sort();
}

export function ProfileForm() {
  const user = useCurrentUser();
  const { setUser } = useAuth();
  const [displayName, setDisplayName] = useState(user.displayName);
  const [timeZoneId, setTimeZoneId] = useState(user.timeZoneId);
  const [error, setError] = useState<string | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const queryClient = useQueryClient();

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSaved(false);
    setFailure(null);
    const problem = validateDisplayName(displayName);
    setError(problem);
    if (problem) return;

    setBusy(true);
    try {
      setUser(await api.me.update(displayName.trim(), timeZoneId));
      // "Today" and overdue flags depend on the time zone, so reload the task data.
      await queryClient.invalidateQueries({ queryKey: taskKeys.all });
      setSaved(true);
    } catch (caught) {
      setFailure(describeError(caught));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card" aria-labelledby="profile-heading">
      <h2 id="profile-heading">{t.settings.profile}</h2>
      <form className="form" onSubmit={handleSubmit} noValidate>
        <div className="field">
          <label htmlFor="profile-name">{t.auth.displayName}</label>
          <input
            id="profile-name"
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
            aria-invalid={Boolean(error)}
            aria-describedby={error ? 'profile-name-error' : undefined}
          />
          <FieldError id="profile-name-error" message={error} />
        </div>
        <div className="field">
          <label htmlFor="profile-zone">{t.settings.timeZone}</label>
          <select
            id="profile-zone"
            value={timeZoneId}
            onChange={(event) => setTimeZoneId(event.target.value)}
          >
            {listTimeZones(user.timeZoneId).map((zone) => (
              <option key={zone} value={zone}>
                {zone}
              </option>
            ))}
          </select>
        </div>
        {failure && <ErrorMessage message={failure} />}
        <div className="form-actions">
          {saved && (
            <span className="success-text" role="status">
              {t.settings.profileSaved}
            </span>
          )}
          <button type="submit" className="button primary" disabled={busy}>
            {t.common.save}
          </button>
        </div>
      </form>
    </section>
  );
}

export function TemplateSection() {
  const user = useCurrentUser();
  const { setUser } = useAuth();
  const queryClient = useQueryClient();
  const [template, setTemplate] = useState<ProfileTemplate>(user.profileTemplate);
  const [failure, setFailure] = useState<string | null>(null);
  const [applied, setApplied] = useState(false);
  const [busy, setBusy] = useState(false);

  async function handleApply(event: FormEvent) {
    event.preventDefault();
    setApplied(false);
    setFailure(null);
    setBusy(true);
    try {
      setUser(await api.me.applyTemplate(template));
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['tags'] }),
        queryClient.invalidateQueries({ queryKey: ['custom-fields'] }),
      ]);
      setApplied(true);
    } catch (caught) {
      setFailure(describeError(caught));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card" aria-labelledby="template-heading">
      <h2 id="template-heading">{t.settings.template}</h2>
      <p className="hint">{t.settings.templateHelp}</p>
      <form className="form" onSubmit={handleApply}>
        <fieldset className="field">
          <legend className="visually-hidden">{t.settings.template}</legend>
          {PROFILE_TEMPLATES.map((option) => (
            <label key={option} className="radio-row">
              <input
                type="radio"
                name="settings-template"
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
        {failure && <ErrorMessage message={failure} />}
        <div className="form-actions">
          {applied && (
            <span className="success-text" role="status">
              {t.settings.templateApplied}
            </span>
          )}
          <button type="submit" className="button primary" disabled={busy}>
            {t.settings.templateApply}
          </button>
        </div>
      </form>
    </section>
  );
}

export function PasswordForm() {
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [errors, setErrors] = useState<{ current?: string | null; next?: string | null }>({});
  const [failure, setFailure] = useState<string | null>(null);
  const [changed, setChanged] = useState(false);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setChanged(false);
    setFailure(null);
    const found = {
      current: current ? null : t.validation.required,
      next: validatePassword(next),
    };
    setErrors(found);
    if (found.current || found.next) return;

    setBusy(true);
    try {
      await api.me.changePassword(current, next);
      setCurrent('');
      setNext('');
      setChanged(true);
    } catch (caught) {
      setFailure(
        caught instanceof ApiError && caught.status === 400
          ? t.settings.passwordWrong
          : describeError(caught),
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="card" aria-labelledby="password-heading">
      <h2 id="password-heading">{t.settings.password}</h2>
      <form className="form" onSubmit={handleSubmit} noValidate>
        <div className="field">
          <label htmlFor="password-current">{t.settings.currentPassword}</label>
          <input
            id="password-current"
            type="password"
            autoComplete="current-password"
            value={current}
            onChange={(event) => setCurrent(event.target.value)}
            aria-invalid={Boolean(errors.current)}
            aria-describedby={errors.current ? 'password-current-error' : undefined}
          />
          <FieldError id="password-current-error" message={errors.current} />
        </div>
        <div className="field">
          <label htmlFor="password-new">{t.settings.newPassword}</label>
          <input
            id="password-new"
            type="password"
            autoComplete="new-password"
            value={next}
            onChange={(event) => setNext(event.target.value)}
            aria-invalid={Boolean(errors.next)}
            aria-describedby={errors.next ? 'password-new-error' : undefined}
          />
          <FieldError id="password-new-error" message={errors.next} />
        </div>
        {failure && <ErrorMessage message={failure} />}
        <div className="form-actions">
          {changed && (
            <span className="success-text" role="status">
              {t.settings.passwordChanged}
            </span>
          )}
          <button type="submit" className="button primary" disabled={busy}>
            {t.common.save}
          </button>
        </div>
      </form>
    </section>
  );
}
