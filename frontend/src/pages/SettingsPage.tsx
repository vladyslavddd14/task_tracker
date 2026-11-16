import { t } from '../i18n';
import { CustomFieldManager } from '../features/settings/CustomFieldManager';
import { PasswordForm, ProfileForm, TemplateSection } from '../features/settings/ProfileSettings';
import { TagManager } from '../features/tags/TagManager';

export function SettingsPage() {
  return (
    <div className="page settings">
      <h1>{t.settings.title}</h1>
      <ProfileForm />
      <TemplateSection />
      <TagManager />
      <CustomFieldManager />
      <PasswordForm />
    </div>
  );
}
