import type { JSX, ReactNode } from 'react';
import { Icon } from './Icon';

// A page-level state that takes the place of the content: a failure (tone "error", announced at once) or an
// informational screen. Follows the page-level error in design/Messages.dc.html. The heading is an H2 by default;
// pass headingLevel={1} when the state is the page's only heading.
export function PageState({ title, tone, action, children, headingLevel = 2 }: {
  title: string;
  tone: 'error' | 'info';
  action?: ReactNode;
  children?: ReactNode;
  headingLevel?: 1 | 2;
}): JSX.Element {
  const Heading = headingLevel === 1 ? 'h1' : 'h2';
  return <section className={`card page-state page-state-${tone}`}>
    <div className="page-state-art" aria-hidden="true">
      <span /><span /><span />
      <span className="page-state-badge"><Icon name={tone === 'error' ? 'close' : 'search'} size={20} /></span>
    </div>
    <Heading className="page-state-title" role={tone === 'error' ? 'alert' : undefined}>{title}</Heading>
    {children && <div className="page-state-body">{children}</div>}
    {action && <div className="page-state-action">{action}</div>}
  </section>;
}
