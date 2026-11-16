import type { CustomFieldDefinition } from '../../api/types';
import { FieldError } from '../../components/Feedback';
import { t } from '../../i18n';

interface CustomFieldInputsProps {
  definitions: CustomFieldDefinition[];
  values: Record<string, string>;
  errors: Record<string, string | null>;
  onChange: (definitionId: string, value: string) => void;
}

/** Inputs for the user's own task fields. The input type follows the field type. */
export function CustomFieldInputs({
  definitions,
  values,
  errors,
  onChange,
}: CustomFieldInputsProps) {
  return (
    <div className="custom-fields">
      {definitions.map((field) => {
        const id = `custom-${field.id}`;
        const value = values[field.id] ?? '';
        const error = errors[field.id];
        const common = {
          id,
          'aria-invalid': Boolean(error),
          'aria-describedby': error ? `${id}-error` : undefined,
        };

        return (
          <div className="field" key={field.id}>
            <label htmlFor={id}>{field.name}</label>
            {field.type === 'Checkbox' ? (
              <select
                {...common}
                value={value}
                onChange={(event) => onChange(field.id, event.target.value)}
              >
                <option value="">{t.common.none}</option>
                <option value="true">{t.common.yes}</option>
                <option value="false">{t.common.no}</option>
              </select>
            ) : (
              <input
                {...common}
                type={field.type === 'Date' ? 'date' : 'text'}
                inputMode={field.type === 'Number' ? 'decimal' : undefined}
                value={value}
                onChange={(event) => onChange(field.id, event.target.value)}
              />
            )}
            <FieldError id={`${id}-error`} message={error} />
          </div>
        );
      })}
    </div>
  );
}
