import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { api } from '../../api/endpoints';
import type { Tag } from '../../api/types';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { EmptyState, ErrorMessage, FieldError, Loading } from '../../components/Feedback';
import { t } from '../../i18n';
import { describeError } from '../../lib/errors';
import { LIMITS, validateTitle } from '../../lib/validation';
import { taskKeys, useTags } from '../tasks/queries';

const DEFAULT_COLOR = '#3b82f6';

function useTagMutations() {
  const queryClient = useQueryClient();
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['tags'] });
    queryClient.invalidateQueries({ queryKey: taskKeys.all });
  };
  return {
    create: useMutation({
      mutationFn: ({ name, color }: { name: string; color: string }) =>
        api.tags.create(name, color),
      onSuccess: refresh,
    }),
    update: useMutation({
      mutationFn: ({ id, name, color }: { id: string; name: string; color: string | null }) =>
        api.tags.update(id, name, color),
      onSuccess: refresh,
    }),
    remove: useMutation({ mutationFn: (id: string) => api.tags.remove(id), onSuccess: refresh }),
  };
}

function TagRow({
  tag,
  onSave,
  onDelete,
  busy,
}: {
  tag: Tag;
  onSave: (name: string, color: string | null) => void;
  onDelete: () => void;
  busy: boolean;
}) {
  const [name, setName] = useState(tag.name);
  const [color, setColor] = useState(tag.color ?? DEFAULT_COLOR);
  const [colorChanged, setColorChanged] = useState(false);
  const error = validateTitle(name, LIMITS.tagName);
  const changed = name.trim() !== tag.name || colorChanged;

  return (
    <li className="manager-row">
      <input
        type="color"
        value={color}
        aria-label={`${t.settings.tagColor}: ${tag.name}`}
        onChange={(event) => {
          setColor(event.target.value);
          setColorChanged(true);
        }}
      />
      <input
        value={name}
        aria-label={`${t.settings.tagName}: ${tag.name}`}
        aria-invalid={Boolean(error)}
        onChange={(event) => setName(event.target.value)}
        maxLength={LIMITS.tagName + 10}
      />
      <button
        type="button"
        className="button small"
        disabled={!changed || Boolean(error) || busy}
        onClick={() => onSave(name.trim(), colorChanged ? color : tag.color)}
      >
        {t.common.save}
      </button>
      <button
        type="button"
        className="icon-button danger-text"
        onClick={onDelete}
        aria-label={`${t.common.delete}: ${tag.name}`}
      >
        ×
      </button>
    </li>
  );
}

/** Create, rename, recolor and delete tags. Deleting a tag only removes it from tasks. */
export function TagManager() {
  const tags = useTags();
  const { create, update, remove } = useTagMutations();
  const [name, setName] = useState('');
  const [color, setColor] = useState(DEFAULT_COLOR);
  const [error, setError] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<Tag | null>(null);

  function handleAdd(event: FormEvent) {
    event.preventDefault();
    const problem = validateTitle(name, LIMITS.tagName);
    setError(problem);
    if (problem) return;
    create.mutate({ name: name.trim(), color }, { onSuccess: () => setName('') });
  }

  const failure = create.error ?? update.error ?? remove.error;

  return (
    <section className="card" aria-labelledby="tags-heading">
      <h2 id="tags-heading">{t.settings.tags}</h2>
      {tags.isPending && <Loading />}
      {tags.isError && (
        <ErrorMessage message={describeError(tags.error)} onRetry={() => tags.refetch()} />
      )}
      {failure && <ErrorMessage message={describeError(failure, t.errors.tagExists)} />}

      {tags.data &&
        (tags.data.length === 0 ? (
          <EmptyState message={t.settings.tagsEmpty} />
        ) : (
          <ul className="manager-list">
            {tags.data.map((tag) => (
              <TagRow
                key={`${tag.id}:${tag.name}:${tag.color}`}
                tag={tag}
                busy={update.isPending}
                onSave={(newName, newColor) =>
                  update.mutate({ id: tag.id, name: newName, color: newColor })
                }
                onDelete={() => setPendingDelete(tag)}
              />
            ))}
          </ul>
        ))}

      <form className="inline-form" onSubmit={handleAdd} noValidate>
        <input
          type="color"
          value={color}
          aria-label={t.settings.tagColor}
          onChange={(event) => setColor(event.target.value)}
        />
        <input
          value={name}
          placeholder={t.settings.tagName}
          aria-label={t.settings.tagName}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? 'tag-add-error' : undefined}
          onChange={(event) => setName(event.target.value)}
        />
        <button type="submit" className="button" disabled={create.isPending}>
          {t.settings.tagAdd}
        </button>
      </form>
      <FieldError id="tag-add-error" message={error} />

      {pendingDelete && (
        <ConfirmDialog
          title={t.settings.tagDeleteTitle}
          message={t.settings.tagDeleteText(pendingDelete.name)}
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
