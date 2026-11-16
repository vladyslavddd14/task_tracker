import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { useViewMode } from '../../hooks/hooks';
import { t } from '../../i18n';
import { ViewSwitcher } from './ViewSwitcher';

function Harness() {
  const [view, setView] = useViewMode();
  return (
    <>
      <ViewSwitcher value={view} onChange={setView} />
      <p data-testid="current">{view}</p>
    </>
  );
}

describe('ViewSwitcher', () => {
  it('shows the three views and marks the selected one', () => {
    render(<ViewSwitcher value="board" onChange={() => {}} />);

    expect(screen.getByRole('button', { name: t.views.list })).toHaveAttribute(
      'aria-pressed',
      'false',
    );
    expect(screen.getByRole('button', { name: t.views.board })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByRole('button', { name: t.views.calendar })).toBeInTheDocument();
  });

  it('reports the chosen view', async () => {
    const onChange = vi.fn();
    render(<ViewSwitcher value="list" onChange={onChange} />);

    await userEvent.click(screen.getByRole('button', { name: t.views.calendar }));

    expect(onChange).toHaveBeenCalledWith('calendar');
  });
});

describe('useViewMode', () => {
  it('starts with the list', () => {
    render(<Harness />);

    expect(screen.getByTestId('current')).toHaveTextContent('list');
  });

  it('remembers the chosen view in the browser', async () => {
    const first = render(<Harness />);
    await userEvent.click(screen.getByRole('button', { name: t.views.board }));
    expect(screen.getByTestId('current')).toHaveTextContent('board');
    first.unmount();

    render(<Harness />);

    expect(screen.getByTestId('current')).toHaveTextContent('board');
  });

  it('ignores an unknown stored value', () => {
    window.localStorage.setItem('tt.view', 'hologram');

    render(<Harness />);

    expect(screen.getByTestId('current')).toHaveTextContent('list');
  });
});
