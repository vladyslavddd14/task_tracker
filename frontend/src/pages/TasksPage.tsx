import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { TaskFilters as Filters } from '../api/types';
import { ErrorMessage, Loading } from '../components/Feedback';
import { useViewMode } from '../hooks/hooks';
import { t } from '../i18n';
import { describeError } from '../lib/errors';
import { validateDateRange } from '../lib/validation';
import { Board } from '../features/board/Board';
import { Calendar } from '../features/calendar/Calendar';
import { QuickAdd } from '../features/tasks/QuickAdd';
import { TaskDetailsModal } from '../features/tasks/TaskDetailsModal';
import { TaskFilters } from '../features/tasks/TaskFilters';
import { TaskList } from '../features/tasks/TaskList';
import { ViewSwitcher } from '../features/tasks/ViewSwitcher';
import { defaultFilters } from '../features/tasks/filters';
import { useCreateTask, useCustomFields, useTags, useTaskList } from '../features/tasks/queries';

const PAGE_SIZE = 50;

function ListView({ filters, onOpen }: { filters: Filters; onOpen: (id: string) => void }) {
  const [page, setPage] = useState(1);
  const tasks = useTaskList(filters, page, PAGE_SIZE);

  if (tasks.isPending) return <Loading />;
  if (tasks.isError) {
    return <ErrorMessage message={describeError(tasks.error)} onRetry={() => tasks.refetch()} />;
  }

  const pages = Math.max(1, Math.ceil(tasks.data.total / PAGE_SIZE));
  return (
    <>
      <TaskList tasks={tasks.data.items} emptyMessage={t.tasks.listEmpty} onOpen={onOpen} />
      {pages > 1 && (
        <nav className="pagination" aria-label={t.tasks.pageOf(page, pages)}>
          <button
            type="button"
            className="button small"
            disabled={page <= 1}
            onClick={() => setPage(page - 1)}
          >
            {t.common.previous}
          </button>
          <span>
            {t.tasks.pageOf(page, pages)} · {t.tasks.total(tasks.data.total)}
          </span>
          <button
            type="button"
            className="button small"
            disabled={page >= pages}
            onClick={() => setPage(page + 1)}
          >
            {t.common.next}
          </button>
        </nav>
      )}
    </>
  );
}

/** Main page with the list, kanban board and calendar sharing one set of filters. */
export function TasksPage() {
  const [filters, setFilters] = useState<Filters>(defaultFilters);
  const [view, setView] = useViewMode();
  const [params, setParams] = useSearchParams();
  const create = useCreateTask();
  const tags = useTags();
  const fields = useCustomFields();
  const openId = params.get('task');

  // A reversed date range would be rejected by the API, so it is ignored until the user fixes it.
  const rangeIsInvalid =
    filters.deadline === 'range' &&
    validateDateRange(filters.deadlineFrom, filters.deadlineTo) !== null;
  const apiFilters: Filters = rangeIsInvalid
    ? { ...filters, deadline: '', deadlineFrom: '', deadlineTo: '' }
    : filters;

  const open = (id: string) => setParams({ task: id });

  return (
    <div className="page">
      <div className="page-header">
        <h1>{t.tasks.pageTitle}</h1>
        <ViewSwitcher value={view} onChange={setView} />
      </div>

      <QuickAdd onAdd={(title) => create.mutateAsync({ title })} />

      <TaskFilters
        filters={filters}
        onChange={setFilters}
        tags={tags.data ?? []}
        fields={fields.data ?? []}
      />

      {/* The key restarts paging from page 1 whenever the filters change. */}
      {view === 'list' && (
        <ListView key={JSON.stringify(apiFilters)} filters={apiFilters} onOpen={open} />
      )}
      {view === 'board' && <Board filters={apiFilters} onOpen={open} />}
      {view === 'calendar' && <Calendar filters={apiFilters} onOpen={open} />}

      {openId && <TaskDetailsModal taskId={openId} onClose={() => setParams({})} />}
    </div>
  );
}
