import { VIEW_MODES, type ViewMode } from '../../hooks/hooks';
import { t } from '../../i18n';

interface ViewSwitcherProps {
  value: ViewMode;
  onChange: (mode: ViewMode) => void;
}

/** Switch between list, kanban board and calendar. */
export function ViewSwitcher({ value, onChange }: ViewSwitcherProps) {
  return (
    <div className="button-group" role="group" aria-label={t.views.label}>
      {VIEW_MODES.map((mode) => (
        <button
          key={mode}
          type="button"
          className={mode === value ? 'button active' : 'button'}
          aria-pressed={mode === value}
          onClick={() => onChange(mode)}
        >
          {t.views[mode]}
        </button>
      ))}
    </div>
  );
}
