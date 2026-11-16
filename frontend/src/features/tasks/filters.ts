import type { TaskFilters } from '../../api/types';

export const defaultFilters: TaskFilters = {
  status: [],
  priority: '',
  tagIds: [],
  deadline: '',
  deadlineFrom: '',
  deadlineTo: '',
  q: '',
  customFieldId: '',
  customFieldValue: '',
  sort: 'priority',
  dir: '',
};

/** True when anything other than the default sorting is set. */
export function hasActiveFilters(filters: TaskFilters): boolean {
  return (
    filters.status.length > 0 ||
    filters.priority !== '' ||
    filters.tagIds.length > 0 ||
    filters.deadline !== '' ||
    filters.q.trim() !== '' ||
    (filters.customFieldId !== '' && filters.customFieldValue.trim() !== '')
  );
}

/** Adds the value to the list, or removes it when it is already there. */
export function toggleInList<T>(list: T[], value: T): T[] {
  return list.includes(value) ? list.filter((item) => item !== value) : [...list, value];
}
