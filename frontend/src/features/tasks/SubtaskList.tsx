import { useId, useState, type FormEvent } from 'react';
import type { Subtask, TaskDetails } from '../../api/types';
import { ProgressBar } from '../../components/Badges';
import { ErrorMessage, FieldError } from '../../components/Feedback';
import { t } from '../../i18n';
import { describeError } from '../../lib/errors';
import { LIMITS, validateTitle } from '../../lib/validation';
import { useChangeStatus, useSubtaskActions } from './queries';

interface SubtaskItemProps {
  subtask: Subtask;
  isFirst: boolean;
  isLast: boolean;
  onToggle: (subtask: Subtask) => void;
  onRename: (subtask: Subtask, title: string) => void;
  onMove: (subtask: Subtask, direction: -1 | 1) => void;
  onRemove: (subtask: Subtask) => void;
}

function SubtaskItem({
  subtask,
  isFirst,
  isLast,
  onToggle,
  onRename,
  onMove,
  onRemove,
}: SubtaskItemProps) {
  const [title, setTitle] = useState(subtask.title);
  const invalid = validateTitle(title) !== null;

  function commit() {
    const trimmed = title.trim();
    if (invalid) {
      setTitle(subtask.title); // an empty title is not allowed, so go back to the saved one
    } else if (trimmed !== subtask.title) {
      onRename(subtask, trimmed);
    }
  }

  return (
    <li className={subtask.isDone ? 'subtask done' : 'subtask'}>
      <input
        type="checkbox"
        checked={subtask.isDone}
        aria-label={t.subtasks.toggle(subtask.title)}
        onChange={() => onToggle(subtask)}
      />
      <input
        className="subtask-title"
        value={title}
        aria-label={t.subtasks.rename}
        onChange={(event) => setTitle(event.target.value)}
        onBlur={commit}
        onKeyDown={(event) => {
          if (event.key === 'Enter') event.currentTarget.blur();
        }}
        maxLength={LIMITS.title}
      />
      <button
        type="button"
        className="icon-button"
        onClick={() => onMove(subtask, -1)}
        disabled={isFirst}
        aria-label={`${t.common.moveUp}: ${subtask.title}`}
      >
        ↑
      </button>
      <button
        type="button"
        className="icon-button"
        onClick={() => onMove(subtask, 1)}
        disabled={isLast}
        aria-label={`${t.common.moveDown}: ${subtask.title}`}
      >
        ↓
      </button>
      <button
        type="button"
        className="icon-button danger-text"
        onClick={() => onRemove(subtask)}
        aria-label={t.subtasks.remove(subtask.title)}
      >
        ×
      </button>
    </li>
  );
}

/** Subtasks of a task: add, rename, tick, reorder and delete. Ticking all of them never changes the task status. */
export function SubtaskList({ task }: { task: TaskDetails }) {
  const actions = useSubtaskActions(task.id);
  const changeStatus = useChangeStatus();
  const [title, setTitle] = useState('');
  const [error, setError] = useState<string | null>(null);
  const errorId = useId();

  const subtasks = [...task.subtasks].sort((a, b) => a.position - b.position);
  const done = subtasks.filter((subtask) => subtask.isDone).length;
  const allDone = subtasks.length > 0 && done === subtasks.length;
  const atLimit = subtasks.length >= LIMITS.maxSubtasks;
  const failure =
    actions.add.error ?? actions.update.error ?? actions.remove.error ?? actions.reorder.error;

  function handleAdd(event: FormEvent) {
    event.preventDefault();
    const problem = validateTitle(title) ?? (atLimit ? t.subtasks.limit : null);
    setError(problem);
    if (problem) return;
    actions.add.mutate(title.trim(), { onSuccess: () => setTitle('') });
  }

  function move(subtask: Subtask, direction: -1 | 1) {
    const ids = subtasks.map((item) => item.id);
    const index = ids.indexOf(subtask.id);
    const target = index + direction;
    if (target < 0 || target >= ids.length) return;
    [ids[index], ids[target]] = [ids[target], ids[index]];
    actions.reorder.mutate(ids);
  }

  return (
    <section className="subtasks" aria-label={t.subtasks.title}>
      <h3>{t.subtasks.title}</h3>
      <ProgressBar done={done} total={subtasks.length} />

      {allDone && task.status !== 'Done' && (
        <div className="alert info" role="status">
          <span>{t.subtasks.allDone}</span>
          <button
            type="button"
            className="button small primary"
            onClick={() => changeStatus.mutate({ id: task.id, status: 'Done' })}
            disabled={changeStatus.isPending}
          >
            {t.subtasks.markTaskDone}
          </button>
        </div>
      )}

      {failure && <ErrorMessage message={describeError(failure)} />}

      {subtasks.length === 0 ? (
        <p className="muted">{t.subtasks.empty}</p>
      ) : (
        <ul className="subtask-list">
          {subtasks.map((subtask, index) => (
            <SubtaskItem
              // The key includes the title so the inline editor resets after a rename from the server.
              key={`${subtask.id}:${subtask.title}`}
              subtask={subtask}
              isFirst={index === 0}
              isLast={index === subtasks.length - 1}
              onToggle={(item) =>
                actions.update.mutate({ id: item.id, title: item.title, isDone: !item.isDone })
              }
              onRename={(item, newTitle) =>
                actions.update.mutate({ id: item.id, title: newTitle, isDone: item.isDone })
              }
              onMove={move}
              onRemove={(item) => actions.remove.mutate(item.id)}
            />
          ))}
        </ul>
      )}

      <form className="inline-form" onSubmit={handleAdd} noValidate>
        <label htmlFor={`${errorId}-new`} className="visually-hidden">
          {t.subtasks.addLabel}
        </label>
        <input
          id={`${errorId}-new`}
          value={title}
          placeholder={t.subtasks.add}
          onChange={(event) => setTitle(event.target.value)}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? errorId : undefined}
          autoComplete="off"
        />
        <button type="submit" className="button" disabled={actions.add.isPending}>
          {t.common.add}
        </button>
      </form>
      <FieldError id={errorId} message={error} />
    </section>
  );
}
