import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { api } from '../../api/endpoints';
import {
  CUSTOM_FIELD_TYPES,
  type CustomFieldDefinition,
  type CustomFieldType,
} from '../../api/types';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { EmptyState, ErrorMessage, FieldError, Loading } from '../../components/Feedback';
import { t } from '../../i18n';
import { describeError } from '../../lib/errors';
import { LIMITS, validateTitle } from '../../lib/validation';
import { taskKeys, useCustomFields } from '../tasks/queries';

function useFieldMutations() {
  const queryClient = useQueryClient();
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['custom-fields'] });
    queryClient.invalidateQueries({ queryKey: taskKeys.all });
  };
  return {
    create: useMutation({
      mutationFn: ({ name, type }: { name: string; type: CustomFieldType }) =>
        api.customFields.create(name, type),
      onSuccess: refresh,
    }),
    update: useMutation({
      mutationFn: (field: CustomFieldDefinition) =>
        api.customFields.update(field.id, field.name, field.type, field.position),
      onSuccess: refresh,
    }),
    remove: useMutation({
      mutationFn: (id: string) => api.customFields.remove(id),
      onSuccess: refresh,
    }),
  };
}

function FieldRow({
  field,
  isFirst,
  isLast,
  busy,
  onRename,
  onMove,
  onDelete,
}: {
  field: CustomFieldDefinition;
  isFirst: boolean;
  isLast: boolean;
  busy: boolean;
  onRename: (name: string) => void;
  onMove: (direction: -1 | 1) => void;
  onDelete: () => void;
}) {
  const [name, setName] = useState(field.name);
  const error = validateTitle(name, LIMITS.fieldName);

  return (
    <li className="manager-row">
      <input
        value={name}
        aria-label={`${t.settings.fieldName}: ${field.name}`}
        aria-invalid={Boolean(error)}
        onChange={(event) => setName(event.target.value)}
      />
      <span className="badge">{t.fieldType[field.type]}</span>
      <button
        type="button"
        className="button small"
        disabled={name.trim() === field.name || Boolean(error) || busy}
        onClick={() => onRename(name.trim())}
      >
        {t.common.save}
      </button>
      <button
        type="button"
        className="icon-button"
        disabled={isFirst || busy}
        onClick={() => onMove(-1)}
        aria-label={`${t.common.moveUp}: ${field.name}`}
      >
        ↑
      </button>
      <button
        type="button"
        className="icon-button"
        disabled={isLast || busy}
        onClick={() => onMove(1)}
        aria-label={`${t.common.moveDown}: ${field.name}`}
      >
        ↓
      </button>
      <button
        type="button"
        className="icon-button danger-text"
        onClick={onDelete}
        aria-label={`${t.common.delete}: ${field.name}`}
      >
        ×
      </button>
    </li>
  );
}

/** Create, rename, reorder and delete the user's own task fields. */
export function CustomFieldManager() {
  const fields = useCustomFields();
  const { create, update, remove } = useFieldMutations();
  const [name, setName] = useState('');
  const [type, setType] = useState<CustomFieldType>('Text');
  const [error, setError] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<CustomFieldDefinition | null>(null);

  const sorted = [...(fields.data ?? [])].sort((a, b) => a.position - b.position);

  function handleAdd(event: FormEvent) {
    event.preventDefault();
    const problem = validateTitle(name, LIMITS.fieldName);
    setError(problem);
    if (problem) return;
    create.mutate({ name: name.trim(), type }, { onSuccess: () => setName('') });
  }

  // Reordering swaps the positions of two neighbouring fields.
  function move(index: number, direction: -1 | 1) {
    const current = sorted[index];
    const neighbour = sorted[index + direction];
    if (!neighbour) return;
    update.mutate({ ...current, position: neighbour.position });
    update.mutate({ ...neighbour, position: current.position });
  }

  const failure = create.error ?? update.error ?? remove.error;

  return (
    <section className="card" aria-labelledby="fields-heading">
      <h2 id="fields-heading">{t.settings.fields}</h2>
      <p className="hint">{t.settings.fieldsHelp}</p>
      {fields.isPending && <Loading />}
      {fields.isError && (
        <ErrorMessage message={describeError(fields.error)} onRetry={() => fields.refetch()} />
      )}
      {failure && <ErrorMessage message={describeError(failure, t.errors.fieldExists)} />}

      {fields.data &&
        (sorted.length === 0 ? (
          <EmptyState message={t.settings.fieldsEmpty} />
        ) : (
          <ul className="manager-list">
            {sorted.map((field, index) => (
              <FieldRow
                key={`${field.id}:${field.name}`}
                field={field}
                isFirst={index === 0}
                isLast={index === sorted.length - 1}
                busy={update.isPending}
                onRename={(newName) => update.mutate({ ...field, name: newName })}
                onMove={(direction) => move(index, direction)}
                onDelete={() => setPendingDelete(field)}
              />
            ))}
          </ul>
        ))}

      <form className="inline-form" onSubmit={handleAdd} noValidate>
        <input
          value={name}
          placeholder={t.settings.fieldName}
          aria-label={t.settings.fieldName}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? 'field-add-error' : undefined}
          onChange={(event) => setName(event.target.value)}
        />
        <select
          value={type}
          aria-label={t.settings.fieldType}
          onChange={(event) => setType(event.target.value as CustomFieldType)}
        >
          {CUSTOM_FIELD_TYPES.map((option) => (
            <option key={option} value={option}>
              {t.fieldType[option]}
            </option>
          ))}
        </select>
        <button type="submit" className="button" disabled={create.isPending}>
          {t.settings.fieldAdd}
        </button>
      </form>
      <FieldError id="field-add-error" message={error} />

      {pendingDelete && (
        <ConfirmDialog
          title={t.settings.fieldDeleteTitle}
          message={t.settings.fieldDeleteText(pendingDelete.name)}
          busy={remove.isPending}
          onCancel={() => setPendingDelete(null)}
          onConfirm={() =>
            remove.mutate(pendingDelete.id, { onSettled: () => setPendingDelete(null) })
          }
        />
      )}
    </section>
  );
}
