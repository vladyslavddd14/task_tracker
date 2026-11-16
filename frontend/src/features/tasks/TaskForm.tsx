import { useState, type FormEvent } from 'react';
import {
  TASK_PRIORITIES,
  TASK_STATUSES,
  type CustomFieldDefinition,
  type Tag,
  type TaskDetails,
  type TaskPriority,
  type TaskStatus,
} from '../../api/types';
import { ErrorMessage, FieldError } from '../../components/Feedback';
import { t } from '../../i18n';
import { describeError } from '../../lib/errors';
import {
  LIMITS,
  validateCustomFieldValue,
  validateMaxLength,
  validateTitle,
} from '../../lib/validation';
import { CustomFieldInputs } from './CustomFieldInputs';
import { NotesEditor } from './NotesEditor';
import { toggleInList } from './filters';
import { useUpdateTask } from './queries';

interface TaskFormProps {
  task: TaskDetails;
  tags: Tag[];
  fields: CustomFieldDefinition[];
}

/** Edit form for all fields of a task. */
export function TaskForm({ task, tags, fields }: TaskFormProps) {
  const update = useUpdateTask(task.id);
  const [title, setTitle] = useState(task.title);
  const [description, setDescription] = useState(task.description ?? '');
  const [notes, setNotes] = useState(task.notesMarkdown ?? '');
  const [status, setStatus] = useState<TaskStatus>(task.status);
  const [priority, setPriority] = useState<TaskPriority>(task.priority);
  const [deadline, setDeadline] = useState(task.deadline ?? '');
  const [tagIds, setTagIds] = useState(task.tags.map((tag) => tag.id));
  const [fieldValues, setFieldValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(task.customFields.map((value) => [value.definitionId, value.value])),
  );
  const [errors, setErrors] = useState<Record<string, string | null>>({});
  const [saved, setSaved] = useState(false);

  function validate(): Record<string, string | null> {
    const result: Record<string, string | null> = {
      title: validateTitle(title),
      description: validateMaxLength(description, LIMITS.description),
      notes: validateMaxLength(notes, LIMITS.notes),
    };
    for (const field of fields) {
      result[field.id] = validateCustomFieldValue(field.type, fieldValues[field.id] ?? '');
    }
    return result;
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSaved(false);
    const found = validate();
    setErrors(found);
    if (Object.values(found).some(Boolean)) return;

    update.mutate(
      {
        title: title.trim(),
        description: description.trim() || null,
        notesMarkdown: notes.trim() ? notes : null,
        status,
        priority,
        deadline: deadline || null,
        tagIds,
        customFields: fields.map((field) => ({
          definitionId: field.id,
          value: fieldValues[field.id]?.trim() || null,
        })),
      },
      { onSuccess: () => setSaved(true) },
    );
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate>
      <div className="field">
        <label htmlFor="task-title">{t.tasks.title}</label>
        <input
          id="task-title"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          aria-invalid={Boolean(errors.title)}
          aria-describedby={errors.title ? 'task-title-error' : undefined}
        />
        <FieldError id="task-title-error" message={errors.title} />
      </div>

      <div className="form-row">
        <div className="field">
          <label htmlFor="task-status">{t.tasks.status}</label>
          <select
            id="task-status"
            value={status}
            onChange={(event) => setStatus(event.target.value as TaskStatus)}
          >
            {TASK_STATUSES.map((option) => (
              <option key={option} value={option}>
                {t.status[option]}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="task-priority">{t.tasks.priority}</label>
          <select
            id="task-priority"
            value={priority}
            onChange={(event) => setPriority(event.target.value as TaskPriority)}
          >
            {TASK_PRIORITIES.map((option) => (
              <option key={option} value={option}>
                {t.priority[option]}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="task-deadline">{t.tasks.deadline}</label>
          <input
            id="task-deadline"
            type="date"
            value={deadline}
            onChange={(event) => setDeadline(event.target.value)}
          />
        </div>
      </div>

      <div className="field">
        <label htmlFor="task-description">{t.tasks.description}</label>
        <textarea
          id="task-description"
          rows={3}
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          aria-invalid={Boolean(errors.description)}
          aria-describedby={errors.description ? 'task-description-error' : undefined}
        />
        <FieldError id="task-description-error" message={errors.description} />
      </div>

      <fieldset className="field">
        <legend>{t.tasks.tags}</legend>
        {tags.length === 0 ? (
          <p className="muted">{t.tasks.noTags}</p>
        ) : (
          <div className="checkbox-group">
            {tags.map((tag) => (
              <label key={tag.id} className="checkbox-row">
                <input
                  type="checkbox"
                  checked={tagIds.includes(tag.id)}
                  onChange={() => setTagIds(toggleInList(tagIds, tag.id))}
                />
                {tag.name}
              </label>
            ))}
          </div>
        )}
      </fieldset>

      {fields.length > 0 && (
        <fieldset className="field">
          <legend>{t.tasks.customFields}</legend>
          <CustomFieldInputs
            definitions={fields}
            values={fieldValues}
            errors={errors}
            onChange={(id, value) => setFieldValues({ ...fieldValues, [id]: value })}
          />
        </fieldset>
      )}

      <div className="field">
        <span className="label">{t.tasks.notes}</span>
        <NotesEditor value={notes} onChange={setNotes} error={errors.notes} />
      </div>

      {update.error && <ErrorMessage message={describeError(update.error)} />}
      <div className="form-actions">
        {saved && (
          <span className="success-text" role="status">
            {t.tasks.saved}
          </span>
        )}
        <button type="submit" className="button primary" disabled={update.isPending}>
          {update.isPending ? t.common.saving : t.common.save}
        </button>
      </div>
    </form>
  );
}
