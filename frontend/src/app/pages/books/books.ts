import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ChangeDetectorRef, Component, DestroyRef, OnDestroy, OnInit, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, skip } from 'rxjs';

import { Book, BookFilters } from '../../models/book';
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

  readonly filterForm = new FormGroup({
    title: new FormControl('', { nonNullable: true }),
    author: new FormControl('', { nonNullable: true }),
    genre: new FormControl('', { nonNullable: true }),
    available: new FormControl<boolean | null>(null),
  });

  books: Book[] = [];
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
    this.loadBooks();

    this.route.queryParamMap.pipe(
      skip(1),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => {
      this.resetFiltersForPageEntry();
      this.loadBooks();
    });
  }

  ngOnDestroy(): void {
    this.clearSuccessMessageTimeout();
  }

  searchBooks(): void {
    this.loadBooks();
  }

  clearFilters(): void {
    this.resetFiltersForPageEntry();
    this.loadBooks();
  }

  openBook(book: Book): void {
    if (!book._id) {
      this.errorMessage = 'A k\u00f6nyvnek nincs azonos\u00edt\u00f3ja, ez\u00e9rt nem nyithat\u00f3 meg.';
      return;
    }

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

  private loadBooks(filters: BookFilters = this.createRequestFilters()): void {
    this.loading = true;
    this.errorMessage = '';

    this.bookService.getBooks(filters).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => {
        this.errorMessage = 'Nem siker\u00fclt bet\u00f6lteni a k\u00f6nyveket.';
        this.books = [];
        return EMPTY;
      }),
    ).subscribe({
      next: (response) => {
        this.books = response.items;
      },
      complete: () => {
        this.loading = false;
        this.changeDetectorRef.detectChanges();
      },
    });
  }

  private readNavigationMessage(): void {
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
    }, 1500);
  }

  private clearSuccessMessageTimeout(): void {
    if (this.successMessageTimeoutId) {
      clearTimeout(this.successMessageTimeoutId);
      this.successMessageTimeoutId = undefined;
    }
  }

  private clearNavigationMessageFromHistory(): void {
    const nextState = {
      ...history.state,
      systemMessage: undefined,
    };

    history.replaceState(nextState, document.title, window.location.href);
  }

  private createRequestFilters(): BookFilters {
    const { title, author, genre, available } = this.filterForm.getRawValue();
    const requestFilters: BookFilters = {
      page: 1,
      pageSize: 50,
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
