import type { IsoDate, TaskListItem } from '../../api/types';
import { addDays, addMonths, endOfMonth, startOfMonth, startOfWeek } from '../../lib/dates';

export type CalendarMode = 'month' | 'week';

export interface VisibleRange {
  /** First and last day of the grid (a month grid starts on Monday and may show days of neighbouring months). */
  from: IsoDate;
  to: IsoDate;
  days: IsoDate[];
}

/** Days shown for the period around `anchor`. Weeks start on Monday. */
export function getVisibleRange(mode: CalendarMode, anchor: IsoDate): VisibleRange {
  const first = mode === 'week' ? startOfWeek(anchor) : startOfWeek(startOfMonth(anchor));
  const lastDayOfPeriod = mode === 'week' ? addDays(first, 6) : endOfMonth(anchor);
  const last = addDays(startOfWeek(lastDayOfPeriod), 6);

  const days: IsoDate[] = [];
  for (let day = first; day <= last; day = addDays(day, 1)) {
    days.push(day);
  }
  return { from: first, to: last, days };
}

/** Moves the anchor to the previous (-1) or next (1) period. */
export function shiftAnchor(mode: CalendarMode, anchor: IsoDate, direction: -1 | 1): IsoDate {
  return mode === 'week' ? addDays(anchor, direction * 7) : addMonths(anchor, direction);
}

export function isInSameMonth(day: IsoDate, anchor: IsoDate): boolean {
  return day.slice(0, 7) === anchor.slice(0, 7);
}

/** Groups tasks by their deadline date. Tasks without a deadline are skipped. */
export function groupByDeadline(tasks: TaskListItem[]): Map<IsoDate, TaskListItem[]> {
  const groups = new Map<IsoDate, TaskListItem[]>();
  for (const task of tasks) {
    if (!task.deadline) continue;
    const list = groups.get(task.deadline) ?? [];
    list.push(task);
    groups.set(task.deadline, list);
  }
  return groups;
}
