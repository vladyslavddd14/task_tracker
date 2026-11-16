import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { t } from '../../i18n';
import { LoginForm } from './LoginForm';
import { RegisterForm } from './RegisterForm';

describe('LoginForm', () => {
  it('validates the email and requires a password before calling the API', async () => {
    const onSubmit = vi.fn();
    render(<LoginForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(t.auth.email), 'not-an-email');
    await userEvent.click(screen.getByRole('button', { name: t.auth.submitLogin }));

    expect(screen.getByText(t.validation.emailInvalid)).toBeInTheDocument();
    expect(screen.getByText(t.validation.required)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('submits trimmed credentials', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<LoginForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(t.auth.email), ' anna@example.com ');
    await userEvent.type(screen.getByLabelText(t.auth.password), 'secret-pass');
    await userEvent.click(screen.getByRole('button', { name: t.auth.submitLogin }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith('anna@example.com', 'secret-pass'));
  });

  it('shows a clear message for wrong credentials', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new ApiError(401, 'Unauthorized'));
    render(<LoginForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(t.auth.email), 'anna@example.com');
    await userEvent.type(screen.getByLabelText(t.auth.password), 'wrong');
    await userEvent.click(screen.getByRole('button', { name: t.auth.submitLogin }));

    expect(await screen.findByText(t.auth.loginFailed)).toBeInTheDocument();
  });
});

describe('RegisterForm', () => {
  it('rejects a short password and an empty name', async () => {
    const onSubmit = vi.fn();
    render(<RegisterForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(t.auth.email), 'anna@example.com');
    await userEvent.type(screen.getByLabelText(t.auth.password), '1234567');
    await userEvent.click(screen.getByRole('button', { name: t.auth.submitRegister }));

    expect(screen.getByText(t.validation.passwordTooShort(8))).toBeInTheDocument();
    expect(screen.getByText(t.validation.nameRequired)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('offers all profile templates and submits the chosen one', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<RegisterForm onSubmit={onSubmit} />);

    expect(screen.getAllByRole('radio')).toHaveLength(4);
    await userEvent.type(screen.getByLabelText(t.auth.displayName), 'Анна');
    await userEvent.type(screen.getByLabelText(t.auth.email), 'anna@example.com');
    await userEvent.type(screen.getByLabelText(t.auth.password), 'password123');
    await userEvent.click(screen.getByRole('radio', { name: new RegExp(t.template.Student) }));
    await userEvent.click(screen.getByRole('button', { name: t.auth.submitRegister }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    expect(onSubmit.mock.calls[0][0]).toMatchObject({
      email: 'anna@example.com',
      displayName: 'Анна',
      profileTemplate: 'Student',
    });
  });

  it('tells the user when the email is already registered', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new ApiError(409, 'Conflict'));
    render(<RegisterForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(t.auth.displayName), 'Анна');
    await userEvent.type(screen.getByLabelText(t.auth.email), 'anna@example.com');
    await userEvent.type(screen.getByLabelText(t.auth.password), 'password123');
    await userEvent.click(screen.getByRole('button', { name: t.auth.submitRegister }));

    expect((await screen.findAllByText(t.auth.emailTaken)).length).toBeGreaterThan(0);
  });
});
