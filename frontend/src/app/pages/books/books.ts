import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ChangeDetectorRef, Component, DestroyRef, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, finalize, skip } from 'rxjs';

import { Book, BookFilters } from '../../models/book';
import { BookService } from '../../services/book.service';
import { getBookAvailabilityLabel } from '../../shared/book-availability';

@Component({
  selector: 'app-books',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './books.html',
  styleUrl: './books.scss',
})
export class Books implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  private readonly filterChanges = new Subject<BookFilters>();
  private loadRequestId = 0;
  private successMessageTimeoutId?: ReturnType<typeof setTimeout>;

  books: Book[] = [];
  loading = false;
  errorMessage = '';
  successMessage = '';
  successMessageVisible = false;
  filters: BookFilters = this.createEmptyFilters();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly bookService: BookService,
    private readonly router: Router,
    private readonly changeDetectorRef: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.readNavigationMessage();
    this.loadBooks();

    this.route.queryParamMap.pipe(
      skip(1),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => {
      this.refreshBooks(true);
    });

    this.filterChanges.pipe(
      debounceTime(300),
      distinctUntilChanged((previous, current) => JSON.stringify(previous) === JSON.stringify(current)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe((filters) => {
      this.loadBooks(filters);
    });
  }

  ngOnDestroy(): void {
    this.clearSuccessMessageTimeout();
  }

  loadBooks(filters: BookFilters = this.createFilterSnapshot()): void {
    const requestId = ++this.loadRequestId;
    this.loading = true;
    this.errorMessage = '';

    // A szűrés a backendben történik, így nagyobb adatmennyiségnél sem kell mindent letölteni.
    this.bookService.getBooks(filters).pipe(
      finalize(() => {
        if (requestId === this.loadRequestId) {
          this.loading = false;
        }
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: (books) => {
        if (requestId !== this.loadRequestId) {
          return;
        }

        this.books = books;
      },
      error: () => {
        if (requestId !== this.loadRequestId) {
          return;
        }

        this.errorMessage = 'Nem sikerült betölteni a könyveket.';
        this.books = [];
      },
    });
  }

  onFiltersChanged(): void {
    this.filterChanges.next(this.createFilterSnapshot());
  }

  applyFilters(): void {
    this.loadBooks();
  }

  clearFilters(): void {
    this.filters = this.createEmptyFilters();
    this.loadBooks();
  }

  openBook(book: Book): void {
    if (!book._id) {
      this.errorMessage = 'A könyvnek nincs azonosítója, ezért nem nyitható meg.';
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

  private refreshBooks(resetFilters: boolean): void {
    if (resetFilters) {
      // Visszanavigáláskor mindig teljes listával, alap szűrőkkel indulunk.
      this.filters = this.createEmptyFilters();
    }

    this.loadBooks();
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
    // A sikerüzenetet csak egyszer szeretnénk megjeleníteni visszanavigálás után.
    const nextState = {
      ...history.state,
      systemMessage: undefined,
    };

    history.replaceState(nextState, document.title, window.location.href);
  }

  private createFilterSnapshot(): BookFilters {
    return {
      title: this.filters.title?.trim() ?? '',
      author: this.filters.author?.trim() ?? '',
      genre: this.filters.genre?.trim() ?? '',
      available: typeof this.filters.available === 'boolean' ? this.filters.available : '',
    };
  }

  private createEmptyFilters(): BookFilters {
    return {
      title: '',
      author: '',
      genre: '',
      available: '',
    };
  }
}
