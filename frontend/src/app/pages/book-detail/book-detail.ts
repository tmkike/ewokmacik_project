import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize, timeout } from 'rxjs';

import { Book } from '../../models/book';
import { Loan, LoanCreatePayload } from '../../models/loan';
import { BookService } from '../../services/book.service';
import { LoanService } from '../../services/loan.service';
import { BOOK_AVAILABILITY_LABELS } from '../../shared/book-availability';
import { extractDateOnly, formatDateOnlyLabel, isIsoDateOnly } from '../../shared/date-only';
import { validateBookLoanForm } from './book-detail-loan-form.validation';

type LoanFormModel = {
  borrowerName: string;
  borrowerEmail: string;
  dueAt: string;
  notes: string;
};

type AvailabilityState = 'available' | 'unavailable' | 'loaned';

const ACTIVE_LOAN_CONFLICT_CODE = 'ACTIVE_LOAN_CONFLICT';

@Component({
  selector: 'app-book-detail',
  imports: [CommonModule, FormsModule],
  templateUrl: './book-detail.html',
  styleUrl: './book-detail.scss',
})
export class BookDetail implements OnInit {
  book?: Book;
  bookId = '';
  activeLoan?: Loan;
  hasActiveLoanConflict = false;
  bookLoadFailed = false;
  private activeLoanRequestId = 0;
  private activeLoanRequestBookId = '';
  loading = false;
  saving = false;
  loanLoading = false;
  loanProcessing = false;
  successMessage = '';
  errorMessage = '';
  loanErrorMessage = '';
  loanForm: LoanFormModel = this.createEmptyLoanForm();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly bookService: BookService,
    private readonly loanService: LoanService,
    private readonly changeDetectorRef: ChangeDetectorRef,
  ) {
    const navigationBook = this.router.getCurrentNavigation()?.extras.state?.['book'] as Book | undefined;

    // Reuse the list row state during navigation so the page can paint immediately
    // before the detail request refreshes the book from the backend.
    if (navigationBook) {
      this.book = { ...navigationBook };
    }
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');

      if (!id) {
        this.clearLoadedBookState();
        this.bookLoadFailed = true;
        this.errorMessage = 'Hiányzó könyvazonosító.';
        this.loading = false;
        this.cancelActiveLoanLookup();
        return;
      }

      this.bookId = id;
      this.primeLoanStateFromNavigationBook(id);
      this.loadBook(id);
    });
  }

  get availabilityState(): AvailabilityState {
    // The UI treats any confirmed or suspected active-loan state as "loaned",
    // even while the detailed loan payload is still being resolved.
    if (this.activeLoan || this.book?.hasActiveLoan || this.hasActiveLoanConflict) {
      return 'loaned';
    }

    if (this.book?.available) {
      return 'available';
    }

    return 'unavailable';
  }

  get canCreateLoan(): boolean {
    return Boolean(this.book?._id && this.availabilityState === 'available' && !this.activeLoan);
  }

  get showLoanForm(): boolean {
    return this.canCreateLoan;
  }

  get minimumLoanDueDate(): string {
    const tomorrow = new Date();
    tomorrow.setHours(0, 0, 0, 0);
    tomorrow.setDate(tomorrow.getDate() + 1);
    return this.toLocalDateValue(tomorrow);
  }

  get minimumLoanDueDateLabel(): string {
    const [year, month, day] = this.minimumLoanDueDate.split('-');
    return `${year}.${month}.${day}.`;
  }

  get showLoanTerminationButton(): boolean {
    return Boolean(this.activeLoan);
  }

  get canDeleteBook(): boolean {
    return this.availabilityState !== 'loaned'
      && !this.activeLoan
      && !this.hasActiveLoanConflict
      && !this.loanLoading
      && !this.loanProcessing
      && !this.saving;
  }

  get bookStatusLabel(): string {
    if (this.availabilityState === 'loaned') {
      return BOOK_AVAILABILITY_LABELS.loaned;
    }

    if (this.availabilityState === 'available') {
      return BOOK_AVAILABILITY_LABELS.available;
    }

    return BOOK_AVAILABILITY_LABELS.unavailable;
  }

  get isLoanFormReadOnly(): boolean {
    return Boolean(this.activeLoan) || this.loanLoading || this.loanProcessing || this.saving;
  }

  loadBook(id: string): void {
    this.loading = !this.book;
    this.bookLoadFailed = false;
    this.errorMessage = '';
    this.loanErrorMessage = '';

    this.bookService.getBook(id).pipe(
      finalize(() => {
        this.loading = false;
        this.refreshView();
      }),
    ).subscribe({
      next: (book) => {
        this.book = book;
        this.bookLoadFailed = false;
        this.syncLoanState(book);
        this.refreshView();
      },
      error: (error: HttpErrorResponse) => {
        this.cancelActiveLoanLookup();
        // After a failed reload, do not leave stale edit or loan state visible from navigation.
        this.clearLoadedBookState();
        this.bookLoadFailed = true;
        this.errorMessage = this.extractErrorMessage(error, `Nem sikerült betölteni a könyvet. Azonosító: ${id}`);
        this.refreshView();
      },
    });
  }

  loadActiveLoan(bookId: string): void {
    if (this.loanLoading && this.activeLoanRequestBookId === bookId) {
      return;
    }

    const requestId = this.beginActiveLoanLookup(bookId);

    this.loanService.getActiveLoanForBook(bookId).pipe(
      timeout(5000),
    ).subscribe({
      next: (loan) => {
        // Requests can overlap during reloads, so only the newest lookup may update the screen.
        if (!this.isCurrentActiveLoanLookup(bookId, requestId)) {
          return;
        }

        if (loan) {
          this.applyLoadedActiveLoan(loan);
          this.finishActiveLoanLookup(requestId);
          this.refreshView();
          return;
        }

        this.loadActiveLoanFallback(bookId, requestId);
      },
      error: () => {
        this.handleActiveLoanLookupError(bookId, requestId);
        this.refreshView();
      },
    });
  }

  saveBook(): void {
    if (!this.book?._id || this.saving || this.loanProcessing) {
      return;
    }

    this.saving = true;
    this.successMessage = '';
    this.errorMessage = '';

    this.bookService.updateBook(this.book._id, this.book).pipe(
      finalize(() => {
        this.saving = false;
      }),
    ).subscribe({
      next: (updatedBook) => {
        this.book = updatedBook;
        this.syncLoanState(updatedBook);
        this.navigateToBooks('A könyv mentése sikeres.');
      },
      error: (error: HttpErrorResponse) => {
        this.handleAvailabilityConflict(error);
        this.errorMessage = this.extractErrorMessage(error, 'Nem sikerült elmenteni a változásokat.');
        this.successMessage = '';
        this.refreshView();
      },
    });
  }

  saveLoan(): void {
    if (this.loanProcessing || this.saving || !this.book?._id || this.availabilityState === 'loaned') {
      return;
    }

    const validationMessage = validateBookLoanForm({
      borrowerName: this.loanForm.borrowerName,
      borrowerEmail: this.loanForm.borrowerEmail,
      dueAt: this.loanForm.dueAt,
      minimumLoanDueDate: this.minimumLoanDueDate,
      minimumLoanDueDateLabel: this.minimumLoanDueDateLabel,
    });

    if (validationMessage) {
      this.loanErrorMessage = validationMessage;
      this.errorMessage = '';
      return;
    }

    this.startLoan();
  }

  startLoan(): void {
    if (!this.book?._id || !this.canCreateLoan) {
      return;
    }

    this.loanProcessing = true;
    this.successMessage = '';
    this.errorMessage = '';
    this.loanErrorMessage = '';

    const payload: LoanCreatePayload = {
      bookId: this.book._id,
      borrowerName: this.loanForm.borrowerName.trim(),
      borrowerEmail: this.loanForm.borrowerEmail.trim(),
      notes: this.loanForm.notes.trim() || null,
      dueAt: this.toIsoDate(this.loanForm.dueAt),
    };

    this.loanService.startLoan(payload).pipe(
      finalize(() => {
        this.loanProcessing = false;
      }),
    ).subscribe({
      next: (loan) => {
        this.cancelActiveLoanLookup();
        this.applyLoadedActiveLoan(loan);

        if (this.book) {
          this.book = {
            ...this.book,
            available: false,
          };
        }

        this.navigateToBooks('A kölcsönzés sikeresen elindult.');
      },
      error: (error: HttpErrorResponse) => {
        this.loanErrorMessage = this.extractErrorMessage(error, 'Nem sikerült elindítani a kölcsönzést.');
        this.errorMessage = '';
        this.successMessage = '';
        this.refreshView();
      },
    });
  }

  returnLoan(): void {
    if (this.loanProcessing || this.saving) {
      return;
    }

    if (this.loanLoading) {
      this.loanErrorMessage = 'Az aktív kölcsönzési adatok még betöltés alatt vannak.';
      return;
    }

    if (!this.activeLoan?._id) {
      this.loanErrorMessage = 'Ehhez a könyvhöz nem található aktív kölcsönzés.';
      return;
    }

    this.loanProcessing = true;
    this.successMessage = '';
    this.errorMessage = '';
    this.loanErrorMessage = '';

    this.loanService.returnLoan(this.activeLoan._id).pipe(
      finalize(() => {
        this.loanProcessing = false;
      }),
    ).subscribe({
      next: () => {
        this.cancelActiveLoanLookup();
        this.activeLoan = undefined;
        this.hasActiveLoanConflict = false;

        if (this.book) {
          this.book = {
            ...this.book,
            available: true,
            hasActiveLoan: false,
          };
        }

        this.loanForm = this.createEmptyLoanForm();
        this.navigateToBooks('A kölcsönzés sikeresen megszüntetve.');
      },
      error: (error: HttpErrorResponse) => {
        this.loanErrorMessage = this.extractErrorMessage(error, 'Nem sikerült megszüntetni a kölcsönzést.');
        this.errorMessage = '';
        this.successMessage = '';
        this.refreshView();
      },
    });
  }

  deleteBook(): void {
    if (this.activeLoan) {
      this.errorMessage = 'Kikölcsönözött könyv nem törölhető.';
      return;
    }

    if (this.saving || this.loanProcessing || !this.book?._id || !confirm(`Biztosan törlöd ezt a könyvet: ${this.book.title}?`)) {
      return;
    }

    this.bookService.deleteBook(this.book._id).subscribe({
      next: () => {
        this.navigateToBooks('A könyv törlése sikeres.');
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage = this.extractErrorMessage(error, 'Nem sikerült törölni a könyvet.');
        this.refreshView();
      },
    });
  }

  goBackToBooks(): void {
    if (this.saving || this.loanProcessing) {
      return;
    }

    this.navigateToBooks();
  }

  private syncLoanState(book: Book): void {
    // A backend can describe the same domain state in multiple ways:
    // embedded loan payload, active-loan flag only, or a manually unavailable book.
    if (!book._id) {
      this.activeLoan = undefined;
      this.hasActiveLoanConflict = false;
      this.cancelActiveLoanLookup();
      this.loanForm = this.createEmptyLoanForm();
      return;
    }

    if (book.activeLoan) {
      this.applyLoadedActiveLoan(book.activeLoan);
      this.cancelActiveLoanLookup();
      return;
    }

    if (book.available) {
      this.activeLoan = undefined;
      this.hasActiveLoanConflict = false;
      this.cancelActiveLoanLookup();
      this.loanErrorMessage = '';
      this.loanForm = this.createEmptyLoanForm();
      return;
    }

    if (!book.hasActiveLoan) {
      this.activeLoan = undefined;
      this.hasActiveLoanConflict = false;
      this.cancelActiveLoanLookup();
      this.loanErrorMessage = '';
      this.loanForm = this.createEmptyLoanForm();
      return;
    }

    this.activeLoan = undefined;
    this.loanForm = this.createEmptyLoanForm();
    this.hasActiveLoanConflict = true;
    this.loadActiveLoan(book._id);
  }

  private primeLoanStateFromNavigationBook(id: string): void {
    if (this.book?._id !== id || this.book.available) {
      return;
    }

    // Preserve already known loan data from the list until the detail reload completes.
    if (this.book.activeLoan) {
      this.applyLoadedActiveLoan(this.book.activeLoan);
      this.cancelActiveLoanLookup();
    }
  }

  private navigateToBooks(systemMessage = ''): void {
    void this.router.navigate(['/books'], {
      queryParams: { refresh: Date.now() },
      state: systemMessage ? { systemMessage } : undefined,
    });
  }

  private createEmptyLoanForm(): LoanFormModel {
    return {
      borrowerName: '',
      borrowerEmail: '',
      dueAt: '',
      notes: '',
    };
  }

  private createLoanFormFromLoan(loan: Loan): LoanFormModel {
    return {
      borrowerName: loan.borrowerName,
      borrowerEmail: loan.borrowerEmail ?? '',
      dueAt: this.toDateValue(loan.dueAt),
      notes: loan.notes ?? '',
    };
  }

  private clearLoadedBookState(): void {
    this.book = undefined;
    this.activeLoan = undefined;
    this.hasActiveLoanConflict = false;
    this.loanForm = this.createEmptyLoanForm();
  }

  private applyLoadedActiveLoan(loan: Loan): void {
    this.activeLoan = loan;
    this.hasActiveLoanConflict = false;

    if (this.book) {
      this.book = {
        ...this.book,
        available: false,
        hasActiveLoan: true,
      };
    }

    this.loanForm = this.createLoanFormFromLoan(loan);
  }

  private beginActiveLoanLookup(bookId: string): number {
    this.activeLoanRequestId += 1;
    this.activeLoanRequestBookId = bookId;
    this.loanLoading = true;
    this.loanErrorMessage = '';
    return this.activeLoanRequestId;
  }

  private finishActiveLoanLookup(requestId: number): void {
    if (this.activeLoanRequestId !== requestId) {
      return;
    }

    this.activeLoanRequestBookId = '';
    this.loanLoading = false;
  }

  private cancelActiveLoanLookup(): void {
    this.activeLoanRequestId += 1;
    this.activeLoanRequestBookId = '';
    this.loanLoading = false;
  }

  private isCurrentActiveLoanLookup(bookId: string, requestId: number): boolean {
    return this.activeLoanRequestId === requestId && this.activeLoanRequestBookId === bookId;
  }

  private loadActiveLoanFallback(bookId: string, requestId: number): void {
    this.loanService.getActiveLoans().pipe(
      timeout(5000),
    ).subscribe({
      next: (loans) => {
        if (!this.isCurrentActiveLoanLookup(bookId, requestId)) {
          return;
        }

        // Fallback to the broader active-loans query when the per-book endpoint
        // cannot provide the matching item even though the book is still marked as loaned.
        const fallbackLoan = loans.find((loan) => String(loan.bookId) === bookId);

        if (fallbackLoan) {
          this.applyLoadedActiveLoan(fallbackLoan);
          this.finishActiveLoanLookup(requestId);
          this.refreshView();
          return;
        }

        this.handleMissingActiveLoan(requestId);
        this.refreshView();
      },
      error: () => {
        this.handleActiveLoanLookupError(bookId, requestId);
        this.refreshView();
      },
    });
  }

  private handleMissingActiveLoan(requestId: number): void {
    this.activeLoan = undefined;

    if (this.book) {
      this.book = {
        ...this.book,
        hasActiveLoan: false,
      };
    }

    this.loanForm = this.createEmptyLoanForm();
    this.hasActiveLoanConflict = false;
    this.finishActiveLoanLookup(requestId);
  }

  private handleActiveLoanLookupError(bookId: string, requestId: number): void {
    if (!this.isCurrentActiveLoanLookup(bookId, requestId)) {
      return;
    }

    this.activeLoan = undefined;
    this.loanErrorMessage = 'Nem sikerült betölteni az aktív kölcsönzési adatokat.';
    this.finishActiveLoanLookup(requestId);
  }

  private handleAvailabilityConflict(error: HttpErrorResponse): void {
    if (!this.isActiveLoanConflict(error) || !this.book?._id) {
      return;
    }

    // Saving "available" can race with a server-side active loan.
    // In that case we immediately switch the UI back to the loaned path.
    const conflictedBookId = this.book._id;
    this.book = {
      ...this.book,
      available: false,
      hasActiveLoan: true,
    };
    this.hasActiveLoanConflict = true;
    this.loadActiveLoan(conflictedBookId);
  }

  private toIsoDate(value: string): string {
    return isIsoDateOnly(value) ? value : '';
  }

  private isActiveLoanConflict(error: HttpErrorResponse): boolean {
    if (error.error?.code === ACTIVE_LOAN_CONFLICT_CODE) {
      return true;
    }

    const sourceText = [
      error.error?.message,
      ...(Array.isArray(error.error?.errors) ? error.error.errors : []),
    ].join(' ');
    const normalizedSourceText = sourceText
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '');

    return normalizedSourceText.includes('aktiv kolcsonz')
      || (normalizedSourceText.includes('kolcsonz') && normalizedSourceText.includes('elerheto'))
      || (normalizedSourceText.includes('jelolhet') && normalizedSourceText.includes('elerheto'));
  }

  private extractErrorMessage(error: HttpErrorResponse, fallbackMessage: string): string {
    const message = error.error?.message;
    const firstValidationError = Array.isArray(error.error?.errors) ? error.error.errors[0] : undefined;

    if (typeof message === 'string' && message.trim()) {
      return message;
    }

    if (typeof firstValidationError === 'string' && firstValidationError.trim()) {
      return firstValidationError;
    }

    return fallbackMessage;
  }

  private toDateValue(value: string | null): string {
    return extractDateOnly(value);
  }

  formatLoanDateLabel(value: string | null | undefined): string {
    return formatDateOnlyLabel(value);
  }

  private toLocalDateValue(value: Date): string {
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private refreshView(): void {
    this.changeDetectorRef.detectChanges();
  }
}
