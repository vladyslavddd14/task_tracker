import { useEffect, useRef, useState } from 'react';
import {
  TASK_PRIORITIES,
  TASK_STATUSES,
  type CustomFieldDefinition,
  type SortDirection,
  type SortField,
  type Tag,
  type TaskFilters as Filters,
  type TaskPriority,
} from '../../api/types';
import { FieldError } from '../../components/Feedback';
import { useDebounce } from '../../hooks/hooks';
import { t } from '../../i18n';
import { validateDateRange } from '../../lib/validation';
import { defaultFilters, hasActiveFilters, toggleInList } from './filters';

interface TaskFiltersProps {
  filters: Filters;
  onChange: (filters: Filters) => void;
  tags: Tag[];
  fields: CustomFieldDefinition[];
}

const SORT_OPTIONS: { value: SortField; label: string }[] = [
  { value: 'priority', label: t.filters.sortPriority },
  { value: 'deadline', label: t.filters.sortDeadline },
  { value: 'createdAt', label: t.filters.sortCreatedAt },
  { value: 'title', label: t.filters.sortTitle },
];

const DEADLINE_OPTIONS: { value: Filters['deadline']; label: string }[] = [
  { value: '', label: t.filters.deadlineAny },
  { value: 'overdue', label: t.filters.deadlineOverdue },
  { value: 'today', label: t.filters.deadlineToday },
  { value: 'upcoming', label: t.filters.deadlineUpcoming },
  { value: 'none', label: t.filters.deadlineNone },
  { value: 'range', label: t.filters.deadlineRange },
];

/** Filter panel shared by the list, kanban and calendar views. */
export function TaskFilters({ filters, onChange, tags, fields }: TaskFiltersProps) {
  const [search, setSearch] = useState(filters.q);
  const debouncedSearch = useDebounce(search, 300);
  const latest = useRef({ filters, onChange });
  latest.current = { filters, onChange };

  // Apply the search text after the user stops typing.
  useEffect(() => {
    const current = latest.current;
    if (debouncedSearch !== current.filters.q) {
      current.onChange({ ...current.filters, q: debouncedSearch });
    }
  }, [debouncedSearch]);

  // Follow external changes, for example the reset button.
  useEffect(() => {
    setSearch(filters.q);
  }, [filters.q]);

  const set = (patch: Partial<Filters>) => onChange({ ...filters, ...patch });
  const rangeError =
    filters.deadline === 'range'
      ? validateDateRange(filters.deadlineFrom, filters.deadlineTo)
      : null;
  const selectedField = fields.find((field) => field.id === filters.customFieldId);

  return (
    <details className="filters" open>
      <summary>{t.filters.title}</summary>
      <div className="filters-grid">
        <div className="field">
          <label htmlFor="filter-search">{t.filters.search}</label>
          <input
            id="filter-search"
            type="search"
            value={search}
            placeholder={t.filters.searchPlaceholder}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>

        <fieldset className="field">
          <legend>{t.filters.status}</legend>
          <div className="checkbox-group">
            {TASK_STATUSES.map((status) => (
              <label key={status} className="checkbox-row">
                <input
                  type="checkbox"
                  checked={filters.status.includes(status)}
                  onChange={() => set({ status: toggleInList(filters.status, status) })}
                />
                {t.status[status]}
              </label>
            ))}
          </div>
        </fieldset>

        <div className="field">
          <label htmlFor="filter-priority">{t.filters.priority}</label>
          <select
            id="filter-priority"
            value={filters.priority}
            onChange={(event) => set({ priority: event.target.value as TaskPriority | '' })}
          >
            <option value="">{t.filters.anyPriority}</option>
            {TASK_PRIORITIES.map((priority) => (
              <option key={priority} value={priority}>
                {t.priority[priority]}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="filter-deadline">{t.filters.deadline}</label>
          <select
            id="filter-deadline"
            value={filters.deadline}
            onChange={(event) => set({ deadline: event.target.value as Filters['deadline'] })}
          >
            {DEADLINE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          {filters.deadline === 'range' && (
            <div className="range-row">
              <label>
                <span className="visually-hidden">{t.filters.from}</span>
                <input
                  type="date"
                  value={filters.deadlineFrom}
                  aria-label={t.filters.from}
                  onChange={(event) => set({ deadlineFrom: event.target.value })}
                />
              </label>
              <label>
                <span className="visually-hidden">{t.filters.to}</span>
                <input
                  type="date"
                  value={filters.deadlineTo}
                  aria-label={t.filters.to}
                  onChange={(event) => set({ deadlineTo: event.target.value })}
                />
              </label>
            </div>
          )}
          <FieldError id="filter-range-error" message={rangeError} />
        </div>

        {tags.length > 0 && (
          <fieldset className="field">
            <legend>{t.filters.tags}</legend>
            <div className="checkbox-group">
              {tags.map((tag) => (
                <label key={tag.id} className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={filters.tagIds.includes(tag.id)}
                    onChange={() => set({ tagIds: toggleInList(filters.tagIds, tag.id) })}
                  />
                  {tag.name}
                </label>
              ))}
            </div>
          </fieldset>
        )}

        {fields.length > 0 && (
          <div className="field">
            <label htmlFor="filter-field">{t.filters.customField}</label>
            <select
              id="filter-field"
              value={filters.customFieldId}
              onChange={(event) => set({ customFieldId: event.target.value, customFieldValue: '' })}
            >
              <option value="">{t.filters.anyField}</option>
              {fields.map((field) => (
                <option key={field.id} value={field.id}>
                  {field.name}
                </option>
              ))}
            </select>
            {selectedField &&
              (selectedField.type === 'Checkbox' ? (
                <select
                  aria-label={t.filters.customFieldValue}
                  value={filters.customFieldValue}
                  onChange={(event) => set({ customFieldValue: event.target.value })}
                >
                  <option value="">{t.common.none}</option>
                  <option value="true">{t.common.yes}</option>
                  <option value="false">{t.common.no}</option>
                </select>
              ) : (
                <input
                  aria-label={t.filters.customFieldValue}
                  value={filters.customFieldValue}
                  placeholder={t.filters.customFieldValue}
                  onChange={(event) => set({ customFieldValue: event.target.value })}
                />
              ))}
          </div>
        )}

        <div className="field">
          <label htmlFor="filter-sort">{t.filters.sort}</label>
          <select
            id="filter-sort"
            value={filters.sort}
            onChange={(event) => set({ sort: event.target.value as SortField })}
          >
            {SORT_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="filter-dir">{t.filters.direction}</label>
          <select
            id="filter-dir"
            value={filters.dir}
            onChange={(event) => set({ dir: event.target.value as SortDirection | '' })}
          >
            <option value="">{t.filters.dirDefault}</option>
            <option value="asc">{t.filters.dirAsc}</option>
            <option value="desc">{t.filters.dirDesc}</option>
          </select>
        </div>
      </div>

      <button
        type="button"
        className="button small"
        onClick={() => onChange(defaultFilters)}
        disabled={!hasActiveFilters(filters) && filters.sort === 'priority' && filters.dir === ''}
      >
        {t.filters.reset}
      </button>
    </details>
  );
}
