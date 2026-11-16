import { TASK_STATUSES, type TaskListItem, type TaskStatus } from '../../api/types';
import { PriorityBadge, ProgressBar, TagChip } from '../../components/Badges';
import { t } from '../../i18n';
import { formatDate } from '../../lib/dates';

interface TaskRowProps {
  task: TaskListItem;
  onOpen: (id: string) => void;
  onStatusChange: (id: string, status: TaskStatus) => void;
  onDelete: (task: TaskListItem) => void;
}

/** One task in the list view. Overdue tasks are highlighted in red. */
export function TaskRow({ task, onOpen, onStatusChange, onDelete }: TaskRowProps) {
  const classes = ['task-row'];
  if (task.isOverdue) classes.push('overdue');
  if (task.status === 'Done') classes.push('done');

  return (
    <li className={classes.join(' ')}>
      <select
        className="status-select"
        value={task.status}
        aria-label={t.tasks.changeStatusOf(task.title)}
        onChange={(event) => onStatusChange(task.id, event.target.value as TaskStatus)}
      >
        {TASK_STATUSES.map((status) => (
          <option key={status} value={status}>
            {t.status[status]}
          </option>
        ))}
      </select>

      <div className="task-main">
        <button
          type="button"
          className="link-button task-title"
          onClick={() => onOpen(task.id)}
          aria-label={t.tasks.openTask(task.title)}
        >
          {task.title}
        </button>
        <div className="task-meta">
          <PriorityBadge priority={task.priority} />
          {task.deadline && (
            <span className={task.isOverdue ? 'deadline overdue-text' : 'deadline'}>
              {task.isOverdue && <span className="visually-hidden">{t.tasks.overdue}: </span>}
              {formatDate(task.deadline)}
            </span>
          )}
          {task.tags.map((tag) => (
            <TagChip key={tag.id} tag={tag} />
          ))}
        </div>
        <ProgressBar done={task.subtasksDone} total={task.subtasksTotal} />
      </div>

      <button
        type="button"
        className="icon-button danger-text"
        onClick={() => onDelete(task)}
        aria-label={`${t.common.delete}: ${task.title}`}
      >
        ×
      </button>
    </li>
  );
}
