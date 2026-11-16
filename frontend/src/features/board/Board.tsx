import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  closestCorners,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { useMemo, useState } from 'react';
import {
  TASK_STATUSES,
  type TaskFilters,
  type TaskListItem,
  type TaskStatus,
} from '../../api/types';
import { PriorityBadge, ProgressBar, TagChip } from '../../components/Badges';
import { ErrorMessage, Loading } from '../../components/Feedback';
import { t } from '../../i18n';
import { formatDate } from '../../lib/dates';
import { describeError } from '../../lib/errors';
import { useBoardTasks, useMoveTask } from '../tasks/queries';
import { computeMove, groupByStatus, resolveDropTarget } from './boardLogic';

function CardContent({ task }: { task: TaskListItem }) {
  return (
    <>
      <span className="card-title">{task.title}</span>
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
    </>
  );
}

interface CardProps {
  task: TaskListItem;
  onOpen: (id: string) => void;
}

function BoardCard({ task, onOpen }: CardProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: task.id,
  });
  const classes = ['board-card'];
  if (task.isOverdue) classes.push('overdue');
  if (task.status === 'Done') classes.push('done');
  if (isDragging) classes.push('dragging');

  return (
    <li
      ref={setNodeRef}
      className={classes.join(' ')}
      style={{ transform: CSS.Transform.toString(transform), transition }}
    >
      <button
        type="button"
        className="drag-handle"
        aria-label={t.board.dragHandle(task.title)}
        {...attributes}
        {...listeners}
      >
        ⠿
      </button>
      <button
        type="button"
        className="card-body link-button"
        onClick={() => onOpen(task.id)}
        aria-label={t.tasks.openTask(task.title)}
      >
        <CardContent task={task} />
      </button>
    </li>
  );
}

interface ColumnProps {
  status: TaskStatus;
  tasks: TaskListItem[];
  onOpen: (id: string) => void;
}

function BoardColumn({ status, tasks, onOpen }: ColumnProps) {
  const { setNodeRef, isOver } = useDroppable({ id: `column:${status}` });

  return (
    <section
      className={isOver ? 'board-column over' : 'board-column'}
      aria-label={t.status[status]}
    >
      <h3>{t.board.column(t.status[status], tasks.length)}</h3>
      <SortableContext items={tasks.map((task) => task.id)} strategy={verticalListSortingStrategy}>
        <ul ref={setNodeRef} className="board-cards">
          {tasks.map((task) => (
            <BoardCard key={task.id} task={task} onOpen={onOpen} />
          ))}
          {tasks.length === 0 && <li className="board-empty muted">{t.board.emptyColumn}</li>}
        </ul>
      </SortableContext>
    </section>
  );
}

interface BoardProps {
  filters: TaskFilters;
  onOpen: (id: string) => void;
}

/** Kanban board with drag-and-drop between and inside columns (mouse, touch and keyboard). */
export function Board({ filters, onOpen }: BoardProps) {
  const tasks = useBoardTasks(filters);
  const move = useMoveTask();
  const [activeTask, setActiveTask] = useState<TaskListItem | null>(null);
  const [moveError, setMoveError] = useState<string | null>(null);

  const columns = useMemo(() => groupByStatus(tasks.data ?? []), [tasks.data]);

  const sensors = useSensors(
    // A small distance lets a plain click open the card instead of starting a drag.
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  function handleDragStart(event: DragStartEvent) {
    setActiveTask((tasks.data ?? []).find((task) => task.id === event.active.id) ?? null);
  }

  function handleDragEnd(event: DragEndEvent) {
    setActiveTask(null);
    if (!event.over) return;

    const taskId = String(event.active.id);
    const target = resolveDropTarget(columns, String(event.over.id));
    if (!target) return;

    const current = (tasks.data ?? []).find((task) => task.id === taskId);
    const currentIndex = columns[target.status].findIndex((task) => task.id === taskId);
    if (current && current.status === target.status && currentIndex === target.index) return;

    const { status, position } = computeMove(columns, taskId, target);
    setMoveError(null);
    move.mutate(
      { id: taskId, status, position },
      { onError: () => setMoveError(t.errors.moveFailed) },
    );
  }

  if (tasks.isPending) return <Loading />;
  if (tasks.isError) {
    return <ErrorMessage message={describeError(tasks.error)} onRetry={() => tasks.refetch()} />;
  }

  return (
    <div aria-label={t.board.label}>
      {moveError && <ErrorMessage message={moveError} />}
      <DndContext
        sensors={sensors}
        collisionDetection={closestCorners}
        onDragStart={handleDragStart}
        onDragEnd={handleDragEnd}
        onDragCancel={() => setActiveTask(null)}
      >
        <div className="board">
          {TASK_STATUSES.map((status) => (
            <BoardColumn key={status} status={status} tasks={columns[status]} onOpen={onOpen} />
          ))}
        </div>
        <DragOverlay>
          {activeTask ? (
            <div className="board-card overlay">
              <div className="card-body">
                <CardContent task={activeTask} />
              </div>
            </div>
          ) : null}
        </DragOverlay>
      </DndContext>
    </div>
  );
}
