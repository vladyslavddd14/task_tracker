import { TASK_STATUSES, type TaskListItem, type TaskStatus } from '../../api/types';

export type Columns = Record<TaskStatus, TaskListItem[]>;

/** Distance between neighbouring cards when a card is dropped at the start or end of a column. */
export const POSITION_STEP = 1000;

/** Splits tasks into the three kanban columns, each ordered by position. */
export function groupByStatus(tasks: TaskListItem[]): Columns {
  const columns: Columns = { Todo: [], InProgress: [], Done: [] };
  for (const task of tasks) {
    columns[task.status].push(task);
  }
  for (const status of TASK_STATUSES) {
    columns[status].sort(
      (a, b) => a.position - b.position || a.createdAt.localeCompare(b.createdAt),
    );
  }
  return columns;
}

export interface MoveResult {
  status: TaskStatus;
  position: number;
}

/**
 * Works out the new status and position for a card dropped at `index` of a column.
 * The index counts cards of that column without the moved card, which is how drag-and-drop
 * libraries report the final place. The position is placed between the two new neighbours,
 * so no other card has to be updated.
 */
export function computeMove(
  columns: Columns,
  taskId: string,
  target: { status: TaskStatus; index: number },
): MoveResult {
  const others = columns[target.status].filter((task) => task.id !== taskId);
  const index = Math.max(0, Math.min(target.index, others.length));
  const before = others[index - 1];
  const after = others[index];

  let position: number;
  if (before && after) position = (before.position + after.position) / 2;
  else if (before) position = before.position + POSITION_STEP;
  else if (after) position = after.position - POSITION_STEP;
  else position = POSITION_STEP;

  return { status: target.status, position };
}

/** Where a card was dropped: on another card (take its place) or on an empty area of a column (go last). */
export function resolveDropTarget(
  columns: Columns,
  overId: string,
): { status: TaskStatus; index: number } | null {
  if (overId.startsWith('column:')) {
    const status = overId.slice('column:'.length) as TaskStatus;
    return TASK_STATUSES.includes(status) ? { status, index: columns[status].length } : null;
  }

  for (const status of TASK_STATUSES) {
    const index = columns[status].findIndex((task) => task.id === overId);
    if (index >= 0) return { status, index };
  }
  return null;
}
