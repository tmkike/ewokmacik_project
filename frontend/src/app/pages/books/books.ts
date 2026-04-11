import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ChangeDetectorRef, Component, DestroyRef, OnDestroy, OnInit, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, skip } from 'rxjs';

import { Book, BookFilters, BookListResponse } from '../../models/book';
import { BookService } from '../../services/book.service';
import { getBookAvailabilityLabel } from '../../shared/book-availability';

@Component({
  selector: 'app-books',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './books.html',
  styleUrl: './books.scss',
})
export class Books implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  private successMessageTimeoutId?: ReturnType<typeof setTimeout>;
  private readonly defaultPageSize = 8;
  private readonly successMessageDurationMs = 1500;

  readonly filterForm = new FormGroup({
    title: new FormControl('', { nonNullable: true }),
    author: new FormControl('', { nonNullable: true }),
    genre: new FormControl('', { nonNullable: true }),
    available: new FormControl<boolean | null>(null),
  });

  books: Book[] = [];
  currentPage = 1;
  pageSize = this.defaultPageSize;
  totalCount = 0;
  loading = false;
  errorMessage = '';
  successMessage = '';
  successMessageVisible = false;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly bookService: BookService,
    private readonly router: Router,
    private readonly changeDetectorRef: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.resetFiltersForPageEntry();
    this.readNavigationMessage();
    this.loadFirstPage();

    this.route.queryParamMap.pipe(
      skip(1),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => {
      // The refresh query param is used as a lightweight "re-enter the list" signal
      // after create/update flows, so we intentionally reopen the page in its default state.
      this.resetFiltersAndLoadFirstPage();
    });
  }

  ngOnDestroy(): void {
    this.clearSuccessMessageTimeout();
  }

  searchBooks(): void {
    this.loadFirstPage();
  }

  clearFilters(): void {
    this.resetFiltersAndLoadFirstPage();
  }

  goToPage(page: number): void {
    if (this.loading || page === this.currentPage || page < 1 || page > this.totalPages) {
      return;
    }

    this.currentPage = page;
    this.loadBooks();
  }

  openBook(book: Book): void {
    if (!book._id) {
      this.errorMessage = 'A könyvnek nincs azonosítója, ezért nem nyitható meg.';
      return;
    }

    // Pass the current row state forward so the detail page can render immediately
    // before it refreshes the book from the backend.
    void this.router.navigate(['/books', book._id], {
      state: { book },
    });
  }

  getBookAvailabilityLabel(book: Book): string {
    return getBookAvailabilityLabel(book);
  }

  getBookAvailabilityClass(book: Book): string {
    if (book.hasActiveLoan) {
      return 'availability availability--loaned';
    }

    return book.available
      ? 'availability availability--available'
      : 'availability availability--unavailable';
  }

  get totalPages(): number {
    return this.totalCount === 0
      ? 1
      : Math.ceil(this.totalCount / this.pageSize);
  }

  get pageStart(): number {
    return this.totalCount === 0
      ? 0
      : ((this.currentPage - 1) * this.pageSize) + 1;
  }

  get pageEnd(): number {
    return this.totalCount === 0
      ? 0
      : Math.min(this.currentPage * this.pageSize, this.totalCount);
  }

  get visiblePages(): number[] {
    const pages: number[] = [];
    const startPage = Math.max(1, this.currentPage - 1);
    const endPage = Math.min(this.totalPages, startPage + 2);
    const adjustedStartPage = Math.max(1, endPage - 2);

    for (let page = adjustedStartPage; page <= endPage; page += 1) {
      pages.push(page);
    }

    return pages;
  }

  private loadBooks(filters: BookFilters = this.createRequestFilters()): void {
    this.loading = true;
    this.errorMessage = '';

    this.bookService.getBooks(filters).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => {
        this.handleBookLoadError();
        return EMPTY;
      }),
    ).subscribe({
      next: (response) => {
        this.applyBooksResponse(response);
      },
      complete: () => {
        this.finishBookLoad();
      },
    });
  }

  private readNavigationMessage(): void {
    // The flash message may arrive from the current router transition or from the
    // preserved browser history state after a redirect back to the list.
    const navigationMessage = this.router.getCurrentNavigation()?.extras.state?.['systemMessage']
      ?? history.state?.systemMessage;

    this.successMessage = typeof navigationMessage === 'string' ? navigationMessage : '';

    if (this.successMessage) {
      this.successMessageVisible = true;
      this.clearNavigationMessageFromHistory();
      this.startSuccessMessageTimer();
    }
  }

  private startSuccessMessageTimer(): void {
    this.clearSuccessMessageTimeout();
    this.successMessageTimeoutId = setTimeout(() => {
      this.successMessageVisible = false;
      this.successMessage = '';
      this.successMessageTimeoutId = undefined;
      this.changeDetectorRef.detectChanges();
    }, this.successMessageDurationMs);
  }

  private clearSuccessMessageTimeout(): void {
    if (this.successMessageTimeoutId) {
      clearTimeout(this.successMessageTimeoutId);
      this.successMessageTimeoutId = undefined;
    }
  }

  private clearNavigationMessageFromHistory(): void {
    // Consume the flash message once so back/refresh does not replay stale feedback.
    const nextState = {
      ...history.state,
      systemMessage: undefined,
    };

    history.replaceState(nextState, document.title, window.location.href);
  }

  private loadFirstPage(): void {
    this.currentPage = 1;
    this.loadBooks();
  }

  private resetFiltersAndLoadFirstPage(): void {
    this.resetFiltersForPageEntry();
    this.loadFirstPage();
  }

  private applyBooksResponse(response: BookListResponse): void {
    this.books = response.items;
    this.currentPage = response.page;
    this.pageSize = response.pageSize;
    this.totalCount = response.totalCount;
  }

  private handleBookLoadError(): void {
    this.errorMessage = 'Nem sikerült betölteni a könyveket.';
    this.books = [];
    this.totalCount = 0;
  }

  private finishBookLoad(): void {
    this.loading = false;
    this.changeDetectorRef.detectChanges();
  }

  private createRequestFilters(): BookFilters {
    const { title, author, genre, available } = this.filterForm.getRawValue();
    const requestFilters: BookFilters = {
      page: this.currentPage,
      pageSize: this.pageSize,
    };

    if (title.trim()) {
      requestFilters.title = title.trim();
    }

    if (author.trim()) {
      requestFilters.author = author.trim();
    }

    if (genre.trim()) {
      requestFilters.genre = genre.trim();
    }

    if (typeof available === 'boolean') {
      requestFilters.available = available;
    }

    return requestFilters;
  }

  private resetFiltersForPageEntry(): void {
    this.filterForm.reset({
      title: '',
      author: '',
      genre: '',
      available: null,
    });
  }
}
