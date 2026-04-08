import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, skip, Subscription } from 'rxjs';

import { Book, BookFilters } from '../../models/book';
import { BookService } from '../../services/book.service';

@Component({
  selector: 'app-books',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './books.html',
  styleUrl: './books.scss',
})
export class Books implements OnInit, OnDestroy {
  private booksSubscription?: Subscription;
  private refreshSubscription?: Subscription;
  private successMessageTimeoutId?: ReturnType<typeof setTimeout>;

  allBooks: Book[] = [];
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
    this.refreshBooks(true);

    this.refreshSubscription = this.route.queryParamMap.pipe(
      skip(1),
    ).subscribe(() => {
      this.refreshBooks(true);
    });
  }

  ngOnDestroy(): void {
    this.booksSubscription?.unsubscribe();
    this.refreshSubscription?.unsubscribe();
    this.clearSuccessMessageTimeout();
  }

  loadBooks(): void {
    this.booksSubscription?.unsubscribe();
    this.loading = true;
    this.errorMessage = '';

    // A lista egyszer töltődik be a backenből, utána a mezők kliensoldalon szűrnek.
    this.booksSubscription = this.bookService.getBooks().pipe(
      finalize(() => {
        this.loading = false;
      }),
    ).subscribe({
      next: (books) => {
        this.allBooks = books;
        this.applyFilters();
      },
      error: () => {
        this.errorMessage = 'Nem sikerült betölteni a könyveket.';
        this.allBooks = [];
        this.books = [];
      },
    });
  }

  applyFilters(): void {
    this.books = this.applyClientSideFilters(this.allBooks);
  }

  clearFilters(): void {
    this.filters = this.createEmptyFilters();
    this.applyFilters();
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
    const nextState = {
      ...history.state,
      systemMessage: undefined,
    };

    history.replaceState(nextState, document.title, window.location.href);
  }

  private applyClientSideFilters(books: Book[]): Book[] {
    return books.filter((book) => {
      const titleMatches = this.matchesTextFilter(book.title, this.filters.title);
      const authorMatches = this.matchesTextFilter(book.author, this.filters.author);
      const genreMatches = this.matchesTextFilter(book.genre, this.filters.genre);
      const availabilityMatches = typeof this.filters.available !== 'boolean'
        || book.available === this.filters.available;

      return titleMatches && authorMatches && genreMatches && availabilityMatches;
    });
  }

  private matchesTextFilter(value: string, filter?: string): boolean {
    if (!filter?.trim()) {
      return true;
    }

    return value.toLowerCase().includes(filter.trim().toLowerCase());
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
