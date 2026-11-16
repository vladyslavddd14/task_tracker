import { useId, useState } from 'react';
import { FieldError } from '../../components/Feedback';
import { MarkdownView } from '../../components/MarkdownView';
import { t } from '../../i18n';

interface NotesEditorProps {
  value: string;
  onChange: (value: string) => void;
  error?: string | null;
}

/** Markdown notes with two modes: edit and preview. */
export function NotesEditor({ value, onChange, error }: NotesEditorProps) {
  const [mode, setMode] = useState<'edit' | 'preview'>('edit');
  const id = useId();

  return (
    <div className="notes-editor">
      <div className="tabs" role="tablist" aria-label={t.notes.tabs}>
        <button
          type="button"
          role="tab"
          id={`${id}-edit-tab`}
          aria-selected={mode === 'edit'}
          aria-controls={`${id}-panel`}
          className={mode === 'edit' ? 'tab active' : 'tab'}
          onClick={() => setMode('edit')}
        >
          {t.notes.edit}
        </button>
        <button
          type="button"
          role="tab"
          id={`${id}-preview-tab`}
          aria-selected={mode === 'preview'}
          aria-controls={`${id}-panel`}
          className={mode === 'preview' ? 'tab active' : 'tab'}
          onClick={() => setMode('preview')}
        >
          {t.notes.preview}
        </button>
      </div>

      <div id={`${id}-panel`} role="tabpanel" aria-labelledby={`${id}-${mode}-tab`}>
        {mode === 'edit' ? (
          <>
            <textarea
              aria-label={t.tasks.notes}
              rows={8}
              value={value}
              placeholder={t.notes.placeholder}
              onChange={(event) => onChange(event.target.value)}
              aria-invalid={Boolean(error)}
              aria-describedby={`${id}-hint`}
            />
            <p id={`${id}-hint`} className="hint">
              {t.notes.hint}
            </p>
          </>
        ) : value.trim() ? (
          <MarkdownView source={value} />
        ) : (
          <p className="muted">{t.notes.empty}</p>
        )}
      </div>
      <FieldError id={`${id}-error`} message={error} />
    </div>
  );
}
