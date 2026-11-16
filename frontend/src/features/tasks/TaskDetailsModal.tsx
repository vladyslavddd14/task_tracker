import { useState } from 'react';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { ErrorMessage, Loading } from '../../components/Feedback';
import { Modal } from '../../components/Modal';
import { t } from '../../i18n';
import { formatDateTime } from '../../lib/dates';
import { describeError } from '../../lib/errors';
import { useCurrentUser } from '../auth/AuthContext';
import { SubtaskList } from './SubtaskList';
import { TaskForm } from './TaskForm';
import { useCustomFields, useDeleteTask, useTags, useTaskDetails } from './queries';

interface TaskDetailsModalProps {
  taskId: string;
  onClose: () => void;
}

/** Details of one task: all fields, subtasks, Markdown notes and the history of status changes. */
export function TaskDetailsModal({ taskId, onClose }: TaskDetailsModalProps) {
  const user = useCurrentUser();
  const task = useTaskDetails(taskId);
  const tags = useTags();
  const fields = useCustomFields();
  const deleteTask = useDeleteTask();
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  return (
    <Modal title={t.tasks.details} onClose={onClose} wide>
      {task.isPending && <Loading />}
      {task.isError && (
        <ErrorMessage message={describeError(task.error)} onRetry={() => task.refetch()} />
      )}

      {task.data && (
        <div className="details">
          {/* The key resets the form when the status was changed from outside (for example "mark done"). */}
          <TaskForm
            key={`${task.data.id}:${task.data.status}`}
            task={task.data}
            tags={tags.data ?? []}
            fields={fields.data ?? []}
          />

          <SubtaskList task={task.data} />

          <section aria-label={t.tasks.history}>
            <h3>{t.tasks.history}</h3>
            <p className="muted small-text">
              {t.tasks.created}: {formatDateTime(task.data.createdAt, user.timeZoneId)}
              {task.data.completedAt &&
                ` · ${t.tasks.completed}: ${formatDateTime(task.data.completedAt, user.timeZoneId)}`}
            </p>
            {task.data.transitions.length === 0 ? (
              <p className="muted">{t.tasks.historyEmpty}</p>
            ) : (
              <ul className="history">
                {task.data.transitions.map((transition, index) => (
                  <li key={index}>
                    <time dateTime={transition.changedAt}>
                      {formatDateTime(transition.changedAt, user.timeZoneId)}
                    </time>{' '}
                    {t.tasks.historyItem(
                      t.status[transition.fromStatus],
                      t.status[transition.toStatus],
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>

          <div className="form-actions spread">
            <button
              type="button"
              className="button danger"
              onClick={() => setConfirmingDelete(true)}
            >
              {t.tasks.deleteTask}
            </button>
            <button type="button" className="button" onClick={onClose}>
              {t.common.close}
            </button>
          </div>

          {confirmingDelete && (
            <ConfirmDialog
              title={t.tasks.deleteConfirmTitle}
              message={t.tasks.deleteConfirmText(task.data.title)}
              busy={deleteTask.isPending}
              onCancel={() => setConfirmingDelete(false)}
              onConfirm={() => deleteTask.mutate(task.data.id, { onSuccess: onClose })}
            />
          )}
          {deleteTask.error && <ErrorMessage message={describeError(deleteTask.error)} />}
        </div>
      )}
    </Modal>
  );
}
