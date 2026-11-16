import { useSearchParams } from 'react-router-dom';
import { ErrorMessage, Loading } from '../components/Feedback';
import { t } from '../i18n';
import { todayInZone } from '../lib/dates';
import { describeError } from '../lib/errors';
import { useCurrentUser } from '../features/auth/AuthContext';
import { QuickAdd } from '../features/tasks/QuickAdd';
import { TaskDetailsModal } from '../features/tasks/TaskDetailsModal';
import { TaskList } from '../features/tasks/TaskList';
import { useCreateTask, useTodayTasks } from '../features/tasks/queries';

/** Start page: unfinished tasks that are due today or overdue, most important first. */
export function TodayPage() {
  const user = useCurrentUser();
  const tasks = useTodayTasks();
  const create = useCreateTask();
  const [params, setParams] = useSearchParams();
  const openId = params.get('task');

  return (
    <div className="page">
      <h1>{t.tasks.todayTitle}</h1>
      <p className="muted">{t.tasks.todaySubtitle}</p>

      {/* Tasks added here are due today, otherwise they would not show up on this page. */}
      <QuickAdd
        onAdd={(title) => create.mutateAsync({ title, deadline: todayInZone(user.timeZoneId) })}
      />

      {tasks.isPending && <Loading />}
      {tasks.isError && (
        <ErrorMessage message={describeError(tasks.error)} onRetry={() => tasks.refetch()} />
      )}
      {tasks.data && (
        <TaskList
          tasks={tasks.data}
          emptyMessage={t.tasks.todayEmpty}
          onOpen={(id) => setParams({ task: id })}
        />
      )}

      {openId && <TaskDetailsModal taskId={openId} onClose={() => setParams({})} />}
    </div>
  );
}
