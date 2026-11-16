import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import { useMemo, useState } from 'react';
import type { IsoDate, TaskFilters, TaskListItem } from '../../api/types';
import { ErrorMessage, Loading } from '../../components/Feedback';
import { t } from '../../i18n';
import { formatDayMonth, formatMonthYear, formatWeekday, todayInZone } from '../../lib/dates';
import { describeError } from '../../lib/errors';
import { useCurrentUser } from '../auth/AuthContext';
import { useCalendarTasks, useSetDeadline, useUndatedTasks } from '../tasks/queries';
import {
  getVisibleRange,
  groupByDeadline,
  isInSameMonth,
  shiftAnchor,
  type CalendarMode,
} from './calendarLogic';

const MAX_CHIPS_PER_DAY = 3;

function chipClass(task: TaskListItem): string {
  const classes = ['calendar-chip', `priority-${task.priority.toLowerCase()}`];
  if (task.status === 'Done') classes.push('done');
  if (task.isOverdue) classes.push('overdue');
  return classes.join(' ');
}

function TaskChip({ task, onOpen }: { task: TaskListItem; onOpen: (id: string) => void }) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({ id: task.id });
  return (
    <button
      type="button"
      ref={setNodeRef}
      className={isDragging ? `${chipClass(task)} dragging` : chipClass(task)}
      onClick={() => onOpen(task.id)}
      aria-label={t.tasks.openTask(task.title)}
      {...attributes}
      {...listeners}
    >
      {task.title}
    </button>
  );
}

interface DayCellProps {
  day: IsoDate;
  today: IsoDate;
  dimmed: boolean;
  showWeekday: boolean;
  tasks: TaskListItem[];
  onOpen: (id: string) => void;
}

function DayCell({ day, today, dimmed, showWeekday, tasks, onOpen }: DayCellProps) {
  const { setNodeRef, isOver } = useDroppable({ id: `day:${day}` });
  const classes = ['calendar-day'];
  if (day === today) classes.push('today');
  if (dimmed) classes.push('dimmed');
  if (isOver) classes.push('over');
  const visible = showWeekday ? tasks : tasks.slice(0, MAX_CHIPS_PER_DAY);

  return (
    <div
      ref={setNodeRef}
      role="gridcell"
      className={classes.join(' ')}
      aria-label={formatDayMonth(day)}
    >
      <div className="day-number">
        {showWeekday && <span className="weekday">{formatWeekday(day)} </span>}
        {Number(day.slice(8))}
      </div>
      {visible.map((task) => (
        <TaskChip key={task.id} task={task} onOpen={onOpen} />
      ))}
      {tasks.length > visible.length && (
        <span className="muted small-text">{t.calendar.more(tasks.length - visible.length)}</span>
      )}
    </div>
  );
}

function UndatedPanel({ tasks, onOpen }: { tasks: TaskListItem[]; onOpen: (id: string) => void }) {
  return (
    <aside className="calendar-undated" aria-label={t.calendar.noDate}>
      <h3>{t.calendar.noDate}</h3>
      <p className="hint">{t.calendar.dropHint}</p>
      {tasks.length === 0 ? (
        <p className="muted">{t.calendar.noDateEmpty}</p>
      ) : (
        tasks.map((task) => <TaskChip key={task.id} task={task} onOpen={onOpen} />)
      )}
    </aside>
  );
}

interface CalendarProps {
  filters: TaskFilters;
  onOpen: (id: string) => void;
}

/** Month or week calendar. Tasks sit in the cell of their deadline; tasks without one are in a side panel. */
export function Calendar({ filters, onOpen }: CalendarProps) {
  const user = useCurrentUser();
  const today = todayInZone(user.timeZoneId);
  const [mode, setMode] = useState<CalendarMode>('month');
  const [anchor, setAnchor] = useState<IsoDate>(today);

  const range = useMemo(() => getVisibleRange(mode, anchor), [mode, anchor]);
  const tasks = useCalendarTasks(filters, range.from, range.to);
  const undated = useUndatedTasks(filters);
  const setDeadline = useSetDeadline();
  const byDay = useMemo(() => groupByDeadline(tasks.data ?? []), [tasks.data]);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor),
  );

  function handleDragEnd(event: DragEndEvent) {
    const overId = event.over ? String(event.over.id) : '';
    if (!overId.startsWith('day:')) return;
    const deadline = overId.slice('day:'.length);
    const task = [...(tasks.data ?? []), ...(undated.data ?? [])].find(
      (item) => item.id === event.active.id,
    );
    if (!task || task.deadline === deadline) return;
    setDeadline.mutate({ id: task.id, deadline });
  }

  const title =
    mode === 'month'
      ? formatMonthYear(anchor)
      : `${formatDayMonth(range.from)} – ${formatDayMonth(range.to)}`;
  const weekdays = range.days.slice(0, 7);

  return (
    <div className="calendar" aria-label={t.calendar.label}>
      <div className="calendar-toolbar">
        <div className="button-group">
          <button
            type="button"
            className="button small"
            onClick={() => setAnchor(shiftAnchor(mode, anchor, -1))}
            aria-label={t.calendar.previous}
          >
            ‹
          </button>
          <button type="button" className="button small" onClick={() => setAnchor(today)}>
            {t.calendar.today}
          </button>
          <button
            type="button"
            className="button small"
            onClick={() => setAnchor(shiftAnchor(mode, anchor, 1))}
            aria-label={t.calendar.next}
          >
            ›
          </button>
        </div>
        <h3 className="calendar-title" aria-live="polite">
          {title}
        </h3>
        <div className="button-group" role="group" aria-label={t.calendar.mode}>
          <button
            type="button"
            className={mode === 'month' ? 'button small active' : 'button small'}
            aria-pressed={mode === 'month'}
            onClick={() => setMode('month')}
          >
            {t.calendar.month}
          </button>
          <button
            type="button"
            className={mode === 'week' ? 'button small active' : 'button small'}
            aria-pressed={mode === 'week'}
            onClick={() => setMode('week')}
          >
            {t.calendar.week}
          </button>
        </div>
      </div>

      {tasks.isError && (
        <ErrorMessage message={describeError(tasks.error)} onRetry={() => tasks.refetch()} />
      )}
      {setDeadline.isError && <ErrorMessage message={describeError(setDeadline.error)} />}
      {tasks.isPending && <Loading />}

      <DndContext sensors={sensors} onDragEnd={handleDragEnd}>
        <div className="calendar-layout">
          <div className="calendar-scroll">
            <div className={mode === 'week' ? 'calendar-grid week' : 'calendar-grid'} role="grid">
              {mode === 'month' &&
                weekdays.map((day) => (
                  <div key={day} role="columnheader" className="calendar-weekday">
                    {formatWeekday(day)}
                  </div>
                ))}
              {range.days.map((day) => (
                <DayCell
                  key={day}
                  day={day}
                  today={today}
                  dimmed={mode === 'month' && !isInSameMonth(day, anchor)}
                  showWeekday={mode === 'week'}
                  tasks={byDay.get(day) ?? []}
                  onOpen={onOpen}
                />
              ))}
            </div>
          </div>
          <UndatedPanel tasks={undated.data ?? []} onOpen={onOpen} />
        </div>
      </DndContext>
    </div>
  );
}
