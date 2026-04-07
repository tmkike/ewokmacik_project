import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { Book, BookFilters } from '../../models/book';
import { BookService } from '../../services/book.service';

@Component({
  selector: 'app-books',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './books.html',
  styleUrl: './books.scss',
})
export class Books {
  allBooks: Book[] = [];
  books: Book[] = [];
  loading = false;
  errorMessage = '';
  filters: BookFilters = {
    title: '',
    genre: '',
    author: '',
    available: '',
  };

  constructor(
    private readonly bookService: BookService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.loadBooks();
  }

  loadBooks(): void {
    this.loading = true;
    this.errorMessage = '';

    this.bookService.getBooks().subscribe({
      next: (books) => {
        this.allBooks = books;
        this.applyFilters();
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Nem sikerult betolteni a konyveket.';
        this.loading = false;
      },
    });
  }

  applyFilters(): void {
    this.books = this.applyClientSideFilters(this.allBooks);
  }

  clearFilters(): void {
    this.filters = {
      title: '',
      genre: '',
      author: '',
      available: '',
    };
    this.applyFilters();
  }

  openBook(book: Book): void {
    if (!book._id) {
      this.errorMessage = 'A konyvnek nincs azonositoja, ezert nem nyithato meg.';
      return;
    }

    void this.router.navigate(['/books', book._id], {
      state: { book },
    });
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
}
