import type { Messages } from '../i18n';
import { PageState } from './PageState';

export function Placeholder({ title, messages }: { title: string; messages: Messages }) {
  return <main className="content"><PageState title={title} tone="info" headingLevel={1}><p>{messages.comingSoon}</p></PageState></main>;
}
