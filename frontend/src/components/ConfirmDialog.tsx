import { t } from '../i18n';
import { Modal } from './Modal';

interface ConfirmDialogProps {
  title: string;
  message: string;
  confirmLabel?: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/** Asks for confirmation before a destructive action (for example deleting a task). */
export function ConfirmDialog({
  title,
  message,
  confirmLabel = t.common.delete,
  busy,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Modal title={title} onClose={onCancel}>
      <p>{message}</p>
      <div className="form-actions">
        <button type="button" className="button" onClick={onCancel}>
          {t.common.cancel}
        </button>
        <button type="button" className="button danger" onClick={onConfirm} disabled={busy}>
          {confirmLabel}
        </button>
      </div>
    </Modal>
  );
}
