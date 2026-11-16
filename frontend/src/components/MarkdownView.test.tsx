import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { MarkdownView } from './MarkdownView';

function renderMarkdown(source: string) {
  return render(<MarkdownView source={source} />);
}

describe('MarkdownView', () => {
  it('renders headings, lists, quotes and code', () => {
    const { container } = renderMarkdown(
      '# Title\n\n- one\n- two\n\n> quote\n\n`inline`\n\n```\nblock\n```',
    );

    expect(screen.getByRole('heading', { level: 1, name: 'Title' })).toBeInTheDocument();
    expect(container.querySelectorAll('li')).toHaveLength(2);
    expect(container.querySelector('blockquote')).toHaveTextContent('quote');
    expect(container.querySelectorAll('code').length).toBeGreaterThanOrEqual(2);
  });

  it('renders GitHub-style tables and task list checkboxes', () => {
    const { container } = renderMarkdown(
      '| a | b |\n|---|---|\n| 1 | 2 |\n\n- [x] done\n- [ ] todo',
    );

    expect(container.querySelector('table')).not.toBeNull();
    expect(container.querySelectorAll('input[type="checkbox"]')).toHaveLength(2);
  });

  it('renders links that open safely in a new tab', () => {
    renderMarkdown('[docs](https://example.com)');

    const link = screen.getByRole('link', { name: 'docs' });
    expect(link).toHaveAttribute('href', 'https://example.com');
    expect(link).toHaveAttribute('rel', expect.stringContaining('noopener'));
  });

  it('never creates a script element from raw HTML', () => {
    const { container } = renderMarkdown('hello <script>window.__hacked = true</script> world');

    expect(container.querySelector('script')).toBeNull();
    expect((window as unknown as { __hacked?: boolean }).__hacked).toBeUndefined();
  });

  it('does not keep event handler attributes from raw HTML', () => {
    const { container } = renderMarkdown('<img src="x" onerror="window.__hacked = true">');

    expect(container.querySelector('[onerror]')).toBeNull();
    expect(container.querySelector('img[onerror]')).toBeNull();
  });

  it('neutralises javascript: links', () => {
    const { container } = renderMarkdown('[click me](javascript:alert(1))');

    for (const link of container.querySelectorAll('a')) {
      expect(link.getAttribute('href') ?? '').not.toMatch(/^\s*javascript:/i);
    }
  });

  it('does not render iframes, styles or forms from raw HTML', () => {
    const { container } = renderMarkdown(
      '<iframe src="https://evil.example"></iframe><style>body{display:none}</style><form action="/x"><input></form>',
    );

    expect(container.querySelector('iframe')).toBeNull();
    expect(container.querySelector('style')).toBeNull();
    expect(container.querySelector('form')).toBeNull();
  });
});
