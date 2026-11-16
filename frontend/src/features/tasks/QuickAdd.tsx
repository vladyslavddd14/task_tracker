import { useId, useRef, useState, type FormEvent } from 'react';
import { FieldError } from '../../components/Feedback';
import { t } from '../../i18n';
import { describeError } from '../../lib/errors';
import { validateTitle } from '../../lib/validation';

interface QuickAddProps {
  /** Creates the task. Rejecting the promise shows an error and keeps the typed text. */
  onAdd: (title: string) => Promise<unknown>;
}

/** One-line "Add task…" field: type a title and press Enter. */
export function QuickAdd({ onAdd }: QuickAddProps) {
  const [title, setTitle] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const errorId = useId();

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const problem = validateTitle(title);
    setError(problem);
    if (problem) {
      inputRef.current?.focus();
      return;
    }

    setBusy(true);
    try {
      await onAdd(title.trim());
      setTitle('');
    } catch (failure) {
      setError(describeError(failure));
    } finally {
      setBusy(false);
      inputRef.current?.focus();
    }
  }

  return (
    <form className="quick-add" onSubmit={handleSubmit} noValidate>
      <div className="quick-add-row">
        <label htmlFor={`${errorId}-input`} className="visually-hidden">
          {t.tasks.quickAddLabel}
        </label>
        <input
          id={`${errorId}-input`}
          ref={inputRef}
          value={title}
          placeholder={t.tasks.quickAddPlaceholder}
          onChange={(event) => setTitle(event.target.value)}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? errorId : undefined}
          autoComplete="off"
        />
        <button type="submit" className="button primary" disabled={busy}>
          {t.tasks.quickAddButton}
        </button>
      </div>
      <FieldError id={errorId} message={error} />
    </form>
  );
}
