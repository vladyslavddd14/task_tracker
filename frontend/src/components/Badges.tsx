import type { CSSProperties } from 'react';
import type { Tag, TaskPriority } from '../api/types';
import { t } from '../i18n';

export function PriorityBadge({ priority }: { priority: TaskPriority }) {
  return <span className={`badge priority-${priority.toLowerCase()}`}>{t.priority[priority]}</span>;
}

export function TagChip({ tag }: { tag: Tag }) {
  const style = tag.color ? ({ '--tag-color': tag.color } as CSSProperties) : undefined;
  return (
    <span className="tag-chip" style={style}>
      {tag.name}
    </span>
  );
}

export function ProgressBar({ done, total }: { done: number; total: number }) {
  if (total === 0) return null;
  const percent = Math.round((done / total) * 100);
  return (
    <div className="progress-line">
      <div
        className="progress"
        role="progressbar"
        aria-label={t.subtasks.title}
        aria-valuemin={0}
        aria-valuemax={total}
        aria-valuenow={done}
        aria-valuetext={t.subtasks.progress(done, total)}
      >
        <div className="progress-fill" style={{ width: `${percent}%` }} />
      </div>
      <span className="progress-text">{t.subtasks.progress(done, total)}</span>
    </div>
  );
}
