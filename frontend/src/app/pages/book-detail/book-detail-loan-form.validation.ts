import { isIsoDateOnly } from '../../shared/date-only';

type LoanFormValidationInput = {
  borrowerName: string;
  borrowerEmail: string;
  dueAt: string;
  minimumLoanDueDate: string;
  minimumLoanDueDateLabel: string;
};

export function validateBookLoanForm({
  borrowerName,
  borrowerEmail,
  dueAt,
  minimumLoanDueDate,
  minimumLoanDueDateLabel,
}: LoanFormValidationInput): string | null {
  const trimmedBorrowerName = borrowerName.trim();
  const trimmedBorrowerEmail = borrowerEmail.trim();

  if (!trimmedBorrowerName) {
    return 'Add meg a kölcsönző nevét.';
  }

  if (!trimmedBorrowerEmail) {
    return 'Add meg a kölcsönző e-mail-címét.';
  }

  if (!isValidEmail(trimmedBorrowerEmail)) {
    return 'Adj meg érvényes e-mail-címet.';
  }

  if (!dueAt) {
    return 'Add meg a kölcsönzés határidejét.';
  }

  if (!isIsoDateOnly(dueAt)) {
    return 'A határidő formátuma YYYY-MM-DD legyen.';
  }

  if (dueAt < minimumLoanDueDate) {
    return `A határidő legkorábban ${minimumLoanDueDateLabel} lehet.`;
  }

  return null;
}

function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}
