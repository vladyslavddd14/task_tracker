import { useState } from 'react';
import type { TaskListItem } from '../../api/types';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { EmptyState, ErrorMessage } from '../../components/Feedback';
import { t } from '../../i18n';
import { describeError } from '../../lib/errors';
import { TaskRow } from './TaskRow';
import { useChangeStatus, useDeleteTask } from './queries';

interface TaskListProps {
  tasks: TaskListItem[];
  emptyMessage: string;
  onOpen: (id: string) => void;
}

/** List of tasks with inline status change and delete (with confirmation). */
export function TaskList({ tasks, emptyMessage, onOpen }: TaskListProps) {
  const changeStatus = useChangeStatus();
  const deleteTask = useDeleteTask();
  const [pendingDelete, setPendingDelete] = useState<TaskListItem | null>(null);

  const failure = changeStatus.error ?? deleteTask.error;

  return (
    <>
      {failure && <ErrorMessage message={describeError(failure)} />}
      {tasks.length === 0 ? (
        <EmptyState message={emptyMessage} />
      ) : (
        <ul className="task-list">
          {tasks.map((task) => (
            <TaskRow
              key={task.id}
              task={task}
              onOpen={onOpen}
              onStatusChange={(id, status) => changeStatus.mutate({ id, status })}
              onDelete={setPendingDelete}
            />
          ))}
        </ul>
      )}

      {pendingDelete && (
        <ConfirmDialog
          title={t.tasks.deleteConfirmTitle}
          message={t.tasks.deleteConfirmText(pendingDelete.title)}
          busy={deleteTask.isPending}
          onCancel={() => setPendingDelete(null)}
          onConfirm={() =>
            deleteTask.mutate(pendingDelete.id, { onSettled: () => setPendingDelete(null) })
          }
        />
      )}
    </>
  );
}
