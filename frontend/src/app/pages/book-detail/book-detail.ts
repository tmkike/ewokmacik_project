import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize, timeout } from 'rxjs';

import { Book } from '../../models/book';
import { Loan, LoanCreatePayload } from '../../models/loan';
import { BookService } from '../../services/book.service';
import { LoanService } from '../../services/loan.service';

type LoanFormModel = {
  borrowerName: string;
  borrowerEmail: string;
  dueAt: string;
  notes: string;
};

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
  ) {
    const navigationBook = this.router.getCurrentNavigation()?.extras.state?.['book'] as Book | undefined;

    if (navigationBook) {
      this.book = { ...navigationBook };
    }
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');

      if (!id) {
        this.errorMessage = 'Hiányzó könyvazonosító.';
        this.loading = false;
        this.loanLoading = false;
        return;
      }

      this.bookId = id;
      this.loadBook(id);
    });
  }

  get canCreateLoan(): boolean {
    return Boolean(this.book?.available && !this.activeLoan);
  }

  get isLoanedState(): boolean {
    return Boolean(this.activeLoan);
  }

  get showLoanTerminationButton(): boolean {
    return Boolean(this.activeLoan);
  }

  get canDeleteBook(): boolean {
    return !this.activeLoan && !this.hasActiveLoanConflict && !this.loanLoading && !this.loanProcessing && !this.saving;
  }

  get isLoanFormReadOnly(): boolean {
    return Boolean(this.activeLoan) || this.loanLoading || this.loanProcessing || this.saving;
  }

  loadBook(id: string): void {
    this.loading = !this.book;
    this.errorMessage = '';
    this.loanErrorMessage = '';

    this.bookService.getBook(id).pipe(
      finalize(() => {
        this.loading = false;
      }),
    ).subscribe({
      next: (book) => {
        this.book = book;

        if (book.activeLoan) {
          this.applyLoadedActiveLoan(book.activeLoan);
        }

        this.syncLoanState(book);
      },
      error: () => {
        this.activeLoan = undefined;
        this.hasActiveLoanConflict = false;
        this.loanLoading = false;
        this.errorMessage = `Nem sikerült betölteni a könyvet. Azonosító: ${id}`;
      },
    });
  }

  loadActiveLoan(bookId: string): void {
    this.loanLoading = true;
    this.loanErrorMessage = '';

    this.loanService.getActiveLoanForBook(bookId).pipe(
      timeout(5000),
      finalize(() => {
        this.loanLoading = false;
      }),
    ).subscribe({
      next: (loan) => {
        if (loan) {
          this.applyLoadedActiveLoan(loan);
          return;
        }

        this.loadActiveLoanFallback(bookId);
      },
      error: () => {
        this.activeLoan = undefined;
        this.loanErrorMessage = 'Nem sikerült betölteni az aktív kölcsönzési adatokat.';

        if (this.book?.available) {
          this.loanForm = this.createEmptyLoanForm();
        }
      },
    });
  }

  private loadActiveLoanFallback(bookId: string): void {
    this.loanService.getActiveLoans().pipe(
      timeout(5000),
    ).subscribe({
      next: (loans) => {
        const fallbackLoan = loans.find((loan) => String(loan.bookId) === bookId);

        if (fallbackLoan) {
          this.applyLoadedActiveLoan(fallbackLoan);
          return;
        }

        this.activeLoan = undefined;

        if (this.book?.available) {
          this.loanForm = this.createEmptyLoanForm();
        }
      },
      error: () => {
        this.activeLoan = undefined;
        this.loanErrorMessage = 'Nem sikerült betölteni az aktív kölcsönzési adatokat.';

        if (this.book?.available) {
          this.loanForm = this.createEmptyLoanForm();
        }
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
        if (this.isActiveLoanConflict(error) && this.book?._id) {
          const conflictedBookId = this.book._id;
          this.book = {
            ...this.book,
            available: false,
          };
          this.hasActiveLoanConflict = true;
          this.loadActiveLoan(conflictedBookId);
        }

        this.errorMessage = error.error?.message || 'Nem sikerült elmenteni a változásokat.';
        this.successMessage = '';
      },
    });
  }

  saveLoan(): void {
    if (this.loanProcessing || this.saving || !this.book?._id || this.isLoanedState) {
      return;
    }

    this.startLoan();
  }

  startLoan(): void {
    if (!this.book?._id) {
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
        this.activeLoan = loan;

        if (this.book) {
          this.book = {
            ...this.book,
            available: false,
          };
        }

        this.loanForm = this.createLoanFormFromLoan(loan);
        this.navigateToBooks('A kölcsönzés sikeresen elindult.');
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage = error.error?.message || 'Nem sikerült elindítani a kölcsönzést.';
        this.successMessage = '';
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
        this.activeLoan = undefined;
        this.hasActiveLoanConflict = false;

        if (this.book) {
          this.book = {
            ...this.book,
            available: true,
          };
        }

        this.loanForm = this.createEmptyLoanForm();
        this.navigateToBooks('A kölcsönzés sikeresen megszüntetve.');
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage = error.error?.message || 'Nem sikerült megszüntetni a kölcsönzést.';
        this.successMessage = '';
      },
    });
  }

  deleteBook(): void {
    if (this.activeLoan) {
      this.errorMessage = 'Kikölcsönzött könyv nem törölhető.';
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
        this.errorMessage = error.error?.message || 'Nem sikerült törölni a könyvet.';
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
    if (!book._id) {
      this.activeLoan = undefined;
      this.hasActiveLoanConflict = false;
      this.loanLoading = false;
      this.loanForm = this.createEmptyLoanForm();
      return;
    }

    if (book.available) {
      this.activeLoan = undefined;
      this.hasActiveLoanConflict = false;
      this.loanLoading = false;
      this.loanErrorMessage = '';
      this.loanForm = this.createEmptyLoanForm();
      return;
    }

    if (book.activeLoan) {
      this.applyLoadedActiveLoan(book.activeLoan);
      this.loanLoading = false;
      return;
    }

    this.activeLoan = undefined;
    this.loanForm = this.createEmptyLoanForm();

    if (!book.hasActiveLoan) {
      this.hasActiveLoanConflict = false;
      this.loanLoading = false;
      return;
    }

    this.hasActiveLoanConflict = true;
    this.loadActiveLoan(book._id);
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

  private applyLoadedActiveLoan(loan: Loan): void {
    this.activeLoan = loan;
    this.hasActiveLoanConflict = false;
    this.loanForm = this.createLoanFormFromLoan(loan);
  }

  private toIsoDate(value: string): string {
    return value;
  }

  private isActiveLoanConflict(error: HttpErrorResponse): boolean {
    const message = String(error.error?.message || '').toLowerCase();
    return message.includes('aktív kölcsönzés');
  }

  private toDateValue(value: string | null): string {
    if (!value) {
      return '';
    }

    return new Date(value).toISOString().slice(0, 10);
  }
}
