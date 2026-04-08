import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';

import { Book } from '../../models/book';
import { BookService } from '../../services/book.service';

@Component({
  selector: 'app-book-detail',
  imports: [CommonModule, FormsModule],
  templateUrl: './book-detail.html',
  styleUrl: './book-detail.scss',
})
export class BookDetail implements OnInit {
  book?: Book;
  bookId = '';
  loading = false;
  saving = false;
  successMessage = '';
  errorMessage = '';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly bookService: BookService,
  ) {
    const navigationBook = this.router.getCurrentNavigation()?.extras.state?.['book'] as Book | undefined;

    if (navigationBook) {
      // Ha a listaoldalról jöttünk, azonnal meg tudjuk jeleníteni az előnézeti adatokat.
      this.book = { ...navigationBook };
    }
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');

      if (!id) {
        this.errorMessage = 'Hiányzó könyvazonosító.';
        this.loading = false;
        return;
      }

      this.bookId = id;
      this.loadBook(id);
    });
  }

  loadBook(id: string): void {
    this.loading = !this.book;
    this.errorMessage = '';

    this.bookService.getBook(id).pipe(
      finalize(() => {
        this.loading = false;
      }),
    ).subscribe({
      next: (book) => {
        this.book = book;
      },
      error: () => {
        this.errorMessage = `Nem sikerült betölteni a könyvet. Azonosító: ${id}`;
      },
    });
  }

  saveBook(): void {
    if (!this.book?._id || this.saving) {
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
        this.successMessage = 'A mentés sikeres.';
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage = error.error?.message || 'Nem sikerült elmenteni a változásokat.';
        this.successMessage = '';
      },
    });
  }

  deleteBook(): void {
    if (this.saving || !this.book?._id || !confirm(`Biztosan törlöd ezt a könyvet: ${this.book.title}?`)) {
      return;
    }

    this.bookService.deleteBook(this.book._id).subscribe({
      next: () => {
        this.navigateToBooks();
      },
      error: () => {
        this.errorMessage = 'Nem sikerült törölni a könyvet.';
      },
    });
  }

  goBackToBooks(): void {
    if (this.saving) {
      return;
    }

    this.navigateToBooks();
  }

  private navigateToBooks(): void {
    // A listaoldal így visszatéréskor mindig teljes újratöltést kap.
    void this.router.navigate(['/books'], {
      queryParams: { refresh: Date.now() },
    });
  }
}
