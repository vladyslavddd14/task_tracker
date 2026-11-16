import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { t } from '../../i18n';
import { QuickAdd } from './QuickAdd';

describe('QuickAdd', () => {
  it('rejects an empty title and does not call the API', async () => {
    const onAdd = vi.fn().mockResolvedValue(undefined);
    render(<QuickAdd onAdd={onAdd} />);

    await userEvent.click(screen.getByRole('button', { name: t.tasks.quickAddButton }));

    expect(await screen.findByRole('alert')).toHaveTextContent(t.validation.titleRequired);
    expect(onAdd).not.toHaveBeenCalled();
  });

  it('rejects a whitespace-only title', async () => {
    const onAdd = vi.fn();
    render(<QuickAdd onAdd={onAdd} />);

    await userEvent.type(screen.getByLabelText(t.tasks.quickAddLabel), '    {Enter}');

    expect(screen.getByRole('alert')).toHaveTextContent(t.validation.titleRequired);
    expect(onAdd).not.toHaveBeenCalled();
  });

  it('rejects a title over 200 characters', async () => {
    const onAdd = vi.fn();
    render(<QuickAdd onAdd={onAdd} />);

    await userEvent.click(screen.getByLabelText(t.tasks.quickAddLabel));
    await userEvent.paste('a'.repeat(201));
    await userEvent.keyboard('{Enter}');

    expect(screen.getByRole('alert')).toHaveTextContent(t.validation.titleTooLong(200));
    expect(onAdd).not.toHaveBeenCalled();
  });

  it('submits the trimmed title on Enter and clears the field', async () => {
    const onAdd = vi.fn().mockResolvedValue(undefined);
    render(<QuickAdd onAdd={onAdd} />);
    const input = screen.getByLabelText(t.tasks.quickAddLabel);

    await userEvent.type(input, '  Купити молоко  {Enter}');

    await waitFor(() => expect(onAdd).toHaveBeenCalledWith('Купити молоко'));
    await waitFor(() => expect(input).toHaveValue(''));
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('keeps the text and shows an error when the server fails', async () => {
    const onAdd = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    render(<QuickAdd onAdd={onAdd} />);
    const input = screen.getByLabelText(t.tasks.quickAddLabel);

    await userEvent.type(input, 'Task{Enter}');

    expect(await screen.findByRole('alert')).toHaveTextContent(t.errors.network);
    expect(input).toHaveValue('Task');
  });
});
