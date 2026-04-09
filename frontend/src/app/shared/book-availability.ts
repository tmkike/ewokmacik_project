import { Book } from '../models/book';

export const BOOK_AVAILABILITY_LABELS = {
  loaned: 'Kikölcsönözve',
  available: 'Elérhető',
  unavailable: 'Nem elérhető',
} as const;

export function getBookAvailabilityLabel(book: Pick<Book, 'available' | 'hasActiveLoan'>): string {
  if (book.hasActiveLoan) {
    return BOOK_AVAILABILITY_LABELS.loaned;
  }

  return book.available
    ? BOOK_AVAILABILITY_LABELS.available
    : BOOK_AVAILABILITY_LABELS.unavailable;
}
