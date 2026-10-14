import type { JSX, ReactNode } from 'react';
import { Icon } from './Icon';

export type AlertVariant = 'info' | 'warning' | 'error' | 'success';

// Badge glyphs from design/Messages.dc.html:18,31,44,57, drawn in white on the variant colour.
const badgeGlyphs: Record<AlertVariant, string> = {
  info: 'M12 11v6M12 7h.01',
  warning: 'M12 7v6M12 17h.01',
  error: 'M7 7l10 10M17 7 7 17',
  success: 'm6 12.5 4 4L18 8.5',
};

// One in-content message. Errors default to role="alert", the other variants to role="status"; pass role={null}
// when the Alert is rendered inside a persistent live region owned by the caller.
export function Alert({ variant, children, id, compact = false, role, action, className }: {
  variant: AlertVariant;
  children: ReactNode;
  id?: string;
  compact?: boolean;
  role?: 'alert' | 'status' | null;
  action?: ReactNode;
  className?: string;
}): JSX.Element {
  const resolvedRole = role === undefined ? (variant === 'error' ? 'alert' : 'status') : role ?? undefined;
  const classes = ['alert', `alert-${variant}`];
  if (compact) classes.push('alert-compact');
  if (className) classes.push(className);
  return <div id={id} className={classes.join(' ')} role={resolvedRole}>
    <div className="alert-body">
      <span className="alert-badge" aria-hidden="true">
        <svg className="alert-glyph" viewBox="0 0 24 24" focusable="false"><path d={badgeGlyphs[variant]} /></svg>
      </span>
      <div className="alert-text">{children}</div>
    </div>
    {action && <div className="alert-action">{action}</div>}
  </div>;
}

// A field-level error. It has no role: the input reaches it through aria-describedby, and the caller keeps any
// form-level announcement.
export function FieldError({ id, children }: { id: string; children: ReactNode }): JSX.Element {
  return <span id={id} className="field-error"><Icon name="alertCircle" size={16} />{children}</span>;
}
